
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using GooseGooseGo_Net.ef;
using GooseGooseGo_Net.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Web;

namespace GooseGooseGo_Net.ef
{
    [Table("tblUser")]
    public class ent_user : cIsDbNull
    {
        private dbContext? dbCon { get; } = null;
        private ILogger? _logger { get; } = null!;

        Exception? exc { get; set; } = null;

        public ent_user(dbContext dbCon, ILogger logger)
        {
            this.dbCon = dbCon;
            this._logger = logger;
        }


        public cUser? doUserReadByUsernameId(cUser mUser)
        {
            cUser? ret = null;

            string cmd = String.Format("Execute spUserReadByUsernameId '{0}','{1}'", mUser.user_id, mUser.user_username);
            var mQuery = this.dbCon!.lUser.FromSqlRaw(cmd);
            var mEnum = mQuery.AsEnumerable<cUser>();

            ret = mEnum.FirstOrDefault<cUser>();
            return ret;
        }

        public cUser? doUserReadByUsernamePwd(cUser mUser)
        {
            cUser? ret = null;
            string baSaltStr = new ent_setting(dbCon!, _logger).doSettingsReadByName("PasswordSalt")!.setValue!;
            byte[] baSalt = System.Text.Encoding.Unicode.GetBytes(baSaltStr);
            string sPwdHash = getPasswordHash(mUser.user_pwd!, baSalt);

            string cmd = String.Format("Execute spUserReadByUsernamePwd '{0}','{1}'", mUser.user_username, sPwdHash);
            var mQuery = this.dbCon!.lUser.FromSqlRaw(cmd);
            var mEnum = mQuery.AsEnumerable<cUser>();

            ret = mEnum.FirstOrDefault<cUser>();
            return ret;

        }

        public cUser? doUserUpdatePwdById(Guid userId, string pwd)
        {
            cUser? ret = null;
            string baSaltStr = new ent_setting(dbCon!, _logger).doSettingsReadByName("PasswordSalt")!.setValue!;
            byte[] baSalt = System.Text.Encoding.Unicode.GetBytes(baSaltStr);
            string userPwd = getPasswordHash(pwd, baSalt);

            string cmd = String.Format("Execute spUserUpdatePwdById '{0}','{1}'", userId, userPwd);
            var mQuery = this.dbCon?.lUser.FromSqlRaw(cmd);
            var mEnum = mQuery?.AsEnumerable<cUser>();

            ret = mEnum?.FirstOrDefault<cUser>();
            return ret;

        }

        public cUser? doUserReadById(string userId)
        {
            cUser? ret = null;

            string cmd = String.Format("Execute spUserReadByid '{0}'", userId);
            var mQuery = this.dbCon?.lUser.FromSqlRaw(cmd);
            var mEnum = mQuery?.AsEnumerable<cUser>();

            ret = mEnum?.FirstOrDefault<cUser>();
            return ret;
        }

        public cUser? doUserReadIsRegTest()
        {
            cUser? ret = null;
            string cmd = String.Format("Execute spUserReadIsRegTest");
            var mQuery = this.dbCon?.lUser.FromSqlRaw(cmd);
            var mEnum = mQuery?.AsEnumerable<cUser>();

            ret = mEnum?.FirstOrDefault<cUser>();
            return ret;

        }

        public List<ent_title> doUserTitleList()
        {
            List<ent_title> ret = new List<ent_title>();

            SqlParameter[] lParams = { };

            string sp = "spUserTitleList ";

            var retSP = this.dbCon?.lTitle.FromSqlRaw(sp, lParams).AsEnumerable();

            ret = retSP?.ToList()!;
            return ret;
        }

        private string getPasswordHash(string pwd, byte[] baSalt)
        {
            string ret = "";

            // Generate a 128-bit salt using a sequence of
            // cryptographically strong random bytes.


            //var ps = m_App._m_settings_all.FirstOrDefault<ent_setting>(s => s.setName.ToLower().Equals("passwordsalt"));
            //if (ps.setValue != null && ps.setValue.Length < 3)
            //{
            //salt = RandomNumberGenerator.GetBytes(128 / 8); // divide by 8 to convert bits to bytes
            //var _m_settings = new ent_setting(m_App.getContextDB());
            //string strSalt = Convert.ToBase64String(salt);
            //byte[] bSalt = Convert.FromBase64String(strSalt);
            //_m_settings.doSettingsUpdateValue("PasswordSalt", strSalt);
            //}
            //else
            //{
            //salt = Convert.FromBase64String(ps.setValue);
            //}

            // derive a 256-bit subkey (use HMACSHA256 with 100,000 iterations)
            ret = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: pwd!,
                salt: baSalt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 256 / 8));

