using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ProviderSearch.Shared;
using Newtonsoft.Json.Linq;

namespace ProviderSearch.Models
{
    internal class ODataResidentialProfile
    {
        public async Task<JObject> GetResidentialProfiles(HttpClient httpClient, string profileId)
        {
            string facilityProfileEndpoint = "nsat_residentialprofiles";

            string[] selectFields = new string[0];

            var request = new ODataRequest(facilityProfileEndpoint, selectFields, profileId);

            var response = await httpClient.RetrieveOdataDynamic<JObject>(request);

            return response;
        }

    }
}
