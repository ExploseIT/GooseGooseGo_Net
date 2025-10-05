
using Microsoft.EntityFrameworkCore;
using GooseGooseGo_Net.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Data;
using GooseGooseGo_Net.ef;
using System.Text;

namespace GooseGooseGo_Net.ef
{
    public class dbContext : DbContext
    {

        public dbContext()
        {
        }

        public dbContext(DbContextOptions<dbContext> options)
: base(options)
        { }

        public DbSet<c_setting> lSetting { get; set; }
        public DbSet<cUser> lUser { get; set; }
        public DbSet<ent_title> lTitle { get; set; }
        public DbSet<ent_page> lPage { get; set; }
        public DbSet<cPageData> lPageData { get; set; }
        public DbSet<cPageContent> lPageContent { get; set; }
        public DbSet<cKraken> lKraken { get; set; }
        public DbSet<cKrakenAssetInfo> lKrakenAssetInfo { get; set; }
        public DbSet<cKrakenPercentageSwing> lKrakenPercentageSwing { get; set; }
        public DbSet<cKrakenInfo> lKrakenInfoList { get; set; }
        public DbSet<cMexc> lMexc { get; set; }
        public DbSet<cMexcAssetInfo> lMexcAssetInfo { get; set; }
        public DbSet<cMexcPercentageSwing> lMexcPercentageSwing { get; set; }
        public DbSet<cMexcInfo> lMexcInfoList { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {

            }
        }
    }

}


