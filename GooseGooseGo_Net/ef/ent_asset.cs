

using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace GooseGooseGo_Net.ef
{
    public class ent_asset
    {
        public class cAsset
        {
            [Key]
            public int kaId { get; set; }
            public int kaIndex { get; set; }
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
            public DateTime kaRetrievedAt { get; set; }
            public DateTime kaRetrievedAtSanitised()
            {
                var dt = this.kaRetrievedAt.ToLocalTime();
                return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
            }
        }

        public class cApiParms
        {
            public string apMethod { get; set; } = "GET";
            public string apPath { get; set; } = "";
            public bool apDoSign { get; set; } = false;
        }

    }
}
