using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebApp.Services;

namespace WebApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            // gateway api
            var baseUrl = builder.Configuration["GatewayBaseUrl"];
            builder.Services.AddScoped(sp =>
                new HttpClient { BaseAddress = new Uri(baseUrl) });

            // authentication service
            builder.Services.AddScoped<AuthService>();

            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");
            

            await builder.Build().RunAsync();
        }
    }
}
