using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ProviderSearch.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ProviderSearch.Activities
{
    internal class GeoSearch
    {
        internal async Task<GeoPoint> GetCoordinatesForZipCode(string zipCode)
        {
            string geoCodeQueryUri = $"{Config.GeoCodeApi}?key={Config.GeoCodeApiKey}&location={zipCode}";

            HttpClient httpClient = new HttpClient();
            HttpRequestHeaders defaultRequestHeaders = httpClient.DefaultRequestHeaders;
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
            httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");

            var response = await httpClient.GetAsync(geoCodeQueryUri);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Get Coordinates for Zip Code failed with reason: {response.ReasonPhrase}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();

            JObject result = JsonConvert.DeserializeObject(responseContent) as JObject;

            if(result == null)
            {
                throw new Exception($"Get Coordinates for Zip Code did not return any results.");
            }

            var latitude = result["results"][0]["locations"][0]["latLng"]["lat"].ToString();
            var longitude = result["results"][0]["locations"][0]["latLng"]["lng"].ToString();

            GeoPoint point = new GeoPoint (
                    double.Parse(latitude),
                    double.Parse(longitude)
                );

            return point;
        }

        internal GeoRange GetMinMaxLatLong(GeoPoint geoPoint, int radiusMiles)
        {
            double latitudeDelta = radiusMiles * 0.01446491;
            double longitudeDelta = radiusMiles * 0.01734522;

            GeoRange geoRange = new GeoRange();

            geoRange.LatitudeMin = geoPoint.Latitude - latitudeDelta;
            geoRange.LatitudeMax = geoPoint.Latitude + latitudeDelta;
            geoRange.LongitudeMin = geoPoint.Longitude - longitudeDelta;
            geoRange.LongitudeMax = geoPoint.Longitude + longitudeDelta;

            return geoRange;
        }

    }
}
