using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Threading.Tasks;

namespace ProviderSearch.Models
{
    public class Config
    {
        public static string EnvironmentUrl { get; set; }
        public static string ClientId { get; set; }
        public static string ClientSecret { get; set; }
        public static string AuthorityUrl { get; set; }
        public static string GeoCodeApi { get; set; }

        public static string GeoCodeApiKey { get; set; }
        public Config(System.Collections.IDictionary keys)
        {
            EnvironmentUrl = (string)keys["EnvironmentUrl"];
            ClientId = (string)keys["ClientId"];
            ClientSecret = (string)keys["ClientSecret"];
            AuthorityUrl = (string)keys["AuthorityUrl"];
            GeoCodeApi = (string)keys["GeoCodeApi"];
            GeoCodeApiKey = (string)keys["GeoCodeApiKey"];
        }

    }
}
