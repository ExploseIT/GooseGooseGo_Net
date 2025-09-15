
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.Security.Principal;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using GooseGooseGo_Net.ef;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Security.Cryptography;

namespace GooseGooseGo_Net.Models
{
    public class mUser
    {
        public string cookie_user_id_key = "exp_user_id";
        public string cookie_to_remove_key = "cookie_user_id";
        
        private mApp? _m_App { get; set; } = null;
        private dbContext? _dbCon { get; set; } = null;

        public mUser()
        {

        }
        public mUser(mApp mApp, dbContext dbCon)
        {
            this._m_App = mApp;
            this._dbCon = dbCon;
        }

    }

}