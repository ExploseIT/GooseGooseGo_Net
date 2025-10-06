
using GooseGooseGo_Net.Models;
using GooseGooseGo_Net.ef;
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
    /// Singleton-safe Kraken logic. No per-instance mutable state, DI for singleton-safe services.
    /// Pass dbContext as method argument for DB operations.
    /// Uses IHttpClientFactory for HTTP calls.
    /// </summary>
    public class ent_kraken : cIsDbNull
    {
        private readonly IConfiguration _conf;
        private readonly ILogger<mApp> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _encryptionKey;
        private readonly cApiDetails? _apiDetails;

        public ent_kraken(
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
            var set_kraken_api = _e_settings.doSettingsReadByName("KRAKEN_API");
            if (set_kraken_api == null)
            {
                var cs = _conf.GetSection("KRAKEN_API");
                string _apiUrl = cs.GetValue<string>("KRAKEN_API_URL") ?? "";
                string _apiKey = cs.GetValue<string>("KRAKEN_KEY") ?? "";
                string _apiSecret = cs.GetValue<string>("KRAKEN_SECRET") ?? "";

                var cApiDetails = new cApiDetails
                {
                    apidet_api_url = _apiUrl,
                    apidet_key = _apiKey,
                    apidet_secret = _apiSecret
                };
                string json = JsonSerializer.Serialize(cApiDetails);
                ret = mEncryption.EncryptString(json, _encryptionKey);
                _e_settings.doSettingsInsertByName("KRAKEN_API", ret, "Encrypted Kraken API details");
            }
            return ret;
        }

        public cApiDetails? doApiDetailsDecrypt(dbContext dbCon)
        {
            cApiDetails? ret = null;
            try
            {
                string api_index = "KRAKEN_API";
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
                _logger.LogError(ex, "Error decrypting Kraken API details");
            }
            return ret;
        }

        public async Task<KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>?> doApi_TickerListAsync(dbContext _dbCon)
        {
            cApiParms p = new cApiParms
            {
                apMethod = "GET",
                apPath = "/0/public/Ticker",
                apDoSign = false
            };
            string retJson = await doApi_Base(p, _dbCon);

            var ret = JsonSerializer.Deserialize<KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>?>(retJson);

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
            var url = new StringBuilder(baseUri).Append(p.apPath).ToString();

            string queryStr = "";
            if (query is { Count: > 0 })
            {
                queryStr = BuildQueryString(query);
                url += "?" + queryStr;
            }

            string nonce = "";
            if (!string.IsNullOrEmpty(_apiDetails!.apidet_key))
            {
                body ??= new Dictionary<string, object>(StringComparer.Ordinal);
                if (!body.TryGetValue("nonce", out _))
                {
                    nonce = GetNonce();
                    body["nonce"] = nonce;
                }
                else
                {
                    nonce = Convert.ToString(body["nonce"]) ?? GetNonce();
                }
            }

            string bodyStr = "";
            var request = new HttpRequestMessage(new HttpMethod(p.apMethod), url);

            if (body is { Count: > 0 })
            {
                bodyStr = JsonSerializer.Serialize(body);
                request.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");
            }

            if (!string.IsNullOrEmpty(_apiDetails!.apidet_key))
            {
                request.Headers.Add("API-Key", _apiDetails!.apidet_key);
                var sig = GetSignature(_apiDetails!.apidet_key, data: queryStr + bodyStr, nonce: nonce, path: p.apPath);
                request.Headers.Add("API-Sign", sig);
            }

            var client = _httpClientFactory.CreateClient();
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            string result = await response.Content.ReadAsStringAsync();

            ret = result;
            return ret;
        }

        private string GetSignature(string privateKey, string data, string nonce, string path)
        {
            using var sha256 = SHA256.Create();
            var sha = sha256.ComputeHash(Encoding.UTF8.GetBytes(nonce + data));
            var pathBytes = Encoding.UTF8.GetBytes(path);
            var toSign = new byte[pathBytes.Length + sha.Length];
            Buffer.BlockCopy(pathBytes, 0, toSign, 0, pathBytes.Length);
            Buffer.BlockCopy(sha, 0, toSign, pathBytes.Length, sha.Length);
            return Sign(privateKey, toSign);
        }

        private string Sign(string privateKey, byte[] message)
        {
            using var hmac = new HMACSHA512(Convert.FromBase64String(privateKey));
            var digest = hmac.ComputeHash(message);
            return Convert.ToBase64String(digest);
        }

        private string GetNonce() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

        private string BuildQueryString(Dictionary<string, string> query)
            => string.Join("&", query.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        // --- DB-Related Methods ---

        public cKrakenAssetInfo? doKrakenGetNextId(dbContext dbCon)
        {
            try
            {
                SqlParameter[] lParams = { };
                string sp = "spKrakenAssetInfoNextId";
                var retSP = dbCon.lKrakenAssetInfo.FromSqlRaw(sp, lParams).AsEnumerable();
                return retSP.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in doKrakenGetNextId");
                return null;
            }
        }

        public cKraken? doKrakenUpdateById(dbContext dbCon, cKraken p)
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
                string sp = "spKrakenUpdateById @kaId,@kaIndex,@kaPair,@kaLastTrade,@kaOpen,@kaBid,@kaAsk,@kaHigh24h,@kaLow24h,@kaVolume24h,@kaRetrievedAt";
                var retSP = dbCon.lKraken.FromSqlRaw(sp, lParams).AsEnumerable();
                return retSP.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in doKrakenUpdateById");
                return null;
            }
        }

        public ApiResponse<List<cKrakenInfo>?> doKrakenInfoList(dbContext dbCon)
        {
            var ret = new ApiResponse<List<cKrakenInfo>?>();
            try
            {
                SqlParameter[] lParams = {};
                string sp = "spKrakenInfoList";
                var retSP = dbCon.lKrakenInfoList.FromSqlRaw(sp, lParams).AsEnumerable();
                ret.apiData = retSP?.ToList() ?? new List<cKrakenInfo>();
                ret.apiSuccess = true;
            }
            catch (Exception ex)
            {
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message + (ex.InnerException != null ? " : " + ex.InnerException.Message : "");
                _logger.LogError(ex, "Error in doKrakenInfoList");
            }
            return ret;
        }

        public ApiResponse<List<cKrakenPercentageSwing>?> doKrakenPercentageSwingList(dbContext dbCon, cKrakenPercentageSwingParms p)
        {
            var ret = new ApiResponse<List<cKrakenPercentageSwing>?>();
            try
            {
                SqlParameter[] lParams = {
                    new SqlParameter("@kapsMinSwing", SqlDbType.Decimal) { Value = p.kapsMinSwing },
                    new SqlParameter("@kapsPeriodValue", SqlDbType.Int) { Value = p.kapsPeriodValue },
                    new SqlParameter("@kapsPeriodUnit", SqlDbType.NVarChar) { Value = p.kapsPeriodUnit },
                    new SqlParameter("@kapsRowCount", SqlDbType.Int) { Value = p.kapsRowCount },
                    new SqlParameter("@kapsPeriodOffset", SqlDbType.Int) { Value = p.kapsPeriodOffset }
                };
                string sp = "spKrakenRollingPercentSwing @kapsMinSwing, @kapsPeriodValue, @kapsPeriodUnit, @kapsRowCount, @kapsPeriodOffset";
                var retSP = dbCon.lKrakenPercentageSwing.FromSqlRaw(sp, lParams).AsEnumerable();
                ret.apiData = retSP?.ToList() ?? new List<cKrakenPercentageSwing>();
                ret.apiSuccess = true;
            }
            catch (Exception ex)
            {
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message + (ex.InnerException != null ? " : " + ex.InnerException.Message : "");
         _logger.LogError(ex, "Error in doKrakenPercentageSwingList");
            }
            return ret;
        }
    }

    // --- Model Classes ---

    public class cAssetWatch
    {
        [Key]
        public int aswId { get; set; }
        public string aswSource { get; set; } = "";
        public string aswPair { get; set; } = "";
    }

    public class cKrakenAssetInfo
    {
        [Key]
        public int kaiId { get; set; }
        public DateTime kaiDT { get; set; }
    }

    public class cKraken
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

    public class cKrakenPercentageSwingParms
    {
        [Precision(18, 5)]
        public decimal kapsMinSwing { get; set; } = 0.0M;
        public int kapsPeriodValue { get; set; } = 0;
        public string kapsPeriodUnit { get; set; } = "";
        public int kapsRowCount { get; set; } = 5;
        public int kapsPeriodOffset { get; set; } = 0;
    }

    public class cKrakenInfo
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

    public class cKrakenPercentageSwing
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


    public sealed record KrakenEnvelope<T>(
        [property: JsonPropertyName("error")] List<string> Error,
        [property: JsonPropertyName("result")] T? Result
    );

    public sealed class KrakenTickerEntry
    {
        [JsonPropertyName("a")] public string[]? Ask { get; set; }
        [JsonPropertyName("b")] public string[]? Bid { get; set; }
        [JsonPropertyName("c")] public string[]? LastTrade { get; set; }
        [JsonPropertyName("v")] public string[]? Volume { get; set; }
        [JsonPropertyName("p")] public string[]? Vwap { get; set; }
        [JsonPropertyName("t")] public int[]? Trades { get; set; }
        [JsonPropertyName("l")] public string[]? Low { get; set; }
        [JsonPropertyName("h")] public string[]? High { get; set; }
        [JsonPropertyName("o")] public string? Open { get; set; }
    }

    public sealed record TickerRow(
        string Pair,
        decimal Last,
        decimal Open,
        decimal ChangePct,
        decimal Bid,
        decimal Ask,
        decimal High24h,
        decimal Low24h,
        string Volume24h
    );

    public sealed record OhlcCandle(
        long Time,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        decimal Vwap,
        decimal Volume,
        int Count
    );

    public class cApiDetails
    {
        public string apidet_api_url { get; set; } = "";
        public string apidet_key { get; set; } = "";
        public string apidet_secret { get; set; } = "";
    }
}