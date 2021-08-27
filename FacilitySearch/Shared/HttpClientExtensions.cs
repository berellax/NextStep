using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ProviderSearch.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ProviderSearch.Shared
{
    internal static class HttpClientExtensions
    {
        /// <summary>
        /// Retrieves data using an oData query
        /// </summary>
        /// <typeparam name="T">Type of an item retrieved by a request. Response will be deserialized into a <see cref="List{T}"/></typeparam>
        /// <param name="client">An <see cref="HttpClient"/> used to make the OData request.</param>
        /// <param name="request">An <see cref="ODataRequest"/> that contains parameters for the OData query.</param>
        /// <returns><see cref="ODataResponse{T}"/></returns>
        internal static async Task<ODataResponse<T>> RetrieveOdata<T>(
            this HttpClient client,
            ODataRequest request)
        {
            var response = await client.GetAsync(request.QueryString);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Get OData request failed with reason: {response.ReasonPhrase}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            return System.Text.Json.JsonSerializer.Deserialize<ODataResponse<T>>(responseContent);
        }

        internal static async Task<T> RetrieveOdataSingle<T>(
            this HttpClient client,
            ODataRequest request)
        {
            HttpResponseMessage response; 

            try
            {
                response = await client.GetAsync(request.QueryString);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(response.ReasonPhrase);
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                return System.Text.Json.JsonSerializer.Deserialize<T>(responseContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Get OData request failed with reason: {ex.Message}");
            }
        }

        internal static async Task<JObject> RetrieveOdataDynamic<T>(
            this HttpClient client,
            ODataRequest request
            )
        {
            var response = await client.GetAsync(request.QueryString);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Get OData request failed with reason: {response.ReasonPhrase}");
            }

            string responseContent = await response.Content.ReadAsStringAsync();

            var result = JsonConvert.DeserializeObject(responseContent) as JObject;

            return result as JObject;
        }

        /// <summary>
        /// Sends a patch request as an asynchronous operation to the specified uri with the given value serialized as JSON.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="client"></param>
        /// <param name="requestUri"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, string requestUri, T value)
        {
            var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
            return client.PatchAsync(requestUri, content);
        }
    }
}
