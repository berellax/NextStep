using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Linq;
using System.Text;
using System.Collections.Generic;

using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.NetworkInformation;

namespace ProviderSearch.Activities
{
    public class NSATSearchEngine
    {
        string clientId { get; set; }
        string clientSecret { get; set; }
        string authorityUrl { get; set; }
        string tenantUrl { get; set; }

        private string accessToken;

        public NSATSearchEngine() { }

        public NSATSearchEngine(string clientId, string clientSecret, string authorityUrl, string tenantUrl)
        {
            this.clientId = clientId;
            this.clientSecret = clientSecret;
            this.authorityUrl = authorityUrl;
            this.tenantUrl = tenantUrl;
        }

        public bool IsAuthenticated()
        {
            return (accessToken != null);
        }

        public async Task GetTokenAsync()
        {
            IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri(authorityUrl))
                .Build();

            string[] scopes = new string[] { string.Format("{0}/.default", tenantUrl) };

            AuthenticationResult result = null;

            try
            {
                result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
                if (result != null)
                {
                    this.accessToken = result.AccessToken;
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                app = null;
            }

        }

        public async Task<JObject> FindFacilitiesByZipProximityAsync(string zipCode, int mileRadius)
        {
            if (IsAuthenticated())
            {
                NSATGeopoint zipCodeGeopoint = await GetCoordinatesForZipCode(zipCode);
                NSATMinMaxRange minMaxCoords = CalculateMaxLatLong(zipCodeGeopoint, mileRadius);

                string accountQuery = string.Format("{0}/api/data/v9.1/accounts?$select=name,accountid&$filter=address1_latitude "
                    + "le {1} and address1_latitude ge {2} and address1_longitude le {3} and address1_longitude ge {4}",
                    tenantUrl, minMaxCoords.latitudeMax, minMaxCoords.latitudeMin, minMaxCoords.longitudeMax, minMaxCoords.longitudeMin);

                JObject accountQueryResult = await ExecuteHtpRequest(accountQuery);


                JArray accountResults = (JArray)accountQueryResult["value"];
                bool firstValue = true;

                Dictionary<string, string> accountDictionary = new Dictionary<string, string>();
                StringBuilder profileQuery = new StringBuilder(string.Format("{0}/api/data/v9.1/nsat_facilityprofiles?$filter=", tenantUrl));
                foreach (var accountResult in accountResults)
                {
                    string accountId = accountResult["accountid"].ToString();
                    string name = accountResult["name"].ToString();
                    accountDictionary.Add(accountId, name);
                    if (firstValue)
                    {
                        profileQuery.Append(string.Format("_nsat_facility_value eq {0} ", accountId));
                        firstValue = false;
                    }
                    else
                    {
                        profileQuery.Append(string.Format("or _nsat_facility_value eq {0} ", accountId));
                    }
                }

                JObject profileQueryResult = await ExecuteHtpRequest(profileQuery.ToString());
                JArray profileResults = (JArray)profileQueryResult["value"];
                JArray searchResults = new JArray();
                foreach (var profileResult in profileResults)
                {
                    string facilityId = profileResult["_nsat_facility_value"].ToString();
                    string facilityName = accountDictionary[facilityId];

                    JObject result = new JObject
                    {
                        ["facilityname"] = facilityName,
                        ["facilityid"] = facilityId,
                        ["profile"] = (JObject)profileResult
                    };

                    searchResults.Add(result);
                }

                JObject resultSet = new JObject();
                resultSet["results"] = searchResults;

                return JObject.FromObject(resultSet);

            }
            else
            {
                throw new Exception("Authentication token is null");
            }
        }

        public async Task FindFacilitiesAsync_old(string zipCode, int mileRadius, Action<JObject> processQueryResult)
        {
            if (IsAuthenticated())
            {
                //    NSATGeopoint zipCodeGeopoint = await GetCoordinatesForZipCode(zipCode);
                //    NSATMinMaxRange minMaxCoords = CalculateMaxLatLong(zipCodeGeopoint, mileRadius);

                //    string query = string.Format("{0}/api/data/v9.1/accounts?$select=name,accountid&$filter=address1_latitude "
                //        + "le {1} and address1_latitude ge {2} and address1_longitude le {3} and address1_longitude ge {4}",
                //        tenantUrl, minMaxCoords.latitudeMax, minMaxCoords.latitudeMin, minMaxCoords.longitudeMax, minMaxCoords.longitudeMin);

                //    HttpResponseMessage response = await ExecuteHtpRequest(query);

                //    if (response.IsSuccessStatusCode)
                //    {
                //        string json = await response.Content.ReadAsStringAsync();
                //        JObject result = JsonConvert.DeserializeObject(json) as JObject;
                //        processQueryResult(result);
                //    }
                //    else
                //    {
                //        throw new Exception(string.Format("FindFacilitiesAsync: Http request failed with status code {0}", response.StatusCode.ToString()));
                //    }
            }
        }

