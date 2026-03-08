using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GGG_WinFormer
{
    public sealed class GGG_Main
    {
        // 1. Private thread-safe lazy initializer
        private static readonly Lazy<GGG_Main> _instance =
            new Lazy<GGG_Main>(() => new GGG_Main());

        // 2. Public access point
        public static GGG_Main Instance => _instance.Value;

        // 3. Private constructor so no one else can 'new' it
        private GGG_Main() { }
        // Create one client for the life of the app
        private readonly HttpClient _client = new HttpClient();

        public async Task<(string Response, long Latency)> GetServerTimeWithLatency()
        {
            // Use the Shared HttpClient from your Singleton
            var sw = Stopwatch.StartNew(); // Start the timer

            try
            {
                var response = await _client.GetStringAsync("https://api.mexc.com/api/v1/contract/ping");
                sw.Stop(); // Stop exactly when the response arrives

                return (response, sw.ElapsedMilliseconds);
            }
            catch
            {
                sw.Stop();
                throw;
            }
        }

        public async Task<string> GetServerTime()
        {
            return await _client.GetStringAsync("https://api.mexc.com/api/v1/contract/ping");
        }

        public async Task<string> GetFuturesContractInfo()
        {
            return await _client.GetStringAsync("https://api.mexc.com/api/v1/contract/detail");
        }

        public obj_apiconfig LoadConfig()
        {
            // Detect environment (Development or Production)
            string? env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            bool isDevelopment = env.Equals("Development", StringComparison.OrdinalIgnoreCase);

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                // Load base settings (non-sensitive defaults)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                // Load environment-specific settings (e.g., appsettings.Development.json)
                .AddJsonFile($"appsettings.{env}.json", optional: true);

            // ONLY load User Secrets if we are local (Development)
            if (isDevelopment)
            {
                builder.AddUserSecrets<GGG_Winformer>();
            }

            // Always allow Environment Variables to override everything (for Production)
            builder.AddEnvironmentVariables();

            var config = builder.Build();
            var ret = new obj_apiconfig();

            // Map your keys from either User Secrets or Environment Variables
            ret.api_key = config["MEXC_API:MEXC_KEY"] ?? "";
            ret.api_secret = config["MEXC_API:MEXC_SECRET"] ?? "";

            return ret;
        }

        public class obj_apiconfig
        {
            public string api_key { get; set; } = string.Empty;
            public string api_secret { get; set; } = string.Empty;
        }
    }
}
