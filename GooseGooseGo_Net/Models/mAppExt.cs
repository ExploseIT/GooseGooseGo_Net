using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Services;

namespace GooseGooseGo_Net.Models
{
    public class mAppExt
    {


        public Dictionary<int, List<cKrakenPercentageSwing>>? lkps = null;
        public KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>? _krakenData { get; set; } = null;
        public CryptoComTickerEnvelope? _cryptocomData { get; set; } = null;
    }
}
