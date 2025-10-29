

using GooseGooseGo_Net.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Data;
using static GooseGooseGo_Net.ef.ent_kraken;

namespace GooseGooseGo_Net.ef
{
    public class ent_asset : cIsDbNull
    {


        private readonly IConfiguration _conf;
        private readonly ILogger<mApp> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _encryptionKey;
        private readonly cApiDetails? _apiDetails;

        public ent_asset(
            IConfiguration conf,
            ILogger<mApp> logger,
            IHttpClientFactory httpClientFactory
            )
        {
            _conf = conf;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }



        // --- DB-Related Methods ---

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


        public cAssetAssetInfo? doAssetGetNextId(dbContext dbCon)
        {
            try
            {
                SqlParameter[] lParams = { };
                string sp = "spAssetInfoNextId";
                var retSP = dbCon.lAssetAssetInfoList.FromSqlRaw(sp, lParams).AsEnumerable();
                return retSP.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in doAssetGetNextId");
                return null;
            }
        }

        public cAsset? doAssetUpdateById(dbContext dbCon, cAsset p)
        {
            try
            {
                SqlParameter[] lParams = {
                    new SqlParameter("@assId", SqlDbType.BigInt) { Value = p.assId },
                    new SqlParameter("@assIndex", SqlDbType.Int) { Value = p.assIndex },
                    new SqlParameter("@assExchange", SqlDbType.NVarChar) { Value = p.assExchange },
                    new SqlParameter("@assPair", SqlDbType.NVarChar) { Value = p.assPair },
                    new SqlParameter("@assLastTrade", SqlDbType.Decimal) { Value = p.assLastTrade },
                    new SqlParameter("@assOpen", SqlDbType.Decimal) { Value = p.assOpen ?? (object)DBNull.Value },
                    new SqlParameter("@assBid", SqlDbType.Decimal) { Value = p.assBid ?? (object)DBNull.Value },
                    new SqlParameter("@assAsk", SqlDbType.Decimal) { Value = p.assAsk ?? (object)DBNull.Value },
                    new SqlParameter("@assHigh24h", SqlDbType.Decimal) { Value = p.assHigh24h ?? (object)DBNull.Value },
                    new SqlParameter("@assLow24h", SqlDbType.Decimal) { Value = p.assLow24h ?? (object)DBNull.Value },
                    new SqlParameter("@assVolume24h", SqlDbType.Decimal) { Value = p.assVolume24h.HasValue ? p.assVolume24h.Value : DBNull.Value},
                    new SqlParameter("@assRetrievedAt", SqlDbType.DateTime) { Value = p.assRetrievedAtSanitised() }
                };
                string sp = "spAssetUpdateById @assId,@assIndex,@assExchange,@assPair,@assLastTrade,@assOpen,@assBid,@assAsk,@assHigh24h,@assLow24h,@assVolume24h,@assRetrievedAt";
                var retSP = dbCon.lAssetList.FromSqlRaw(sp, lParams).AsEnumerable();
                return retSP.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in doAssetUpdateById");
                return null;
            }
        }

        public ApiResponse<List<cAssetInfo>?> doAssetInfoList(dbContext dbCon)
        {
            var ret = new ApiResponse<List<cAssetInfo>?>();
            try
            {
                SqlParameter[] lParams = { };
                string sp = "spAssetInfoList";
                var retSP = dbCon.lAssetInfoList.FromSqlRaw(sp, lParams).AsEnumerable();
                ret.apiData = retSP?.ToList() ?? new List<cAssetInfo>();
                ret.apiSuccess = true;
            }
            catch (Exception ex)
            {
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message + (ex.InnerException != null ? " : " + ex.InnerException.Message : "");
                _logger.LogError(ex, "Error in doAssetInfoList");
            }
            return ret;
        }

