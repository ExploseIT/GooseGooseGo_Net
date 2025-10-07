

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
    }

}


