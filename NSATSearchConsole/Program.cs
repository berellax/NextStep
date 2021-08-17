using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSATSearch;

namespace NSATSearchConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string clientId = "6e3d36f8-4725-4de7-ae1a-e2b19174f84e";
            string clientSecret = "KW_3jBeLJG-ARzvvQx38447f6~U-yw~k-9";
            string authorityUrl = "https://login.microsoftonline.com/4e414336-172a-4ca9-bbfb-482b911bdc0e/oauth2/v2.0/token";
            string tenantUrl = "https://org75a0e50a.crm.dynamics.com";

            NSATSearchEngine searchEngine = new NSATSearchEngine(clientId, clientSecret, authorityUrl, tenantUrl);

            await searchEngine.GetTokenAsync();
            Console.WriteLine("Is Authenticated: {0}", searchEngine.IsAuthenticated());

            if (searchEngine.IsAuthenticated())
            {
                var result = await searchEngine.FindFacilitiesByZipProximityAsync("85251", 25);
                ProcessFacilityQueryResult(result);
            }
            Console.WriteLine();
            Console.WriteLine("Done");
            Console.ReadLine();
        }

        public static void ProcessFacilityQueryResult(JObject result)
        {

            //var jtoken = result.GetValue("accountid");
            //var acctId = result.Properties().Where(p => p.Value.Equals("Sagewood"));

            foreach (JProperty child in result.Properties().Where(p => !p.Name.StartsWith("@")))
            {
                Console.WriteLine($"{child.Name} = {child.Value}");
            }


        }
    }
}
