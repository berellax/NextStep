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
    internal class ODataContact
    {
        [JsonPropertyName("@odata.etag")]
        public string OdataEtag { get; set; }

        [JsonPropertyName("contactid")]
        public string Name { get; set; }

        [JsonPropertyName("_nsat_clinicalprofile_value")]
        public string ClinicalProfileId { get; set; }

        [JsonPropertyName("_nsat_residentialprofile_value")]
        public string ResidentialProfileId { get; set; }

        [JsonPropertyName("fullname")]
        public string FullName { get; set; }

        public Dictionary<string, bool> ClinicalOptions { get; set; }
        public ResidentialProfile ResidentialProfile { get; set; }

        public async Task<ODataContact> GetContacts(HttpClient httpClient, string contactId)
        {
            string endpoint = "contacts";

            string[] selectFields = new string[]
            {
                "contactid",
                "_nsat_clinicalprofile_value",
                "_nsat_residentialprofile_value",
                "fullname"
            };

            var request = new ODataRequest(endpoint, selectFields, contactId);

            var response = await httpClient.RetrieveOdataSingle<ODataContact>(request);

            return response;
        }

    }
}
