

using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using GooseGooseGo_Net.Services;
using GooseGooseGo_Net.svc_mexc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace GooseGooseGo_Net.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<mApp> _logger;
        private IConfiguration _conf;
        private IWebHostEnvironment _env;
        private dbContext _dbCon;
        private IHttpClientFactory _httpClientFactory;
        private readonly PriceCache _priceCache;

        public HomeController(ILogger<mApp> logger, IConfiguration conf, IWebHostEnvironment env, IHttpClientFactory httpClientFactory, dbContext dbCon, PriceCache priceCache)
        {
            _logger = logger;
            _conf = conf;
            _env = env;
            _dbCon = dbCon;
            _httpClientFactory = httpClientFactory;
            _priceCache = priceCache;
        }


        public IActionResult Index()
        {
            var symbols = new[] { "BTC_USDT", "HYPE_USDT", "AIA_USDT", "BCH_USDT" };
            var data = new List<PriceSnapshot>();

            foreach (var symbol in symbols)
            {
                if (_priceCache.TryGet(symbol, out var snapshot))
                {
                    decimal? variation24h = null;
                    if (snapshot.Low24h.HasValue && snapshot.Low24h > 0 && snapshot.High24h.HasValue)
                    {
                        variation24h = ((snapshot.High24h - snapshot.Low24h) / snapshot.Low24h) * 100;
                    }

                    data.Add(new PriceSnapshot
                    {
                        Symbol = symbol,
                        LastPrice = snapshot.Last,
                        Bid = snapshot.Bid,
                        Ask = snapshot.Ask,
                        High24h = snapshot.High24h,
                        Low24h = snapshot.Low24h,
                        Volume24h = snapshot.Vol24,  // Base volume (contracts)
                        QuoteVolume24h = snapshot.Amt24,  // Quote volume (if available)
                        Variation24hPercent = variation24h,
                        TimestampMs = snapshot.TsMs
                    });
                }
            }

            // Optionally, filter for "large" variations (e.g., > 5% - adjust threshold as needed)
            // data = data.Where(d => d.Variation24hPercent > 5).ToList();

            // Or sort by descending variation to highlight large ones first
            data = data.OrderByDescending(d => d.Variation24hPercent).ToList();

            return Json(data);
        }

        public async Task<IActionResult> Index2()
        {
            HttpContext _hc = this.HttpContext;
            Exception? exc = null;

            mApp _m_App = new mApp(_hc, this.Request, this.RouteData, _dbCon, _conf, _env, _logger);
            List<KeyValuePair<string, string>>? qListIn = null;

            var _e_mexc = new ent_mexc(_conf, _logger, _httpClientFactory, _dbCon, _priceCache);

            // Get Mexc Portfolio Data
            cMexcAccounts? mexcAccountsData = await _e_mexc.doApi_AccountsAsync(_dbCon);

            var mexcData = await _e_mexc.doApi_TickerListAsync(_dbCon, qListIn);

            return View(_m_App);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
