using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProviderSearch.Models
{
    internal class ODataResponse<T>
    {
        [JsonPropertyName("@odata.context")]
        public string OdataContext { get; set; }

        [JsonPropertyName("value")]
        public List<T> Response { get; set; } = new List<T>();
    }
}
