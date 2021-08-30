using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using ProviderSearch.Models;

namespace ProviderSearch.Responses
{
    internal class ProviderSearchResponse
    {
        [JsonPropertyName("error")]
        public bool IsError { get; set; }

        [JsonPropertyName("errorMessage")]
        public string ErrorMessage { get; set; }

        [JsonPropertyName("providers")]
        public List<ProviderResponse> Providers { get; set; }
    }
}
