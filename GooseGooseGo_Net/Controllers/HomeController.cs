

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
        private readonly ILogger<ent_kraken> _logger;
        private IConfiguration _conf;
        private IWebHostEnvironment _env;
        private dbContext _dbCon;
        private IHttpClientFactory _httpClientFactory;

        public HomeController(ILogger<ent_kraken> logger, IConfiguration conf, IWebHostEnvironment env, IHttpClientFactory httpClientFactory, dbContext dbCon)
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
            var _e_kraken = new ent_kraken(_conf, _logger, _httpClientFactory);

            var apiDetails = _e_kraken.doApiDetailsDecrypt(_dbCon);

            KrakenEnvelope<Dictionary<string, KrakenTickerEntry>>? krakenData = await _e_kraken.doApi_TickerListAsync(apiDetails!);

            string json_cmc = await _e_cmcap.doAPIQuery();


            _m_App._krakenData = krakenData;

            var all = await CryptoComClient.GetTickersAsync(_conf);

            _m_App._cryptocomData = all;


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
