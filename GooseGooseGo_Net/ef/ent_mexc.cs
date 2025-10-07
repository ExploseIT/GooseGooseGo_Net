using GooseGooseGo_Net.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Threading.Tasks;
using static GooseGooseGo_Net.ef.ent_asset;

namespace GooseGooseGo_Net.ef
{
    /// <summary>
    /// Singleton-safe Mexc logic. No per-instance mutable state, DI for singleton-safe services.
    /// Pass dbContext as method argument for DB operations.
    /// Uses IHttpClientFactory for HTTP calls.
    /// </summary>
    public class ent_mexc : cIsDbNull
    {
        private readonly IConfiguration _conf;
        private readonly ILogger<mApp> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _encryptionKey;
        private readonly cApiDetails? _apiDetails;

        public ent_mexc(
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
            var set_mexc_api = _e_settings.doSettingsReadByName("Mexc_API");
            if (set_mexc_api == null)
            {
                var cs = _conf.GetSection("MEXC_API");
                string _apiUrl = cs.GetValue<string>("MEXC_API_URL") ?? "";
                string _apiKey = cs.GetValue<string>("MEXC_KEY") ?? "";
                string _apiSecret = cs.GetValue<string>("MEXC_SECRET") ?? "";

                var cMexcApiDetails = new cMexcApiDetails
                {
                    apidet_api_url = _apiUrl,
                    apidet_key = _apiKey,
                    apidet_secret = _apiSecret
                };
                string json = JsonSerializer.Serialize(cMexcApiDetails);
                ret = mEncryption.EncryptString(json, _encryptionKey);
                _e_settings.doSettingsInsertByName("MEXC_API", ret, "Encrypted Mexc API details");
            }
            return ret;
        }

        public cApiDetails? doApiDetailsDecrypt(dbContext dbCon)
        {
            cApiDetails? ret = null;
            try
            {
                string api_index = "MEXC_API";
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
                _logger.LogError(ex, "Error decrypting Mexc API details");
            }
            return ret;
        }

        public async Task<List<MexcTickerEntry>?> doApi_TickerListAsync(dbContext _dbCon)
        {
            cApiParms p = new cApiParms
            {
                apMethod = "GET",
                apPath = "/api/v3/ticker/24hr"
            };
            string retJson = await doApi_Base(p, _dbCon);

            var ret = JsonSerializer.Deserialize<List<MexcTickerEntry>>(retJson);

            return ret;
        }

