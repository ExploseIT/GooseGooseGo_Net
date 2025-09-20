
using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Web;
using System.Reflection.Metadata;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace GooseGooseGo_Net.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private dbContext _dbCon;
        private IConfiguration _conf;
        private IWebHostEnvironment _env;

        public AdminController(ILogger<AdminController> logger, IConfiguration conf, IWebHostEnvironment env, dbContext dbCon)
        {

            _logger = logger;
            _dbCon = dbCon;
            _conf = conf;
            _env = env;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Index()
        {
            HttpContext _hc = this.HttpContext;

            mApp _m_App = new mApp(_hc, this.Request, this.RouteData, _dbCon, _conf, _env, _logger);

            return View(_m_App);
        }

        public IActionResult Dashboard()
        {
            HttpContext _hc = this.HttpContext;

            mApp _m_App = new mApp(_hc, this.Request, this.RouteData, _dbCon, _conf, _env, _logger);

            return View(_m_App);
        }







        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            HttpContext _hc = this.HttpContext;
            string _method = _hc.Request.Method;
            mApp _m_App = new mApp(_hc, this.Request, this.RouteData, _dbCon, _conf, _env, _logger);

            return View(_m_App);
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> LoginAsync(cUser? _mUser)
        {
            HttpContext _hc = this.HttpContext;
            string? returnUrl = null;

            bool b_authtype_jwt = true;

            Exception? exc = null;
            mApp? _m_App = null;
            try
            {
                var pqs = HttpUtility.ParseQueryString(this.Request.QueryString.Value ?? "");
                if (pqs.HasKeys())
                {
                    returnUrl = (pqs["ReturnUrl"]! + "").ToLower()!;
                    if (returnUrl.IndexOf("/admin") > 0)
                    {
                        
                    }
                }

                _m_App = new mApp(_hc, this.Request, this.RouteData, _dbCon, _conf, _env, _logger);

                ent_user e_user = new ent_user(_m_App.getDBContext()!, _logger);
                
                if (b_authtype_jwt)
                {
                    var accessToken = Request.Headers["Authorization"].ToString();

                    if (string.IsNullOrEmpty(accessToken))
                    {
                        return Unauthorized(new { Message = "No Authorization token provided" });
                    }

                    // ✅ Remove "Bearer " prefix from token
                    var token = accessToken.Replace("Bearer ", "");

                    // ✅ Decode JWT token
                    var handler = new JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(token);

                    var username = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
                    var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                    var mUser = e_user.doUserReadByUsernamePwd(_mUser!);

                    //Sign in with JWT
                    if (username != null) 
                    {

                    }
                    if (!String.IsNullOrEmpty(returnUrl))
                    {
                        Response.Redirect(returnUrl);
                    }
                }
                else
                {
                    var mUser = e_user.doUserReadByUsernamePwd(_mUser!);
                    //Sign in with Cookie
                    if (mUser != null)
                    {
                        var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, mUser.user_id.ToString()),
                new Claim(ClaimTypes.Name, mUser.user_username!),
            };

                        var identity = new ClaimsIdentity(claims, "CookieAuth");
                        var principal = new ClaimsPrincipal(identity);
                        await this.HttpContext.SignInAsync(principal);

                        this.HttpContext.Response.Redirect(this.HttpContext.Request.PathBase + "/Admin/");
                    }
                    if (!String.IsNullOrEmpty(returnUrl))
                    {
                        Response.Redirect(returnUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                exc = ex;
            }

            return View(_m_App);
        }

        public async Task<IActionResult> LogoutAsync(ent_user? _mUser)
        {
            HttpContext _hc = this.HttpContext;

            mApp _m_App = new mApp(_hc, this.Request, this.RouteData, _dbCon, _conf, _env, _logger);

            await this.HttpContext.SignOutAsync();

            this.HttpContext.Response.Redirect(this.HttpContext.Request.PathBase + "/Admin/");

            return View("Login", _m_App);
        }


        public IActionResult Logout()
        {
            HttpContext _hc = this.HttpContext;

            mApp _m_App = new mApp(_hc, this.Request, this.RouteData, _dbCon, _conf, _env, _logger);
            return View(_m_App);
        }

        private string GenerateJwtToken(string username)
        {
            var jwtSettings = _conf.GetSection("JwtSettings");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpiryMinutes"])),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