            //Guid userId = Guid.Parse("2379a598-b843-48b2-bccd-4b166502ec63");
            //ent_user _m_ent_user = new ent_user(m_App.getContextDB(), m_App);
            //_m_ent_user.doUserUpdatePwdById(userId, pwd);
            return ret;
        }


        public IEnumerable<cUser>? doUserProfileCreateBase(cUser p)
        {

            SqlParameter[] lParams = {
            new SqlParameter("@user_username", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, IsDbNull(p.user_username))
            , new SqlParameter("@user_firebase", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, IsDbNull(p.user_firebase))
            , new SqlParameter("@user_deviceid", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, IsDbNull(p.user_deviceid))
        };
            string sp = "exec spUserCreateByUsernameFirebase @user_username, @user_firebase, @user_deviceid";

            return this.dbCon?.lUser.FromSqlRaw(sp, lParams).AsEnumerable();
        }

        public IEnumerable<cUser>? doUserProfileReadByFirebaseBase(cUser p)
        {

            SqlParameter[] lParams = {
            new SqlParameter("@user_firebase", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, IsDbNull(p.user_firebase))
            , new SqlParameter("@user_deviceid", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, IsDbNull(p.user_deviceid))
        };
            string sp = "exec spUserReadByFirebase @user_firebase, @user_deviceid";

            return this.dbCon?.lUser.FromSqlRaw(sp, lParams).AsEnumerable();
        }

        public ApiResponse<cUser> doUserProfileCreate(cUser p)
        {
            //cUser ret = new cUser();
            ApiResponse<cUser> ret = new ApiResponse<cUser>();
            try
            {
                var retSP = this.doUserProfileCreateBase(p);

                if (retSP != null)
                {
                    ret.apiData = retSP?.FirstOrDefault<cUser>()!;
                }
            }
            catch (Exception ex)
            {
                exc = ex;
                ret.apiSuccess = false;
                ret.apiMessage = exc.Message;
                this._logger?.LogError(ex, "Error in doCreateUserProfile");
            }

            return ret;
        }

        public ApiResponse<cUser> doUserProfileReadByFirebase(cUser p)
        {
            //cUser ret = new cUser();
            ApiResponse<cUser> ret = new ApiResponse<cUser>();
            try
            {
                var retSP = this.doUserProfileReadByFirebaseBase(p);

                if (retSP != null)
                {
                    ret.apiData = retSP?.FirstOrDefault<cUser>()!;
                }
                if (ret.apiData == null)
                {
                    ret.apiData = new cUser(p, enUserStatus.enUserNotFound);
                }
            }
            catch (Exception ex)
            {
                exc = ex;
                ret.apiSuccess = false;
                ret.apiMessage = exc.Message;
                this._logger?.LogError(ex, "Error in doUserProfileReadByFirebase");
            }

            return ret;
        }

    }

    public class cUser
    {
        public cUser() { }

        public cUser(cUser p, enUserStatus _status)
        {
            this.user_status = _status;
            this.user_firebase = p.user_firebase;
        }


        [Key]
        public Guid? user_id { get; set; } = Guid.Empty;
        public string? user_username { get; set; } = "Guest";
        public string? user_firstname { get; set; } = "";
        public string? user_lastname { get; set; } = "";
        public string? user_title { get; set; } = "";
        public string? user_pwd { get; set; } = "";
        public string? user_email { get; set; } = "";
        public string? user_phone { get; set; } = "";
        public string user_settings { get; set; } = "";
        public bool? user_isreg { get; set; } = false;
        public bool? user_alreadyexists { get; set; } = false;
        public DateTime? user_dtreg { get; set; } = null;
        public DateTime? user_dtdob { get; set; } = null;
        public bool? user_regtest { get; set; } = null;
        public bool? user_rememberme { get; set; } = null;
        public string? user_firebase { get; set; } = "";
        public string user_deviceid { get; set; } = "";
        [NotMapped]
        public enUserStatus? user_status { get; set; } = enUserStatus.enUserDefault;
    }

    public enum enUserStatus
    {
        enUserDefault = 0,
        enUserActive = 1,
        enUserInactive = 2,
        enUserLocked = 3,
        enUserDeleted = 4,
        enUserAlreadyExists = 5,
        enUserNotFound = 6
    }

    public class ent_user_success
    {
        public bool success { get; set; } = false;
        public Exception? exception { get; set; } = null;
        public string message { get; set; } = "";
        public ent_user? user { get; set; }
        public List<ent_user>? userlist { get; set; } = null;
    }

    public class ent_userandroles_success
    {
        public string getFullName()
        {
            string ret = "";
            if (user != null)
            {
                ret = String.Format("{0} {1}", user.user_firstname, user.user_lastname);
            }
            return ret;
        }
        public bool success { get; set; } = false;
        public Exception? exception { get; set; } = null;
        public cUser? user { get; set; } = null;
        public List<ent_userrole>? rolelist { get; set; } = null;
    }

    [Table("tblTitle")]
    public class ent_title
    {
        [Key]
        public string? tit_name { get; set; } = null;
        public string? tit_value { get; set; } = null;
        public int? tit_index { get; set; } = null;
    }

    public class UserProfileRequest
    {
        public string profUserFirebase { get; set; } = "";
        public string profUsername { get; set; } = "";
    }

    public class UserProfileResponse
    {
        public bool profSuccess { get; set; } = true;
        public string profMessage { get; set; } = "";
    }

}