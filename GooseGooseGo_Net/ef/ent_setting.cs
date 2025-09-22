
using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace GooseGooseGo_Net.ef
{


    public class ent_setting
    {
        private dbContext? dbCon { get; } = null;
        private ILogger? logger { get; } = null!;
        
        private Exception? exc = null;


        public ent_setting(dbContext dbCon, ILogger logger)
        {
            this.dbCon = dbCon;
            this.logger = logger;
        }

        public List<c_setting> getList()
        {
            var ls = this.dbCon?.lSetting;
            List<c_setting> ret = ls!.ToList<c_setting>();

            return ret;
        }

        public string doHide()
        {
            string ret = "";
            ret = "style='display:none'";
            return ret;
        }


        public string getVersion()
        {
            return getVersion(false);
        }

        public string getVersion(bool v)
        {
            string ret = "";
            string format = "{0}";
            if (v)
            {
                format = "v{0}";
            }
            ret = String.Format(format, typeof(c_setting).Assembly.GetName().Version);

            return ret;
        }



        public bool isVersionDifferent()
        {
            bool ret = false;
            string sAppVersion = getVersion();
            string sDBVersion = getValue("Version");
            string sMatch = @"(?<sig>[\d]+\.[\d]+\.[\d]+)\.[\d]+";

            Match match1 = new Regex(sMatch).Match(sAppVersion);
            Match match2 = new Regex(sMatch).Match(sDBVersion);
            if (match1.Success && match2.Success)
            {
                if (String.Compare(match1.Groups["sig"].Value, match2.Groups["sig"].Value, true) != 0)
                {
                    ret = true;
                }

            }

            return ret;
        }

        public string getValue(string name)
        {
            return "value";
        }


        public c_setting? doSettingsReadByName(string setName)
        {
            c_setting ret = new c_setting();
            SqlParameter[] lParams = { new SqlParameter("@setName", setName) };
            string cmd = String.Format("Execute spSettingsList '{0}'", setName);
            var mQuery = this.dbCon!.lSetting.FromSqlRaw(cmd);
            var mEnum = mQuery.AsEnumerable<c_setting>();
            if (mEnum != null)
            {
                ret = mEnum.FirstOrDefault<c_setting>()!;
            }
            return ret;
        }

        public c_setting doSettingsListAllIntoSettings()
        {
            c_setting ret = new c_setting();


            return ret;
        }
        public List<c_setting>? doSettingsList(string setName)
        {
            List<c_setting>? ret = null;
            SqlParameter[] lParams = { new SqlParameter("@setName", setName) };
            string cmd = String.Format("Execute spSettingsList '{0}'", setName);
            var mQuery = this.dbCon!.lSetting.FromSqlRaw(cmd);
            var mEnum = mQuery.AsEnumerable<c_setting>();

            ret = mEnum.ToList<c_setting>();

            return ret;
        }

        public List<c_setting>? doSettingsListAll()
        {
            List<c_setting>? ret = null;
            string cmd = String.Format("Execute spSettingsListAll");
            var mQuery = this.dbCon!.lSetting.FromSqlRaw(cmd);
            var mEnum = mQuery.AsEnumerable<c_setting>();

            ret = mEnum.ToList<c_setting>();

            return ret;
        }

        public List<c_setting>? doSettingsUpdateValue(string setName, string setValue)
        {
            List<c_setting>? ret = new List<c_setting>();
            string cmd = String.Format("Execute spSettingsUpdateValue '{0}','{1}'", setName, setValue);
            var mQuery = this.dbCon!.lSetting.FromSqlRaw(cmd);
            var mEnum = mQuery.AsEnumerable<c_setting>();

            ret = mEnum.ToList<c_setting>();
            return ret;
        }

        public c_setting doSettingReadByName(string setName)
        {
            c_setting ret = null!;

            try
            {
                SqlParameter[] lParams = {
                new SqlParameter("@setName", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, setName)
            };

                string sp = "spSettingsReadByName @setName";

                var retSP = this.dbCon?.lSetting.FromSqlRaw(sp, lParams).AsEnumerable();

                ret = retSP?.FirstOrDefault()!;
            }
            catch (Exception ex)
            {
                exc = ex;
            }

            return ret;
        }

        public c_setting doSettingsInsertByName(string setName, string setValue, string setDescription)
        {
            c_setting ret = null!;

            try
            {
                SqlParameter[] lParams = {
                new SqlParameter("@setName", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, setName)
                , new SqlParameter("@setValue", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, setValue)
                , new SqlParameter("@setDescription", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, setDescription)
            };

                string sp = "spSettingsInsertByName @setName, @setValue, @setDescription";

                var retSP = this.dbCon?.lSetting.FromSqlRaw(sp, lParams).AsEnumerable();

                ret = retSP?.FirstOrDefault()!;
            }
            catch (Exception ex)
            {
                exc = ex;
            }

            return ret;
        }

        public bool getMustHaveEmail()
        {
            bool ret = false;

            bool.TryParse(getValue("MustHaveEmail"), out ret);

            return ret;
        }

        public bool getMustHaveStaffNo()
        {
            bool ret = false;

            bool.TryParse(getValue("MustHaveStaffNo"), out ret);

            return ret;
        }

        public bool IsUploadLastIfNoChanges()
        {
            bool ret = false;
            string retStr = getValue("UploadLastIfNoChanges");
            bool.TryParse(retStr, out ret);
            return ret;

        }

    }

    [Table("tblSettings")]
    public class c_setting
    {
        private List<c_setting>? l_settings { get; set; } = null;

        public c_setting()
        {
        }

        public c_setting set_settings(dbContext? _dbCon)
        {
            string cmd = String.Format("Execute spSettingsListAll");
            var mQuery = _dbCon!.lSetting.FromSqlRaw(cmd);
            var mEnum = mQuery.AsEnumerable<c_setting>();

            l_settings = mEnum.ToList<c_setting>();

            return this;
        }

        public string gset(string set_name)
        {
            string ret = "";

            if (l_settings != null)
            {
                var retObj = l_settings.FirstOrDefault(s => String.Compare(s.setName, set_name, true) == 0);
                if (retObj != null)
                {
                    if (retObj.setValue != null)
                    {
                        ret = retObj.setValue;
                    }
                }
            }

            return ret;
        }

        [Key]
        public string setName { get; set; } = String.Empty;
        public string setValue { get; set; } = String.Empty;
        public string setDescription { get; set; } = String.Empty;
    }
}