        public ApiResponse<List<cAssetPercentageSwing>?> doAssetPercentageSwingList(dbContext dbCon, cAssetPercentageSwingParms p)
        {
            var ret = new ApiResponse<List<cAssetPercentageSwing>?>();
            try
            {
                SqlParameter[] lParams = {
                    new SqlParameter("@aspspUpSwing", SqlDbType.Bit) { Value = p.aspspUpSwing },
                    new SqlParameter("@aspspMinSwing", SqlDbType.Decimal) { Value = p.aspspMinSwing },
                    new SqlParameter("@aspspPeriodValue", SqlDbType.Int) { Value = p.aspspPeriodValue },
                    new SqlParameter("@aspspPeriodUnit", SqlDbType.NVarChar) { Value = p.aspspPeriodUnit },
                    new SqlParameter("@aspspRowCount", SqlDbType.Int) { Value = p.aspspRowCount },
                    new SqlParameter("@aspspPeriodOffset", SqlDbType.Int) { Value = p.aspspPeriodOffset }
                };
                string sp = "spAssetRollingPercentSwing @aspspUpSwing, @aspspMinSwing, @aspspPeriodValue, @aspspPeriodUnit, @aspspRowCount, @aspspPeriodOffset";
                var retSP = dbCon.lAssetPercentageSwing.FromSqlRaw(sp, lParams).AsEnumerable();
                ret.apiData = retSP?.ToList() ?? new List<cAssetPercentageSwing>();
                ret.apiSuccess = true;
            }
            catch (Exception ex)
            {
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message + (ex.InnerException != null ? " : " + ex.InnerException.Message : "");
                _logger.LogError(ex, "Error in doAssetPercentageSwingList");
            }
            return ret;
        }

        public ApiResponse<cAssetProfit?> doAssetProfitRead(dbContext dbCon, cAssetProfit p)
        {
            var ret = new ApiResponse<cAssetProfit?>();
            try
            {
                SqlParameter[] lParams = {
                    new SqlParameter("@assprAsset", SqlDbType.NVarChar) { Value = p.assprAsset },
                    new SqlParameter("@assprExchangeId", SqlDbType.NVarChar) { Value = p.assprExchangeId },
                    new SqlParameter("@assprOrderId", SqlDbType.NVarChar) { Value = p.assprOrderId }
                };
                string sp = "spAssetProfitRead @assprAsset, @assprExchangeId, @assprOrderId";
                var retSP = dbCon.lAssetProfit.FromSqlRaw(sp, lParams).AsEnumerable();
                ret.apiData = retSP?.FirstOrDefault<cAssetProfit>();
                ret.apiSuccess = true;
            }
            catch (Exception ex)
            {
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message + (ex.InnerException != null ? " : " + ex.InnerException.Message : "");
                _logger.LogError(ex, "Error in doAssetProfitRead(");
            }
            return ret;
        }
        public ApiResponse<cAssetProfit?> doAssetProfitUpdate(dbContext dbCon, cAssetProfit p)
        {
            var ret = new ApiResponse<cAssetProfit?>();
            try
            {
                SqlParameter[] lParams = {
                    new SqlParameter("@assprAsset", SqlDbType.NVarChar) { Value = p.assprAsset },
                    new SqlParameter("@assprExchangeId", SqlDbType.NVarChar) { Value = p.assprExchangeId },
                    new SqlParameter("@assprPrice", SqlDbType.Decimal) { Value = p.assprPrice },
                    new SqlParameter("@assprOrderId", SqlDbType.NVarChar) { Value = p.assprOrderId }
                };
                string sp = "spAssetProfitUpdate @assprAsset, @assprExchangeId, @assprPrice, @assprOrderId";
                var retSP = dbCon.lAssetProfit.FromSqlRaw(sp, lParams).AsEnumerable();
                ret.apiData = retSP?.FirstOrDefault<cAssetProfit>();
                ret.apiSuccess = true;
            }
            catch (Exception ex)
            {
                ret.apiSuccess = false;
                ret.apiMessage = ex.Message + (ex.InnerException != null ? " : " + ex.InnerException.Message : "");
                _logger.LogError(ex, "Error in doAssetProfitRead(");
            }
            return ret;
        }
    }

