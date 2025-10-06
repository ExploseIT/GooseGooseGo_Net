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
    public class ent_kucoin : cIsDbNull
    {
        private readonly IConfiguration _conf;
        private readonly ILogger<mApp> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _encryptionKey;
        private readonly cApiDetails? _apiDetails;

        public ent_kucoin(
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
            var settings_api = _e_settings.doSettingsReadByName("Kucoin_API");
            if (settings_api == null)
            {
                var cs = _conf.GetSection("KUCOIN_API");
                string _apiUrl = cs.GetValue<string>("KUCOIN_API_URL") ?? "";
                string _apiKey = cs.GetValue<string>("KUCOIN_KEY") ?? "";
                string _apiSecret = cs.GetValue<string>("KUCOIN_SECRET") ?? "";

                var cApiDetails = new cApiDetails
                {
                    apidet_api_url = _apiUrl,
                    apidet_key = _apiKey,
                    apidet_secret = _apiSecret
                };
                string json = JsonSerializer.Serialize(cApiDetails);
                ret = mEncryption.EncryptString(json, _encryptionKey);
                _e_settings.doSettingsInsertByName("Kucoin_API", ret, "Encrypted Kucoin API details");
            }
            return ret;
        }

        public cApiDetails? doApiDetailsDecrypt(dbContext dbCon)
        {
            cApiDetails? ret = null;
            try
            {
                string api_index = "KUCOIN_API";
                var _e_settings = new ent_setting(dbCon, _logger);
                c_setting? settings_api = _e_settings.doSettingsReadByName(api_index);
                if (settings_api != null)
                {
                    var retJson = mEncryption.DecryptString(settings_api.setValue, _encryptionKey);
                    ret = JsonSerializer.Deserialize<cApiDetails>(retJson)!;
                }
                else
                {
                    var _r_enc = doApiDetailsEncrypt(dbCon);
                    settings_api = _e_settings.doSettingsReadByName(api_index);
                    if (settings_api != null)
                    {
                        var retJson = mEncryption.DecryptString(settings_api.setValue, _encryptionKey);
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

        public async Task<cReturnedKucoin?> doApi_TickerListAsync(dbContext _dbCon)
        {
            cApiParms p = new cApiParms
            {
                apMethod = "GET",
                apPath = "/api/v1/market/allTickers"
            };
            string retJson = await doApi_Base(p, _dbCon);

            var ret = JsonSerializer.Deserialize<cReturnedKucoin?>(retJson);

            return ret;
        }

        public async Task<string> doApi_Base(cApiParms p, dbContext _dbCon)
        {
            string environment = _apiDetails!.apidet_api_url;
            if (string.IsNullOrWhiteSpace(environment))
                throw new ArgumentException("Environment (base URL) is required.", nameof(environment));

            var baseUri = environment.TrimEnd('/');
            string url = $"{baseUri}{p.apPath}";

            var request = new HttpRequestMessage(new HttpMethod(p.apMethod), url);


            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string result = await response.Content.ReadAsStringAsync();

            return result;
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


        // --- DB-Related Methods ---

    }

    // --- Model Classes ---

    public class cKucoinAssetInfo
    {
        [Key]
        public int kaiId { get; set; }
        public DateTime kaiDT { get; set; }
    }

    public class cKucoin
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

    public class cKucoinPercentageSwingParms
    {
        [Precision(18, 5)]
        public decimal kapsMinSwing { get; set; } = 0.0M;
        public int kapsPeriodValue { get; set; } = 0;
        public string kapsPeriodUnit { get; set; } = "";
        public int kapsRowCount { get; set; } = 5;
        public int kapsPeriodOffset { get; set; } = 0;
    }

    public class cKucoinInfo
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

    public class cKucoinPercentageSwing
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

    public sealed record KucoinEnvelope<T>(
        [property: JsonPropertyName("error")] List<string> Error,
        [property: JsonPropertyName("result")] T? Result
    );


    public class cReturnedKucoin
    {
        public string code { get; set; } = "";
        public cDataKucoin? data { get; set; } = null;
    }

    public class cDataKucoin
    {
        public long time { get; set; } = 0;
        public List<cAssetKucoin>? ticker { get; set; } = null;
    }


    public class cAssetKucoin
    {
        /*
         "code": "200000",
      "data": {
        "time": 1759716477458,
        "ticker": [
          {
            "symbol": "MNDE-USDT",
            "symbolName": "MNDE-USDT",
            "buy": "0.13245",
            "bestBidSize": "280",
            "sell": "0.13302",
            "bestAskSize": "280",
            "changeRate": "-0.0346",
            "changePrice": "-0.00475",
            "open": "0.1372",
            "high": "0.14058",
            "low": "0.13045",
            "vol": "576468.4076",
            "volValue": "77894.73945238",
            "last": "0.13245",
            "lastSize": "280",
            "averagePrice": "0.13840014",
            "takerFeeRate": "0.001",
            "makerFeeRate": "0.001",
            "takerCoefficient": "3",
            "makerCoefficient": "3"
          },
         */

        public string symbol { get; set; } = "";
        public string symbolName { get; set; } = "";
        public string buy { get; set; } = "";
        public string stringbestBidSize { get; set; } = "";
        public string sell { get; set; } = "";
        public string bestAskSize { get; set; } = "";
        public string changeRate { get; set; } = "";
        public string changePrice { get; set; } = "";
        public string open { get; set; } = "";
        public string high { get; set; } = "";
        public string low { get; set; } = "";
        public string vol { get; set; } = "";
        public string volValue { get; set; } = "";
        public string last { get; set; } = "";
        public string lastSize { get; set; } = "";
        public string averagePrice { get; set; } = "";
        public string takerFeeRate { get; set; } = "";
        public string makerFeeRate { get; set; } = "";
        public string takerCoefficient { get; set; } = "";
        public string makerCoefficient { get; set; } = "";
    }

    public class cKucoinApiDetails
    {
        public string apidet_api_url { get; set; } = "";
        public string apidet_key { get; set; } = "";
        public string apidet_secret { get; set; } = "";
    }
}