using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ProviderSearch.Shared;

namespace ProviderSearch.Models
{
    internal class ODataAccount
    {
        #region Select Field Properties
        [JsonPropertyName("@odata.etag")]
        public string OdataEtag { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("accountid")]
        public string AccountId { get; set; }

        [JsonPropertyName("telephone1")]
        public string Phone { get; set; }

        [JsonPropertyName("address1_fax")]
        public string Fax { get; set; }

        [JsonPropertyName("emailaddress1")]
        public string Email { get; set; }

        [JsonPropertyName("address1_line1")]
        public string Address1 { get; set; }

        [JsonPropertyName("address1_line2")]
        public string Address2 { get; set; }

        [JsonPropertyName("address1_city")]
        public string City { get; set; }

        [JsonPropertyName("address1_stateorprovince")]
        public string StateOrProvince { get; set; }

        [JsonPropertyName("address1_postalcode")]
        public string PostalCode { get; set; }

        [JsonPropertyName("nsat_headline")]
        public string Headline { get; set; }

        [JsonPropertyName("nsat_currentpromotions")]
        public string CurrentPromotions { get; set; }

        [JsonPropertyName("nsat_shortdescription")]
        public string ShortDescription { get; set; }

        [JsonPropertyName("nsat_longdescription")]
        public string LongDescription { get; set; }

        [JsonPropertyName("address1_latitude")]
        public double AddressLatitude { get; set; }

        [JsonPropertyName("address1_longitude")]
        public double AddressLongitude { get; set; }

        [JsonPropertyName("statecode")]
        public int StateCode { get; set; }

        [JsonPropertyName("_nsat_clinicalprofile_value")]
        public string ClinicalProfileId { get; set; }

        [JsonPropertyName("_nsat_residentialprofile_value")]
        public string ResidentialProfileId { get; set; }

        public Dictionary<string, bool> ClinicalOptions { get; set; }
        public ResidentialProfile ResidentialProfile { get; set; }

        #endregion

        public async Task<ODataResponse<ODataAccount>> GetAccounts(HttpClient httpClient, GeoRange geoRange)
        {
            string accountEndpoint = "accounts";

            string[] selectFields = new string[]
            {
                "name",
                "accountid",
                "telephone1",
                "address1_fax",
                "emailaddress1",
                "address1_line1",
                "address1_line2",
                "address1_city",
                "address1_stateorprovince",
                "address1_postalcode",
                "nsat_headline",
                "nsat_shortdescription",
                "nsat_longdescription",
                "address1_latitude",
                "address1_longitude",
                "_nsat_clinicalprofile_value",
                "_nsat_residentialprofile_value",
                "nsat_currentpromotions"
            };

            StringBuilder filterBuilder = new StringBuilder();
            filterBuilder.Append($"address1_latitude le {geoRange.LatitudeMax}");
            filterBuilder.Append($" and address1_latitude ge {geoRange.LatitudeMin}");
            filterBuilder.Append($" and address1_longitude le {geoRange.LongitudeMax}");
            filterBuilder.Append($" and address1_longitude ge {geoRange.LongitudeMin}");
            filterBuilder.Append($" and statecode eq 0");

            string filter = filterBuilder.ToString();

            var request = new ODataRequest(accountEndpoint, selectFields, null, filter);

            var response = await httpClient.RetrieveOdata<ODataAccount>(request);

            return response;
        }

    }
}
