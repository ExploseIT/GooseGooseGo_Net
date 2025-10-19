

using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GooseGooseGo_Net.Controllers
{
    [Route("api")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly ILogger<mApp> _logger;
        private dbContext _dbCon;
        private IConfiguration _conf;
        private IWebHostEnvironment _env;
        private IHttpClientFactory _httpClientFactory;

        public ApiController(ILogger<mApp> logger, IConfiguration conf, IWebHostEnvironment env, IHttpClientFactory httpClientFactory, dbContext dbCon)
        {
            _logger = logger;
            _dbCon = dbCon;
            _conf = conf;
            _env = env;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("doAssetPercentageSwingList")]
        public ApiResponse<List<cAssetPercentageSwing>?> doAssetPercentageSwingList(cAssetPercentageSwingParms p)
        {
            var ret = new ent_asset(_conf, _logger, _httpClientFactory, _dbCon).doAssetPercentageSwingList(_dbCon, p);
            return ret;
        }

        [HttpPost("doAssetInfoList")]
        public ApiResponse<List<cAssetInfo>?> doAssetInfoList()
        {
            var ret = new ent_asset(_conf, _logger, _httpClientFactory, _dbCon).doAssetInfoList(_dbCon);
            return ret;
        }

        [HttpPost("doKrakenReturnPortfolio")]
        public async Task<ActionResult<ApiResponse<List<cKrakenPortfolio>>>> DoKrakenReturnPortfolio()
        {
            var svc = new ent_kraken(_conf, _logger, _httpClientFactory, _dbCon);

            var ret = await svc.doKrakenReturnPortfolio(_dbCon);

            if (ret is null)
                return StatusCode(502, "Upstream returned null.");

            return Ok(ret);
        }

        [HttpPost("doMexcReturnPortfolio")]
        public async Task<ActionResult<ApiResponse<List<cMexcOrderLotSummaryGroup<string, cMexcOrderLotSummary>>>>> DoMexcReturnPortfolio()
        {
            var svc = new ent_mexc(_conf, _logger, _httpClientFactory, _dbCon);

            var ret = await svc.doMexcReturnPortfolio(_dbCon);

            if (ret is null)
                return StatusCode(502, "Upstream returned null.");

            return Ok(ret);
        }
    }

}


