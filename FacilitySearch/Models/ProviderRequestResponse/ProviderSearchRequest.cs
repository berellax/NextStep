using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace ProviderSearch.Models
{
    internal class ProviderSearchRequest : ModelProcessing
    {
        private string _zipCode;
        private int _milesRadius;
        private string _contactId;

        [JsonPropertyName("zip")]
        public string ZipCode { 
            get { return _zipCode;  }
            set { _zipCode = _encode(value); }
        }

        [JsonPropertyName("radius")]
        public int MilesRadius
        {
            get { return _milesRadius; }
            set { _milesRadius = value; }
        }

        [JsonPropertyName("contactId")]
        public string ContactId
        {
            get { return _contactId; }
            set { _contactId = _encode(value); }
        }

        [JsonPropertyName("resultCount")]
        public int ResultCount { get; set; }
    }
}
