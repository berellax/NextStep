using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProviderSearch.Shared
{
    internal static class HttpRequestExtensions
    {
        public static async Task<T> DeserializeRequestBodyAsync<T>(this HttpRequest req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            return JsonSerializer.Deserialize<T>(requestBody);
        }
    }
}
