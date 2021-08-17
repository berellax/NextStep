using System;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(ProviderSearch.Startup))]

namespace ProviderSearch
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddHttpClient("base", client =>
            {
                var environmentUri = Environment.GetEnvironmentVariable("EnvironmentUrl");
                var apiVersion = Environment.GetEnvironmentVariable("ApiVersion");
                
                client.BaseAddress = new Uri($"{environmentUri}/api/data/v9.1/");
                client.Timeout = TimeSpan.FromMinutes(2);
            });
        }
    }
}
