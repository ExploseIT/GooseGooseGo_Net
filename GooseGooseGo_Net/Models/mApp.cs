
using Azure.Core;
using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using GooseGooseGo_Net.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace GooseGooseGo_Net.Models
{
    public class mApp : mAppExt
    {
        public bool bIsLocal { get; set; } = false;
        public mApp(HttpContext hc, HttpRequest request, Microsoft.AspNetCore.Routing.RouteData rd, dbContext dbCon, IConfiguration conf, IWebHostEnvironment env, ILogger _logger)
        {

            this._m_hc = hc;
            this._m_conf = conf;
            this._m_dbCon = dbCon;
            this.siteEnv = env;
            this._c_rd = new cRouteData(rd);
            this.mIdentity = _m_hc.User.Identity;
            if (this.mIdentity!= null && this.mIdentity.IsAuthenticated)
            {
                this.mIsLoggedIn = true;
                var e_user = new ent_user(this._m_dbCon, _logger);
                var _id = _m_hc.User.Claims.FirstOrDefault(c => c.Type.IndexOf("identifier")>0)!.Value;
                var _cUser = new cUser();
                _cUser.user_username = _m_hc.User.Identity!.Name;
                _cUser.user_id = Guid.Parse(_id);
                this.mUser = e_user.doUserReadByUsernameId(_cUser);
            }
            else
            {
                this.mIsLoggedIn = false;
            }

            //if (hc != null)
            if (dbCon != null)
            {
                this.bIsLocal = ("" + request.Host).ToLower().IndexOf("localhost") > -1;

                if (this.bIsLocal)
                {
                    mUrl =  "http://" + request.Host + request.PathBase;
                }
                else
                {
                    mUrl = "https://" + request.Host + request.PathBase;
                }

                //mUrl = request.Scheme + "://" + request.Host + request.PathBase;
                string mRemoteIpAddress = request.HttpContext.Connection.RemoteIpAddress!.ToString();
                string mUserAgent = request.Headers["User-Agent"].ToString();
                mBrowserDetails = new cBrowserDetails(mRemoteIpAddress, mUserAgent);

                //mUrl = request.Scheme + "://" + request.Host + request.PathBase;
                //mUrl = _m_nm.BaseUri;
                if (mUrl != null)
                {
                    int mUrl_slash_index = mUrl.LastIndexOf(@"/");
                    if (mUrl_slash_index == mUrl.Length - 1)
                    {
                        mUrl = mUrl.Substring(0, mUrl.Length - 1);
                    }
                }
                mVersion = typeof(mApp).Assembly.GetName().Version!.ToString();

                /*

                var _e_PD = new ent_pagedata(dbCon);
                
                _cPageData = _e_PD.pdPageDataReadByControllerAction(_c_rd);
                _cPageData!.pd_contents = _e_PD.pdPageContentList(_c_rd);
                _cPageDatas = _e_PD.pdPageDataList();

                bool is_local = this.bIsLocal;
                bool is_all = this._c_rd.rd_controller == "admin" ? true : false;

                List<cBlog> cBlogs = new ent_blog(dbCon).doBlogList(bIsLocal, is_all);

                this._cBlogs = cBlogs;

                List<cBlogStatus> cBlogStatuses = new ent_blog(dbCon).doBlogStatusList();
                this._cBlogStatuses = cBlogStatuses;

                var _e_productdata = new ent_productdata(dbCon);


                Guid guid = Guid.Empty;
                if (Guid.TryParse(_c_rd.rd_id, out guid))
                {
                    this._cProductData = _e_productdata.prdProductDataReadById(guid);
                }
                this._cProductDatas = new ent_productdata(dbCon).prdProductDataList(this.bIsLocal);
                */

                this._c_setting = new c_setting().set_settings(dbCon);

                mPath = "";
                if (hc != null)
                {
                    request = _m_hc.Request;
                    mPath = _m_hc.Request.Path == "" ? "/Home" : _m_hc.Request.Path;
                }
                var jwtSettings = _m_conf.GetSection("JwtSettings");
                if (jwtSettings != null)
                {
                    string jwtTokenName = jwtSettings["TokenName"]!;
                    string jwtKey = jwtSettings["SecretKey"]!;
                    string jwtIssuer = jwtSettings["Issuer"]!;
                    string jwtAudience = jwtSettings["Audience"]!;

                    this._m_jwtTokenName = jwtTokenName;
                    this._m_jwtKey = jwtKey;
                    this._m_jwtIssuer = jwtIssuer;
                    this._m_jwtAudience = jwtAudience;
                }
                ent_setting eSettings = new ent_setting(dbCon, _logger);

                var _m_settings = new ent_setting(_m_dbCon, _logger);
                this._m_settings_all = _m_settings.doSettingsListAll();

            }
            
            IConfigurationSection cs = conf.GetSection("FileInfo:FilePath");
            if (cs != null)
            {
                this.mVDir = cs.Value!;
                this.siteDataPathName = cs.Value!;
                bool success = GetDataPath(env.ContentRootPath, cs.Value!, out this.site_datapath, out this.site_rootpath);
            }

        }

        /*
        public Dictionary<Guid, List<cProductData>>? prdProductDataList(bool bIsLive)
        {
            Dictionary<Guid, List<cProductData>>? ret = null;
            ret = new ent_productdata(getDBContext()).prdProductDataList(bIsLive);
            return ret;
        }

        public cProductData? prdProductDataItem()
        {
            cProductData? ret = null;
            ret = _prdProductData;
            return ret;
        }

        public cProductData? prdProductDataItem(string prdUrl)
        {
            cProductData? ret = null;
            _prdProductData = new ent_productdata(getDBContext()).prdProductDataReadByUrl(prdUrl);
            ret = _prdProductData;
            return ret;
        }


        public List<c_featuredproduct>? fpFeaturedProductList()
        {
            List<c_featuredproduct>? ret = null;
            ret = new ent_featuredproduct(getDBContext()!).fpFeaturedProductList();
            return ret;
        }
        */

        public string getBrowserDetails()
        {
            string ret = JsonSerializer.Serialize(mBrowserDetails);
            return ret;
        }

        public string displayVersion()
        {
            return "v" + this.mVersion;
        }





        public string? GetDataPath()
        {
            string? ret = null;
            ret = GetDataPath("");
            return ret;
        }

        public string? GetDataPathName()
        {
            return this.siteDataPathName;
        }

        public string GetDataPathNameSlash()
        {
            return String.Format("/{0}", this.siteDataPathName);
        }

        public string? GetDataPath(string path)
        {
            string? ret = null;
            string? crp = this.siteEnv!.ContentRootPath;
            DirectoryInfo? cParent = null;
            do
            {
                ret = Directory.EnumerateDirectories(crp!).FirstOrDefault<string>(c => c.ToLower().IndexOf(siteDataPathName!.ToLower()) > 0);
                cParent = Directory.GetParent(crp!);
                if (cParent != null)
                {
                    crp = cParent.ToString();
                }
                else
                {
                    crp = null;
                }
            } while ((ret == null) && (cParent != null));

            if (String.IsNullOrEmpty(crp))
            {
                ret = String.Format(@"C:\inetpub\wwwroot\{0}", this.siteDataPathName);
            }
            if (!String.IsNullOrEmpty(path))
            {
                string newpath = Path.Combine(ret!, path);
                ret = newpath;
            }
            return ret;
        }

        public bool IsAuthenticated()
        {
            bool ret = false;

            if (mIdentity != null)
            {
                ret = mIdentity.IsAuthenticated;
            }
            return ret;
        }

        public void cookieTest(HttpContext hc)
        {
            var lCookie = hc.Request.Cookies["username"];
            CookieOptions opt = new CookieOptions();
            opt.Expires = DateTime.Now.AddSeconds(30);
            hc.Response.Cookies.Append("username", "supatee", opt);
            //hc.Response.Cookies.Delete("username");
        }

        public void setId(string id)
        {
            mId = id;
        }

        public void setId2(string id2)
        {
            mId2 = id2;
        }

        public string? getPath(string path)
        {
            string? ret = Path.Combine(this.site_datapath!, path);
            return ret;
        }

        public bool GetDataPath(string contentrootpath, string datapathname, out string datapath, out string dataparentpath)
        {
            bool ret = false;
            string? retStr = null;
            string retParentStr = contentrootpath;
            string? crp = contentrootpath;
            DirectoryInfo? cParent = null;
            do
            {
                retStr = Directory.EnumerateDirectories(crp!).FirstOrDefault<string>(c => c.ToLower().IndexOf(datapathname.ToLower()) > 0);
                cParent = Directory.GetParent(crp!);
                if (cParent != null)
                {
                    crp = cParent.ToString();
                }
                else
                {
                    crp = null;
                }
            } while ((retStr == null) && (cParent != null));

            if (String.IsNullOrEmpty(crp))
            {
                retStr = String.Format(@"C:\inetpub\wwwroot\{0}", datapathname);
            }

            datapath = retStr!;
            dataparentpath = retParentStr;
            return ret;
        }

        /*
        public ent_post get_ent_post()
        {
            ent_post ret = new ent_post(getDBContext()!);
            return ret;
        }
        */

        public dbContext? getDBContext()
        {
            return _m_dbCon;
        }

        public IConfiguration? getSiteConf()
        {
            return _m_conf;
        }

        //** Admins
        public c_setting? _c_setting { get; set; } = null;
        public cPageData? _cPageData { get; set; } = null;
        public List<cPageData>? _cPageDatas { get; set; } = null;

        /*
        //public List<cPageContent>? _cPageContents { get; set; } = null;
        public cPageContent? _cPageContent { get; set; } = null;
        public cProductData? _cProductData { get; set; } = null;
        public Dictionary<Guid, List<cProductData>>? _cProductDatas { get; set; } = null;
        public List<cProductSource>? _prdsProductSources { get; set; } = null;
        public List<cProductCat>? _prdcProductCats { get; set; } = null;        
        public c_setting? _c_setting { get; set; } = null;
        public List<cBlog>? _cBlogs { get; set; } = null;
        public List<cBlogStatus>? _cBlogStatuses { get; set; } = null;
        public cBlog? _cBlog { get; set; } = null;
        */

        /*
            public cPageData? pdPageData()
      
            cPageData? ret = null;

            
            ret = new cPageData();
            ret.pd_contents = new ent_pagedata(getDBContext()).pdPageContentListByPage("index");
            
            return ret;
    
            */

        //** Admins



        public cBrowserDetails? mBrowserDetails { get; set; } = null;
        public string mUserAgent { get; set; } = "";
        //public System.Net.IPAddress? mRemoteIpAddress { get; set; } = null;
        public string mRemoteIpAddress { get; set; } = "";
        public bool mIsLoggedIn { get; set; } = false;
        
        /*
        public c_post? mPost { get; set; } = null;
        public c_post? mPost_About { get; set; } = null;
        public List<c_post>? mPosts { get; set; } = null;
        public List<ent_page>? mPages { get; set; } = null;
        public ent_page? mPage { get; set; } = null;
        public ent_menu? mMenu { get; set; } = null;
        public List<ent_menu>? mMenus { get; set; } = null;
        */

        public string? site_datapath = null;
        public string? site_rootpath = null;

        public Guid mUid = Guid.Empty;
        public string? mId = null;
        public string? mId2 = null;


        private IWebHostEnvironment? siteEnv = null;
        private string? siteDataPathName = null;




        private IConfiguration? _m_conf = null;
        private HttpContext? _m_hc = null;
        public cRouteData? _c_rd { get; set; } = null;
        private dbContext? _m_dbCon = null;
        public NavigationManager? _m_nm = null;

        public cUser? mUser { get; set; } = null;
        public IIdentity? mIdentity { get; set; } = null;


        public string? mVDir { get; set; } = null;
        public string? mUrl { get; set; } = null;
        public string? mPath { get; set; } = null;
        public string? mVersion { get; set; } = null;
        public ent_userandroles_success? e_User { get; set; } = null;

        public List<c_setting>? _m_settings_all { get; set; } = null;

        public string? _m_jwtTokenName { get; set; } = null;
        public string? _m_jwtKey { get; set; } = null;
        public string? _m_jwtIssuer { get; set; } = null;
        public string? _m_jwtAudience { get; set; } = null;
    }

    public class cBrowserDetails
    {
        public cBrowserDetails(string _mRemoteIP, string _mUserAgent)
        {
            mRemoteIP = _mRemoteIP;
            mUserAgent = _mUserAgent;
        }

        public string mRemoteIP { get; set; } = string.Empty;
        public string mUserAgent { get; set; } = String.Empty;
    }

    public class sPair
    {
        public sPair()
        {

        }

        public sPair(string key, string value)
        {
            this.spKey = key;
            this.spValue = value;
        }

        public string? spKey { get; set; } = null;
        public string? spValue { get; set; } = null;
    }

    public class cRouteData
    {
        public cRouteData(string cont, string action, string id)
        {
            this.rd_id = id;
            this.rd_action = action;
            this.rd_controller = cont;
        }

        public cRouteData(Microsoft.AspNetCore.Routing.RouteData _rd)
        {
            this.rd_id = (_rd.Values["id"] ?? "").ToString()!.ToLower();
            this.rd_action = (_rd.Values["action"] ?? "").ToString()!.ToLower();
            this.rd_controller = (_rd.Values["controller"] ?? "").ToString()!.ToLower();
            this.rd_area = (_rd.Values["area"] ?? "").ToString()!.ToLower();
        }
        public string rd_id { get; set; } = "";
        public string rd_action { get; set; } = "";
        public string rd_controller { get; set; } = "";
        public string rd_area { get; set; } = "";
    }

    public class CookieAuthOptions
    {
        public string CookieName { get; set; } = string.Empty;
        public int ExpireTime { get; set; }
        public bool Secure { get; set; }
        public bool HttpOnly { get; set; }
    }

}