        private async Task<JObject> ExecuteHtpRequest(string query)
        {
            HttpClient httpClient = new HttpClient();
            var defaultRequestHeaders = httpClient.DefaultRequestHeaders;

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");

            defaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", accessToken);

            HttpResponseMessage response = await httpClient.GetAsync(query);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject(json) as JObject;
                return result;
            }
            else
            {
                throw new Exception(string.Format("Http request failed with status code {0} for query '{1}", response.StatusCode.ToString(), query));
            }
        }

        private static async Task<NSATGeopoint> GetCoordinatesForZipCode(string zipCode)
        {
            string queryUrl = string.Format("https://public.opendatasoft.com/api/records/1.0/search/?dataset=us-zip-code-latitude-and-longitude&q=&refine.zip={0}", zipCode);

            HttpClient httpClient = new HttpClient();
            var defaultRequestHeaders = httpClient.DefaultRequestHeaders;
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");
            HttpResponseMessage response = await httpClient.GetAsync(queryUrl);

            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                JObject result = JsonConvert.DeserializeObject(json) as JObject;

                //TODO: Need to add null check in case there is no return values
                NSATGeopoint point = point = new NSATGeopoint(
                    double.Parse(result["records"][0]["fields"]["geopoint"][0].ToString()),
                    double.Parse(result["records"][0]["fields"]["geopoint"][1].ToString())
                    );
                return point;
            }
            else
            {
                throw new Exception(string.Format("GetCoordinatesForZipCode: Http request failed with status code {0}", response.StatusCode.ToString()));
            }

        }

        private static double CalculateDistance(NSATGeopoint point1, NSATGeopoint point2, DistanceUnit unit)
        {
            double R = (unit == DistanceUnit.Miles) ? 3960 : 6371;
            var lat = (point2.latitude - point1.latitude).ToRadians();
            var lng = (point2.longitude - point1.longitude).ToRadians();
            var h1 = Math.Sin(lat / 2) * Math.Sin(lat / 2) +
                          Math.Cos(point1.latitude.ToRadians()) * Math.Cos(point2.latitude.ToRadians()) *
                          Math.Sin(lng / 2) * Math.Sin(lng / 2);
            var h2 = 2 * Math.Asin(Math.Min(1, Math.Sqrt(h1)));
            return R * h2;
        }

        private static NSATMinMaxRange CalculateMaxLatLong(NSATGeopoint zipCodeLatLong, int miles)
        {
            double latitudeDelta = miles * 0.01446491;
            double longitudeDelta = miles * 0.01734522;

            NSATMinMaxRange latLongMinMax = new NSATMinMaxRange();
            latLongMinMax.latitudeMin = zipCodeLatLong.latitude - latitudeDelta;
            latLongMinMax.latitudeMax = zipCodeLatLong.latitude + latitudeDelta;
            latLongMinMax.longitudeMin = zipCodeLatLong.longitude - longitudeDelta;
            latLongMinMax.longitudeMax = zipCodeLatLong.longitude + longitudeDelta;

            return latLongMinMax;
        }

    }

    public enum DistanceUnit { Miles, Kilometers };

    public class SearchResult
    {
        public string name { get; set; }
        public string accountId { get; set; }
        public JToken profile { get; set; }

        public SearchResult(string name, string accountId, JToken profile)
        {
            this.name = name;
            this.accountId = accountId;
            this.profile = profile;
        }
    }

    public class NSATGeopoint
    {
        public double latitude { get; set; }
        public double longitude { get; set; }

        public NSATGeopoint(double latitude, double longitude)
        {
            this.latitude = latitude;
            this.longitude = longitude;
        }
    }

    public class NSATMinMaxRange
    {
        public double latitudeMin { get; set; }
        public double latitudeMax { get; set; }
        public double longitudeMax { get; set; }
        public double longitudeMin { get; set; }

    }

    public static class Numbericextensions
    {
        public static double ToRadians(this double val)
        {
            return (Math.PI / 180) * val;
        }
    }
}
