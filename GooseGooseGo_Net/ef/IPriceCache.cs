using System.Collections.Concurrent;

namespace GooseGooseGo_Net.ef
{
    public interface IPriceCache
    {
        bool TryGet(string symbol, out MexcWsTickerSnapshot snap);
        void Upsert(string symbol, Func<MexcWsTickerSnapshot, MexcWsTickerSnapshot> updater);
    }

    public sealed class PriceCache : IPriceCache
    {
        private readonly ConcurrentDictionary<string, MexcWsTickerSnapshot> _map
            = new(StringComparer.OrdinalIgnoreCase);

        public bool TryGet(string symbol, out MexcWsTickerSnapshot snap)
            => _map.TryGetValue(symbol, out snap!);

        public void Upsert(string symbol, Func<MexcWsTickerSnapshot, MexcWsTickerSnapshot> updater)
            => _map.AddOrUpdate(symbol,
                                _ => updater(new MexcWsTickerSnapshot()),
                                (_, old) => updater(old));
    }
}
