using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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


        public async Task<string> GetOpenPositions()
        {
            var config = LoadConfig();
            string apiKey = config.api_key;
            string apiSecret = config.api_secret;

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // For a GET request with no parameters, use an empty string for the signature body
            string signatureData = apiKey + timestamp;

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureData));
                string signature = BitConverter.ToString(hash).Replace("-", "").ToLower();

                // 2026 Unified Headers
                _client.DefaultRequestHeaders.Clear(); // Clear old headers to prevent stacking
                _client.DefaultRequestHeaders.Add("ApiKey", apiKey);
                _client.DefaultRequestHeaders.Add("Request-Time", timestamp.ToString());
                _client.DefaultRequestHeaders.Add("Signature", signature);

                // MUST be a GET request for open_positions
                var response = await _client.GetAsync("https://api.mexc.com/api/v1/private/position/open_positions");

                // You must read the content body:
                string jsonResult = await response.Content.ReadAsStringAsync();

                // Now you can print or parse it
                Console.WriteLine(jsonResult);
                return jsonResult;
            }
        }


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


        public async Task<decimal> GetFairPrice(string symbol)
        {
            var response = await _client.GetStringAsync($"https://api.mexc.com/api/v1/contract/ticker/{symbol}");
            // Parse the "fairPrice" field from the JSON
            return JsonDocument.Parse(response).RootElement.GetProperty("data").GetProperty("fairPrice").GetDecimal();
        }

        public async Task<MexcTickerResponse> GetAllTickers()
        {
            var jsonResp = await _client.GetStringAsync("https://api.mexc.com/api/v1/contract/ticker");
            var tickers = JsonSerializer.Deserialize<MexcTickerResponse>(jsonResp);
            return tickers;
        }

        // Call this once at startup and cache the results
        public async Task<decimal> GetContractSize(string symbol)
        {
            var response = await _client.GetStringAsync("https://api.mexc.com/api/v1/contract/detail");
            // Find your symbol in the list and save its "contractSize"
            return JsonDocument.Parse(response).RootElement.GetProperty("data").EnumerateArray()
                .FirstOrDefault(e => e.GetProperty("symbol").GetString() == symbol)
                .GetProperty("contractSize").GetDecimal();
        }



        public async Task<MexcContractResponse?> LoadContractDetails()
        {
            // Public endpoint - no signature or headers needed
            string json = await _client.GetStringAsync("https://api.mexc.com/api/v1/contract/detail");
            var response = JsonSerializer.Deserialize<MexcContractResponse>(json);

            return response;
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

        public class MexcTickerResponse
        {
            public bool success { get; set; }
            public List<MexcTickerDetail> data { get; set; }
        }

        public class MexcTickerDetail
        {
            public string symbol { get; set; }
            public decimal fairPrice { get; set; }
        }


        public class MexcContractResponse
        {
            public bool success { get; set; }
            public List<MexcContractDetail> data { get; set; }
        }

        public class MexcContractDetail
        {
            public string symbol { get; set; }        // e.g., "BTC_USDT"
            public decimal contractSize { get; set; } // The crucial 0.0001 or 0.1 multiplier
            public decimal minVol { get; set; }       // Smallest allowed trade size
            public int priceScale { get; set; }       // Number of decimal places for price
        }



        public class MexcPositionResponse
        {
            public bool success { get; set; }
            public int code { get; set; }
            // Change List to BindingList
            public BindingList<MexcPosition> data { get; set; }
        }



        public class MexcPositionResponse_2
        {
            public bool success { get; set; }
            public int code { get; set; }
            public List<MexcPosition> data { get; set; }
        }

        public class MexcPosition
        {
            public long positionId { get; set; }
            public string symbol { get; set; }
            public int leverage { get; set; }
            public int positionType { get; set; } // 1: Long, 2: Short
            public string positionTypeStr => positionType == 1 ? "Long" : "Short";
            public decimal closeProfitLoss { get; set; } // This is your 'Unrealized PnL'
            public decimal realised { get; set; }        // Fees/Settled PnL
            public decimal profitRatio { get; set; }      // This is your 'PnL %'
            public string pnlPercentStr => (profitRatio * 100).ToString("N2") + "%";
            //public decimal unrealizedPnl { get; set; }
            public decimal holdVol { get; set; }
            public decimal holdAvgPrice { get; set; }
            public decimal liquidatePrice { get; set; }
            public decimal accuratePnL { get; set; } 


        }
    }
}