    public class cAsset
    {
        [Key]
        public Int64 assId { get; set; }
        public int assIndex { get; set; }
        public string assExchange { get; set; } = "";
        public string assPair { get; set; } = "";
        [Precision(18, 5)]
        public decimal assLastTrade { get; set; }
        [Precision(18, 5)]
        public decimal? assOpen { get; set; }
        [Precision(18, 5)]
        public decimal? assBid { get; set; }
        [Precision(18, 5)]
        public decimal? assAsk { get; set; }
        [Precision(18, 5)]
        public decimal? assHigh24h { get; set; }
        [Precision(18, 5)]
        public decimal? assLow24h { get; set; }
        [Precision(18, 5)]
        public decimal? assVolume24h { get; set; }
        public DateTime assRetrievedAt { get; set; }
        public DateTime assRetrievedAtSanitised()
        {
            var dt = this.assRetrievedAt.ToLocalTime();
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
        }
    }

    public class cApiParms
    {
        public string apMethod { get; set; } = "GET";
        public string apPath { get; set; } = "";
        public Dictionary<string, string> apQuery { get; set; } = new();
        public string apBody { get; set; } = "";
        public bool apDoSign { get; set; } = false;
    }

    public class cAssetAssetInfo
    {
        [Key]
        public int asiId { get; set; }
        public DateTime asiDT { get; set; } = DateTime.Now;
    }
    public class cAssetInfo
    {
        [Key]
        public string assPair { get; set; } = "";
        [Precision(18, 5)]
        public decimal assMinLastTrade { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal assMaxLastTrade { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal assHigh24h { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal assLow24h { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal assVolume24h { get; set; } = 0.0M;
        public DateTime assRetrievedAt { get; set; } = DateTime.Now;
    }

    public class cAssetPercentageSwing
    {
        [Key]
        public Int64 asspsId { get; set; } = 0;
        public string asspsExchange { get; set; } = "";
        public string asspsExchangeFullName { get; set; } = "";
        public string asspsPair { get; set; } = "";
        [Precision(18, 5)]
        public decimal asspsStartTrade { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal asspsEndTrade { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal asspsTradeDiffPercent { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal asspsTradeDiff { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal asspsTradeDiffAbs { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal asspsStartVolume { get; set; } = 0.0M;
        [Precision(18, 5)]
        public decimal asspsEndVolume { get; set; } = 0.0M;
        public DateTime asspsStartRetrievedAt { get; set; }
        public DateTime asspsEndRetrievedAt { get; set; }
    }
    public class cAssetPercentageSwingParms
    {
        public bool aspspUpSwing { get; set; } = false;
        [Precision(18, 5)]
        public decimal aspspMinSwing { get; set; } = 0.0M;
        public int aspspPeriodValue { get; set; } = 0;
        public string aspspPeriodUnit { get; set; } = "";
        public int aspspRowCount { get; set; } = 5;
        public int aspspPeriodOffset { get; set; } = 0;
    }
    public class cApiDetails
    {
        public string apidet_api_url { get; set; } = "";
        public string apidet_key { get; set; } = "";
        public string apidet_secret { get; set; } = "";
    }

    public class cSignalInfoPost
    {
        public string? sip_symbol { get; set; } = "";
        public string? sip_strategy { get; set; } = "";
        public string? sip_action { get; set; } = "";
        public string? sip_price { get; set; } = "";
        public string? sip_whenStr { get; set; } = "";
    }

    public class cAssetProfit
    {
        [Key]
        public int assprId { get; set; } = 0;
        public string assprAsset { get; set; } = "";
        public string assprExchangeId { get; set; } = "";
        public string assprOrderId { get; set; } = "";
        [Precision(18, 5)]
        public decimal assprPrice { get; set; } = 0.0M;
        public DateTime assprDT { get; set; } = DateTime.Now;
    }
}
