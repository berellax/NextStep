using System;
using System.Text.Json.Serialization;

namespace ProviderSearch.Models
{
    internal class ResponseContent
    {
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("userMessage")]
        public string UserMessage { get; set; }

        [JsonPropertyName("developerMessage")]
        public string DeveloperMessage { get; set; }

        [JsonPropertyName("requestId")]
        public string RequestId { get; set; }

        [JsonPropertyName("moreInfo")]
        public string MoreInfo { get; set; }

        [JsonIgnore]
        public Exception Exception { get; set; }

        public ResponseContent(
            string userMessage,
            string developerMessage,
            string moreInfo = null,
            Exception exception = null
            )
        {
            UserMessage = userMessage;
            DeveloperMessage = developerMessage;
            MoreInfo = moreInfo;
            Exception = exception;
        }
    }
}