        public async Task<string> doApi_Base(cApiParms p, dbContext _dbCon)
        {
            string ret = null!;
            Dictionary<string, string>? query = null;
            Dictionary<string, object>? body = null;
            
            string environment = _apiDetails!.apidet_api_url;
            if (string.IsNullOrWhiteSpace(environment))
                throw new ArgumentException("Environment (base URL) is required.", nameof(environment));

            var baseUri = environment.TrimEnd('/');
            string url = "";
            //url = new StringBuilder(baseUri).Append(p.apPath).ToString();

            string queryStr = "";
            if (query is { Count: > 0 })
            {
                queryStr = BuildQueryString(query);
                url += "?" + queryStr;
            }

            string bodyStr = "";

            var signature = GetSignature(_apiDetails!.apidet_secret, queryStr);
            url = $"{_apiDetails!.apidet_api_url}{p.apPath}?{queryStr}&signature={signature}";
            var request = new HttpRequestMessage(new HttpMethod(p.apMethod), url);

            if (body is { Count: > 0 })
            {
                bodyStr = JsonSerializer.Serialize(body);
                request.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");
            }
            request.Headers.Add("X-MEXC-APIKEY", _apiDetails!.apidet_key);

            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string result = await response.Content.ReadAsStringAsync();
            
            ret = result;
            return ret;
        }
        private string BuildQueryString(Dictionary<string, string> query)
    => string.Join("&", query.Select(kv =>
        $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        private string GetSignature(string secret, string queryString)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private string Sign(string privateKey, byte[] message)
        {
            using var hmac = new HMACSHA512(Convert.FromBase64String(privateKey));
            var digest = hmac.ComputeHash(message);
            return Convert.ToBase64String(digest);
        }

        private string GetNonce() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();


    }

    // --- Model Classes ---

    public class cMexcAssetInfo
    {
        [Key]
        public int kaiId { get; set; }
        public DateTime kaiDT { get; set; }
    }

    public class cMexc
    {
        [Key]
        public int kaId { get; set; }
        public int kaIndex { get; set; }
        public string kaPair { get; set; } = "";
        [Precision(18, 5)]
        public decimal kaLastTrade { get; set; }
        [Precision(18, 5)]
        public decimal? kaOpen { get; set; }
        [Precision(18, 5)]
        public decimal? kaBid { get; set; }
        [Precision(18, 5)]
        public decimal? kaAsk { get; set; }
        [Precision(18, 5)]
        public decimal? kaHigh24h { get; set; }
        [Precision(18, 5)]
        public decimal? kaLow24h { get; set; }
        [Precision(18, 5)]
        public decimal? kaVolume24h { get; set; }
        public DateTime kaRetrievedAt { get; set; }
        public DateTime kaRetrievedAtSanitised()
        {
            var dt = this.kaRetrievedAt.ToLocalTime();
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
        }
    }

    public class cMexcPercentageSwingParms
    {
        [Precision(18, 5)]
        public decimal kapsMinSwing { get; set; } = 0.0M;
        public int kapsPeriodValue { get; set; } = 0;
        public string kapsPeriodUnit { get; set; } = "";
        public int kapsRowCount { get; set; } = 5;
        public int kapsPeriodOffset { get; set; } = 0;
    }

    public class cMexcInfo
    {
        [Key]
        public string kaPair { get; set; } = "";
        [Precision(18, 5)]
        public decimal kaMinLastTrade { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kaMaxLastTrade { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kaHigh24h { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kaLow24h { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kaVolume24h { get; set; } = 0.0M;
        public DateTime kaRetrievedAt { get; set; } = DateTime.Now;
    }    

    public class cMexcPercentageSwing
    {
        [Key]
        public string kapsPair { get; set; } = "";
        [Precision(18, 5)]
        public decimal kapsStartTrade { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kapsEndTrade { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kapsTradeDiffPercent { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kapsTradeDiff { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kapsTradeDiffAbs { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kapsStartVolume { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal kapsEndVolume { get; set; } = 0.0M;
        public DateTime kapsStartRetrievedAt { get; set; }
        public DateTime kapsEndRetrievedAt { get; set; }
    }

    public sealed record MexcEnvelope<T>(
        [property: JsonPropertyName("error")] List<string> Error,
        [property: JsonPropertyName("result")] T? Result
    );

    public class MexcTickerEntry
    {
        /*"symbol": "METALUSDT",
        "priceChange": "-0.013",
        "priceChangePercent": "-0.0319",
        "prevClosePrice": "0.40666",
        "lastPrice": "0.39366",
        "bidPrice": "0.39337",
        "bidQty": "3.24",
        "askPrice": "0.3941",
        "askQty": "121.34",
        "openPrice": "0.40666",
        "highPrice": "0.41618",
        "lowPrice": "0.39019",
        "volume": "89050.54",
        "quoteVolume": "35717.78507",
        "openTime": 1759627383256,
        "closeTime": 1759627447124,
        "count": null*/
        public string symbol { get; set; } = "";
        public string priceChange { get; set; } = "";
        public string priceChangePercent { get; set; } = "";
        public string prevClosePrice { get; set; } = "";
        public string lastPrice { get; set; } = "";
        public string bidPrice { get; set; } = "";
        public string bidQty { get; set; } = "";
        public string askPrice { get; set; } = "";
        public string askQty { get; set; } = "";
        public string openPrice { get; set; } = "";
        public string highPrice { get; set; } = "";
        public string volume { get; set; } = "";
        public string quoteVolume { get; set; } = "";
        public Int64 openTime { get; set; } = 0;
        public Int64 closeTime { get; set; } = 0;
    }

    public class cMexcApiDetails
    {
        public string apidet_api_url { get; set; } = "";
        public string apidet_key { get; set; } = "";
        public string apidet_secret { get; set; } = "";
    }
}