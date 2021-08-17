using Microsoft.Identity.Client;
using System.Threading.Tasks;
using ProviderSearch.Models;
using System;

namespace ProviderSearch.Shared
{
    internal static class Authentication
    {

        /// <summary>
        /// Authenticates Dataverse Environment with Active Directory via App Registration
        /// </summary>
        /// <param name="config"></param>
        /// <returns>Authentication token</returns>
        internal static async Task<AuthenticationResult> AuthenticateClient(Config config)
        {
            IConfidentialClientApplication app = ConfidentialClientApplicationBuilder.Create(Config.ClientId)
                                                    .WithClientSecret(Config.ClientSecret)
                                                    .WithAuthority(new Uri(Config.AuthorityUrl))
                                                    .Build();

            string[] scopes = new string[] { $"{Config.EnvironmentUrl}/.default" };

            AuthenticationResult authResult = null;

            authResult = await app.AcquireTokenForClient(scopes).ExecuteAsync();

            return authResult;
        }
    }
}
