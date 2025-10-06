using GooseGooseGo_Net.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static GooseGooseGo_Net.ef.ent_asset;

namespace GooseGooseGo_Net.ef
{
    /// <summary>
    /// Singleton-safe Kraken logic. No per-instance mutable state, DI for singleton-safe services.
    /// Pass dbContext as method argument for DB operations.
    /// Uses IHttpClientFactory for HTTP calls.
    /// </summary>
    public class ent_cryptocom : cIsDbNull
    {
        private readonly IConfiguration _conf;
        private readonly ILogger<mApp> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _encryptionKey;
        private readonly cApiDetails? _apiDetails;
        private static readonly HttpClient Http = new HttpClient();

        public ent_cryptocom(
            IConfiguration conf,
            ILogger<mApp> logger,
            IHttpClientFactory httpClientFactory,
            dbContext _dbCon
        )
        {
            _conf = conf;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _encryptionKey = conf.GetSection("Encryption").GetValue<string>("EncryptionKey") ?? "";
            _apiDetails = doApiDetailsDecrypt(_dbCon!)!;
        }

        public string doApiDetailsEncrypt(dbContext dbCon)
        {
            string ret = "";
            var _e_settings = new ent_setting(dbCon, _logger);
            var settings_api = _e_settings.doSettingsReadByName("CRYPTOCOM_API");
            if (settings_api == null)
            {
                var cs = _conf.GetSection("CRYPTOCOM_API");
                string _apiUrl = cs.GetValue<string>("CRYPTOCOM_API_URL") ?? "";
                string _apiKey = cs.GetValue<string>("CRYPTOCOM_KEY") ?? "";
                string _apiSecret = cs.GetValue<string>("CRYPTOCOM_SECRET") ?? "";

                var cApiDetails = new cApiDetails
                {
                    apidet_api_url = _apiUrl,
                    apidet_key = _apiKey,
                    apidet_secret = _apiSecret
                };
                string json = JsonSerializer.Serialize(cApiDetails);
                ret = mEncryption.EncryptString(json, _encryptionKey);
                _e_settings.doSettingsInsertByName("CRYPTOCOM_API", ret, "Encrypted Crypto.com API details");
            }
            return ret;
        }

        public cApiDetails? doApiDetailsDecrypt(dbContext dbCon)
        {
            cApiDetails? ret = null;
            try
            {
                string api_index = "CRYPTOCOM_API";
                var _e_settings = new ent_setting(dbCon, _logger);
                c_setting? set_mexc_api = _e_settings.doSettingsReadByName(api_index);
                if (set_mexc_api != null)
                {
                    var retJson = mEncryption.DecryptString(set_mexc_api.setValue, _encryptionKey);
                    ret = JsonSerializer.Deserialize<cApiDetails>(retJson)!;
                }
                else
                {
                    var _r_enc = doApiDetailsEncrypt(dbCon);
                    set_mexc_api = _e_settings.doSettingsReadByName(api_index);
                    if (set_mexc_api != null)
                    {
                        var retJson = mEncryption.DecryptString(set_mexc_api.setValue, _encryptionKey);
                        ret = JsonSerializer.Deserialize<cApiDetails>(retJson)!;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting Crypto.com API details");
            }
            return ret;
        }

        private string GetNonce() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        private string BuildQueryString(Dictionary<string, string> query)
            => string.Join("&", query.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        public async Task<cReturnedCryptoCom?> doApi_TickerListAsync(dbContext _dbCon)
        {
            cApiParms p = new cApiParms
            {
                apMethod = "GET",
                apPath = "/public/get-tickers",
                apDoSign = false
            };
            string retJson = await doApi_Base(p, _dbCon);

            var ret = JsonSerializer.Deserialize<cReturnedCryptoCom?>(retJson);

            return ret;
        }

        public async Task<string> doApi_Base(cApiParms p, dbContext _dbCon)
        {
            string ret = null!;
            Dictionary<string, string>? query = null;
            Dictionary<string, object>? body = null;
            Dictionary<string, object>? @params = null;

            string environment = _apiDetails!.apidet_api_url;
            if (string.IsNullOrWhiteSpace(environment))
                throw new ArgumentException("Environment (base URL) is required.", nameof(environment));

            var baseUri = environment.TrimEnd('/');
            var url = new StringBuilder(baseUri).Append("/v1").Append(p.apPath).ToString();
            
            HttpRequestMessage? request = null;

            if (!p.apDoSign)
            {
                request = new HttpRequestMessage(new HttpMethod(p.apMethod), url);
            }
            else
            {
                //This section needs fixing for Crypto.com
                // ----- PRIVATE: POST JSON with signature -----
                var id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                var dictBody = new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["method"] = p.apPath,
                    ["params"] = @params ?? new Dictionary<string, object>()
                };

                if (string.IsNullOrEmpty(_apiDetails.apidet_key) || string.IsNullOrEmpty(_apiDetails.apidet_secret))
                    throw new InvalidOperationException("API key/secret required for private call.");

                dictBody["api_key"] = _apiDetails.apidet_key;
                dictBody["nonce"] = nonce;

                // sig = HMAC_SHA256_HEX(method + id + api_key + paramStr + nonce)
                var paramStr = BuildParamString((Dictionary<string, object>)dictBody["params"]);
                var payload = $"{p.apMethod}{id}{_apiDetails.apidet_key}{paramStr}{nonce}";
                dictBody["sig"] = SignHex(_apiDetails.apidet_secret, payload);
                

                var json = JsonSerializer.Serialize(dictBody, JsonOpts);
                request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string result = await response.Content.ReadAsStringAsync();

            ret = result;
            return ret;
        }


        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static string SignHex(string secret, string message)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var sb = new StringBuilder(digest.Length * 2);
            foreach (var b in digest) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
        private static string GetSignature(string secret, string message)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            var sb = new StringBuilder(digest.Length * 2);
            foreach (var b in digest) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
        /// <summary>
        /// v1 requires alphabetical key order; value concatenation.
        /// For list/array values, concatenate each item with no separators.
        /// </summary>
        private static string BuildParamString(Dictionary<string, object> dict)
        {
            if (dict == null || dict.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            foreach (var kv in dict.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                sb.Append(kv.Key);
                switch (kv.Value)
                {
                    case null:
                        break;
                    case IEnumerable<object> list:
                        foreach (var item in list)
                            sb.Append(Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture));
                        break;
                    default:
                        sb.Append(Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture));
                        break;
                }
            }
            return sb.ToString();
        }


    }

    public class cReturnedCryptoCom
    {
        public int id { get; set; } = 0;
        public string method { get; set; } = "";
        public int code { get; set; } = 0;
        public cDataCryptoCom? result { get; set; } = null;
    }


    public class cDataCryptoCom
    {
        public List<cAssetCryptoCom>? data { get; set; } = null;
    }

    public class cAssetCryptoCom
    {
        /*
       "i" : "ACH_USD",
      "h" : "0.019413",
      "l" : "0.018557",
      "a" : "0.018587",
      "v" : "757750",
      "vv" : "14257.19",
      "c" : "-0.0135",
      "b" : "0.018562",
      "k" : "0.018658",
      "oi" : "0",
      "t" : 1759709112068
        */

        public string @i { get; set; } = "";
        public string @h { get; set; } = "";
        public string @l { get; set; } = "";
        public string @a { get; set; } = "";
        public string @v { get; set; } = "";
        public string @vv { get; set; } = "";
        public string @c { get; set; } = "";
        public string @b { get; set; } = "";
        public string @k { get; set; } = "";
        public string @oi { get; set; } = "";
        public Int64 @t { get; set; } = 0;
    }



}





