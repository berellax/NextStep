using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace ProviderSearch.Models
{
    internal class ODataRequest
    {
        public string Endpoint { get; set; }
        public IEnumerable<string> Fields { get; set; }
        public string Filter { get; set; }
        public string Expand { get; set; }
        public string Id { get; set; }
        public ODataRequest(string endpoint, IEnumerable<string> fields = null, string id = null, string filter = null, string expand = null)
        {
            Endpoint = endpoint;
            Fields = fields;
            Filter = filter;
            Expand = expand;
            Id = id;
        }

        public string QueryString
        {
            get
            {
                var endpointSuffix = new List<string>();
                var queryBuilder = new StringBuilder();

                queryBuilder.Append(Endpoint);

                if (Id != string.Empty && Id != null)
                {
                    queryBuilder.Append($"({Id})");                    
                }

                if (Fields != null && Fields.Any())
                    endpointSuffix.Add($"$select={string.Join(',', Fields)}");

                if(Filter != string.Empty 
                    && Filter != null
                    && (Id == string.Empty || Id == null))
                {
                    endpointSuffix.Add($"$filter={Filter}");
                }

                if (Expand != string.Empty && Expand != null)
                    endpointSuffix.Add($"$expand={Expand}");

                if(endpointSuffix.Count > 0)
                {
                    queryBuilder.Append("?");
                    queryBuilder.Append(string.Join('&', endpointSuffix));
                }

                var query = queryBuilder.ToString();
                return query;
            }
        }
    }
}
