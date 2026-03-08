
using System;
using System.Collections.Concurrent;

namespace GooseGooseGo_Net.svc_mexc
{
    public class PriceCache : IPriceCache
    {
        private readonly ConcurrentDictionary<string, MexcWsTickerSnapshot> _cache = new();

        public bool TryGet(string symbol, out MexcWsTickerSnapshot snap)
        {
            return _cache.TryGetValue(symbol, out snap);
        }

        public void Upsert(string symbol, Func<MexcWsTickerSnapshot, MexcWsTickerSnapshot> updater)
        {
            _cache.AddOrUpdate(symbol,
                _ => updater(null),
                (_, existing) => updater(existing));
        }
    }
}

