using System.Text.Json;
using System.Web;

namespace ProviderSearch.Models
{
    /// <summary>
    /// Provides <see cref="HttpUtility.UrlEncode(string)"/> as "_encode" and 
    /// <see cref="JsonSerializer.Serialize{TValue}(TValue, JsonSerializerOptions)"/> as ToString override.
    /// </summary>
    internal abstract class ModelProcessing
    {
        internal string _encode(object val) => HttpUtility.UrlEncode((string)val);

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
