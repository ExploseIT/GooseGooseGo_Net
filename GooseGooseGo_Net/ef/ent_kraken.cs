

using GooseGooseGo_Net.ef;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace GooseGooseGo_Net.ef
{
    public class ent_kraken
    {

        private dbContext? dbCon { get; } = null;
        Exception? exc = null!;

        public ent_kraken()
        {
        }

        public ent_kraken(dbContext dbCon)
        {
            this.dbCon = dbCon;
        }

        public cKrakenAssetInfo doKrakenGetNextId()
        {
            cKrakenAssetInfo ret = new cKrakenAssetInfo();
            try
            {
                SqlParameter[] lParams = { };
                string sp = "spKrakenAssetInfoNextId";

                var retSP = this.dbCon?.lKrakenAssetInfo.FromSqlRaw(sp, lParams).AsEnumerable();

                ret = retSP?.FirstOrDefault()!;
            }
            catch (Exception ex)
            {
                exc = ex;
            }

            return ret;
        }

        public cKraken? doKrakenUpdateById(cKraken p)
        {
            cKraken? ret = null;

            try
            {
                SqlParameter[] lParams = {
                new SqlParameter("@kaId", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaId)
                , new SqlParameter("@kaIndex", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaIndex)
                , new SqlParameter("@kaPair", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaPair)
                , new SqlParameter("@kaLastTrade", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaLastTrade)
                , new SqlParameter("@kaOpen", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaOpen)
                , new SqlParameter("@kaBid", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaBid)
                , new SqlParameter("@kaAsk", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaAsk)
                , new SqlParameter("@kaHigh24h", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaHigh24h)
                , new SqlParameter("@kaLow24h", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaLow24h)
                , new SqlParameter("@kaVolume24h", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kaVolume24h)
                , new SqlParameter("@kaRetrievedAt", SqlDbType.DateTime, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p. kaRetrievedAt.ToLocalTime())

            };

                string sp = "spKrakenUpdateById @kaId,@kaIndex,@kaPair,@kaLastTrade,@kaOpen,@kaBid,@kaAsk,@kaHigh24h,@kaLow24h,@kaVolume24h,@kaRetrievedAt";

                var retSP = this.dbCon?.lKraken.FromSqlRaw(sp, lParams).AsEnumerable();

                ret = retSP?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                exc = ex;
            }

            return ret;
        }



        public List<cKrakenPercentageSwing>? doKrakenPercentageSwingList(cKrakenPercentageSwingParms p)
        {
            List<cKrakenPercentageSwing> ret = new List<cKrakenPercentageSwing>();

            try
            {
                SqlParameter[] lParams = {
                new SqlParameter("@kapsMinSwing", SqlDbType.Decimal, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kapsMinSwing)
                , new SqlParameter("@kapsPeriodValue", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kapsPeriodValue)
                , new SqlParameter("@kapsPeriodUnit", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kapsPeriodUnit)
                , new SqlParameter("@kapsPeriodOffset", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.kapsPeriodOffset)             

            };

                string sp = "spKrakenRollingPercentSwing @kapsMinSwing, @kapsPeriodValue, @kapsPeriodUnit, @kapsPeriodOffset";

                var retSP = this.dbCon?.lKrakenPercentageSwing.FromSqlRaw(sp, lParams).AsEnumerable();

                ret = retSP?.ToList()!;
            }
            catch (Exception ex)
            {
                exc = ex;
            }

            return ret;
        }

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

        [Precision(18, 8)]
        public decimal kaLastTrade { get; set; }

        [Precision(18, 8)]
        public decimal? kaOpen { get; set; }

        [Precision(18, 8)]
        public decimal? kaBid { get; set; }

        [Precision(18, 8)]
        public decimal? kaAsk { get; set; }

        [Precision(18, 8)]
        public decimal? kaHigh24h { get; set; }

        [Precision(18, 8)]
        public decimal? kaLow24h { get; set; }

        public string? kaVolume24h { get; set; }
        public DateTime kaRetrievedAt { get; set; }
    }

    public class cKrakenPercentageSwingParms
    {
        public decimal kapsMinSwing { get; set; } = 0.0M;
        public int kapsPeriodValue { get; set; } = 0;
        public string kapsPeriodUnit { get; set; } = "";
        public int kapsPeriodOffset { get; set; } = 0;
    }


    public class cKrakenPercentageSwing
    {
        [Key]
        public string kapsPair { get; set; } = "";
        [Precision(18, 4)]
        public decimal kapsStartTrade { get; set; } = 0.0M;
        [Precision(18, 4)]
        public decimal kapsEndTrade { get; set; } = 0.0M;
        [Precision(18, 4)]
        public decimal kapsTradeDiffPercent { get; set; } = 0.0M;
        [Precision(18, 4)]
        public decimal kapsTradeDiff { get; set; } = 0.0M;
        [Precision(18, 4)]
        public decimal kapsTradeDiffAbs { get; set; } = 0.0M;
        public string kapsStartVolume { get; set; } = "";
        public string kapsEndVolume { get; set; } = "";
        public DateTime kapsStartRetrievedAt { get; set; }
        public DateTime kapsEndRetrievedAt { get; set; }
    }
}
