

using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using GooseGooseGo_Net.Services;
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

        public HomeController(ILogger<mApp> logger, IConfiguration conf, IWebHostEnvironment env, IHttpClientFactory httpClientFactory, dbContext dbCon)
        {
            _logger = logger;
            _conf = conf;
            _env = env;
            _dbCon = dbCon;
            _httpClientFactory = httpClientFactory;
        }


        public async Task<IActionResult> Index()
        {
            HttpContext _hc = this.HttpContext;
            Exception? exc = null;

            mApp _m_App = new mApp(_hc, this.Request, this.RouteData, _dbCon, _conf, _env, _logger);

            var _e_cmcap = new ent_cmcap(_conf, _dbCon);
            var _e_kraken = new ent_kraken(_conf, _logger, _httpClientFactory, _dbCon);
            var _e_mexc = new ent_mexc(_conf, _logger, _httpClientFactory, _dbCon);
            var _e_cryptocom = new ent_cryptocom(_conf, _logger, _httpClientFactory, _dbCon);
            var _e_kucoin = new ent_kucoin(_conf, _logger, _httpClientFactory, _dbCon);

            KrakenEnvelope<KrakenTradesHistoryResult>? krakenTradesHistoryData = await _e_kraken.doApi_TradesHistoryAsync(_dbCon);
            KrakenEnvelope<Dictionary<string, string>>? krakenBalanceData = await _e_kraken.doApi_AssetBalanceAsync(_dbCon);
            KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>? krakenData = await _e_kraken.doApi_TickerListAsync(_dbCon);
            /*
            cReturnedKucoin? kucoinData = await _e_kucoin.doApi_TickerListAsync(_dbCon);
            List<MexcTickerEntry>? mexcData = await _e_mexc.doApi_TickerListAsync(_dbCon);
            
            cReturnedCryptoCom? cryptocomData = await _e_cryptocom.doApi_TickerListAsync(_dbCon);

            string json_cmc = await _e_cmcap.doAPIQuery();
            

            _m_App._krakenData = krakenData;
            */

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
