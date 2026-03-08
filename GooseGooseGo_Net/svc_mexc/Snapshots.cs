using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GooseGooseGo_Net.svc_mexc
{
    public class PriceSnapshot
    {
        public string Symbol { get; set; } = "";
        public decimal? LastPrice { get; set; } = null;
        public decimal? Bid { get; set; } = null;
        public decimal? Ask { get; set; } = null;
        public decimal? High24h { get; set; } = null;
        public decimal? Low24h { get; set; } = null;
        public decimal? Volume24h { get; set; } = null;
        public decimal? QuoteVolume24h { get; set; } = null;
        public decimal? Variation24hPercent { get; set; } = null;
        public long TimestampMs { get; set; } = 0;
    }

    public sealed record MexcWsTickerSnapshot(
decimal? Last = null,
decimal? Bid = null,
decimal? Ask = null,
decimal? High24h = null,
decimal? Low24h = null,
decimal? Vol24 = null,   // base volume 24h
decimal? Amt24 = null,   // quote volume 24h
long TsMs = 0
);

}
