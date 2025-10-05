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

        public ent_mexc(
            IConfiguration conf,
            ILogger<mApp> logger,
            IHttpClientFactory httpClientFactory)
        {
            _conf = conf;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _encryptionKey = conf.GetSection("Encryption").GetValue<string>("EncryptionKey") ?? "";
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

        public async Task<List<MexcTickerEntry>?> doApi_TickerListAsync(cApiDetails apiDetails)
        {
            cMexcAPIParms p = new cMexcAPIParms
            {
                apMethod = "GET",
                apPath = "/api/v3/ticker/24hr"
            };
            string retJson = await doApi_Base(p, apiDetails);

            var ret = JsonSerializer.Deserialize<List<MexcTickerEntry>>(retJson);

            return ret;
        }

        public async Task<string> doApi_Base(cMexcAPIParms p, cApiDetails apiDetails)
        {
            string ret = null!;
            Dictionary<string, string>? query = null;
            Dictionary<string, object>? body = null;
            string environment = apiDetails.apidet_api_url;
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

            var signature = GetSignature(apiDetails.apidet_secret, queryStr);
            url = $"{apiDetails.apidet_api_url}{p.apPath}?{queryStr}&signature={signature}";
            var request = new HttpRequestMessage(new HttpMethod(p.apMethod), url);

            if (body is { Count: > 0 })
            {
                bodyStr = JsonSerializer.Serialize(body);
                request.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");
            }
            request.Headers.Add("X-MEXC-APIKEY", apiDetails.apidet_key);

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


        // --- DB-Related Methods ---

        public cMexcAssetInfo? doMexcGetNextId(dbContext dbCon)
        {
            try
            {
                SqlParameter[] lParams = { };
                string sp = "spMexcAssetInfoNextId";
                var retSP = dbCon.lMexcAssetInfo.FromSqlRaw(sp, lParams).AsEnumerable();
                return retSP.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in doMexcGetNextId");
                return null;
            }
        }

        public cMexc? doMexcUpdateById(dbContext dbCon, cMexc p)
        {
            try
            {
                SqlParameter[] lParams = {
                    new SqlParameter("@kaId", SqlDbType.Int) { Value = p.kaId },
                    new SqlParameter("@kaIndex", SqlDbType.Int) { Value = p.kaIndex },
                    new SqlParameter("@kaPair", SqlDbType.NVarChar) { Value = p.kaPair },
                    new SqlParameter("@kaLastTrade", SqlDbType.Decimal) { Value = p.kaLastTrade },
                    new SqlParameter("@kaOpen", SqlDbType.Decimal) { Value = p.kaOpen ?? (object)DBNull.Value },
                    new SqlParameter("@kaBid", SqlDbType.Decimal) { Value = p.kaBid ?? (object)DBNull.Value },
                    new SqlParameter("@kaAsk", SqlDbType.Decimal) { Value = p.kaAsk ?? (object)DBNull.Value },
                    new SqlParameter("@kaHigh24h", SqlDbType.Decimal) { Value = p.kaHigh24h ?? (object)DBNull.Value },
                    new SqlParameter("@kaLow24h", SqlDbType.Decimal) { Value = p.kaLow24h ?? (object)DBNull.Value },
                    new SqlParameter("@kaVolume24h", SqlDbType.Decimal) { Value = IsDbNull(p.kaVolume24h) },
                    new SqlParameter("@kaRetrievedAt", SqlDbType.DateTime) { Value = p.kaRetrievedAtSanitised() }
                };
                string sp = "spMexcUpdateById @kaId,@kaIndex,@kaPair,@kaLastTrade,@kaOpen,@kaBid,@kaAsk,@kaHigh24h,@kaLow24h,@kaVolume24h,@kaRetrievedAt";
                var retSP = dbCon.lMexc.FromSqlRaw(sp, lParams).AsEnumerable();
                return retSP.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in doMexcUpdateById");
                return null;
            }
        }

        public ApiResponse<List<cMexcInfo>?> doMexcInfoList(dbContext dbCon)
        {
            var ret = new ApiResponse<List<cMexcInfo>?>();
            try
            {
                SqlParameter[] lParams = {};
                string sp = "spMexcInfoList";
                var retSP = dbCon.lMexcInfoList.FromSqlRaw(sp, lParams).AsEnumerable();
                ret.apiData = retSP?.ToList() ?? new List<cMexcInfo>();
                ret.apiSuccess = true;
            }
            catch (Exception ex)
            {
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message + (ex.InnerException != null ? " : " + ex.InnerException.Message : "");
                _logger.LogError(ex, "Error in doMexcInfoList");
            }
            return ret;
        }

        public ApiResponse<List<cMexcPercentageSwing>?> doMexcPercentageSwingList(dbContext dbCon, cMexcPercentageSwingParms p)
        {
            var ret = new ApiResponse<List<cMexcPercentageSwing>?>();
            try
            {
                SqlParameter[] lParams = {
                    new SqlParameter("@kapsMinSwing", SqlDbType.Decimal) { Value = p.kapsMinSwing },
                    new SqlParameter("@kapsPeriodValue", SqlDbType.Int) { Value = p.kapsPeriodValue },
                    new SqlParameter("@kapsPeriodUnit", SqlDbType.NVarChar) { Value = p.kapsPeriodUnit },
                    new SqlParameter("@kapsRowCount", SqlDbType.Int) { Value = p.kapsRowCount },
                    new SqlParameter("@kapsPeriodOffset", SqlDbType.Int) { Value = p.kapsPeriodOffset }
                };
                string sp = "spMexcRollingPercentSwing @kapsMinSwing, @kapsPeriodValue, @kapsPeriodUnit, @kapsRowCount, @kapsPeriodOffset";
                var retSP = dbCon.lMexcPercentageSwing.FromSqlRaw(sp, lParams).AsEnumerable();
                ret.apiData = retSP?.ToList() ?? new List<cMexcPercentageSwing>();
                ret.apiSuccess = true;
            }
            catch (Exception ex)
            {
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message + (ex.InnerException != null ? " : " + ex.InnerException.Message : "");
                _logger.LogError(ex, "Error in doMexcPercentageSwingList");
            }
            return ret;
        }
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

    public class cMexcAPIParms
    {
        public string apMethod { get; set; } = "GET";
        public string apPath { get; set; } = "";
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