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
    internal class ODataClinicalProfile
    {
        public async Task<JObject> GetClinicalProfiles(HttpClient httpClient, string profileId)
        {
            string endpoint = "nsat_clinicalprofiles";

            string[] selectFields = null;


            var request = new ODataRequest(endpoint, selectFields, profileId);

            var response = await httpClient.RetrieveOdataDynamic<JObject>(request);

            return response;
        }

    }
}
