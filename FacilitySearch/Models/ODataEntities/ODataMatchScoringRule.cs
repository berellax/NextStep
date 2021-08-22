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
    internal class ODataMatchScoringRule
    {
        private string _friendlyName;

        [JsonPropertyName("@odata.etag")]
        public string OdataEtag { get; set; }

        [JsonPropertyName("nsat_profilefield")]
        public string Field { get; set; }

        [JsonPropertyName("nsat_score")]
        public int Score { get; set; }

        [JsonPropertyName("nsat_optionalmatch")]
        public bool OptionalMatch { get; set; }

        [JsonPropertyName("nsat_targetprofiletype")]
        public int TargetProfileType { get; set; }

        [JsonPropertyName("nsat_profilefieldfriendlyname")]
        public string FriendlyName {
            get { return _friendlyName; }
            set { _friendlyName = value ?? string.Empty; }
        }

        public async Task<ODataResponse<ODataMatchScoringRule>> GetMatchScoringRules(HttpClient httpClient)
        {
            string accountEndpoint = "nsat_matchscoringrules";

            string[] selectFields = new string[]
            {
                "nsat_profilefield",
                "nsat_score",
                "nsat_optionalmatch",
                "nsat_targetprofiletype",
                "nsat_profilefieldfriendlyname"
            };

            string filter = "statecode eq 0";

            var request = new ODataRequest(accountEndpoint, selectFields, null, filter);

            var response = await httpClient.RetrieveOdata<ODataMatchScoringRule>(request);

            return response;
        }

    }

    internal enum MatchProfileType
    {
        Residential = 100000000,
        Clinical = 100000001
    }
}
