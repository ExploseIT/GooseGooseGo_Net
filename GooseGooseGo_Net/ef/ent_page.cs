

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using GooseGooseGo_Net.ef;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Data;

namespace GooseGooseGo_Net.ef
{
    [Table("tblPage")]
    public class ent_page
    {
        private dbContext? dbCon { get; } = null;
        private Guid? userId { get; } = null;
        public ent_page()
        {
        }

        public ent_page(dbContext dbCon)
        {
            this.dbCon = dbCon;
        }

        public ent_page? doPageReadHome()
        {
            ent_page? ret = null;
            string cmd = String.Format("Execute spPageReadHome");
            var mQuery = this.dbCon?.lPage.FromSqlRaw(cmd);
            var mEnum = mQuery?.AsEnumerable<ent_page>();
            ret = mEnum?.FirstOrDefault();

            return ret;
        }

        public ent_page? doPageReadById(Guid page_id)
        {
            ent_page? ret = null;

            SqlParameter[] lParams = {
                new SqlParameter("@page_Id", SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, page_id)
            };

            string sp = "spPageReadById @page_Id";

            var retSP = this.dbCon?.lPage.FromSqlRaw(sp, lParams).AsEnumerable();

            ret = retSP?.FirstOrDefault();

            return ret;
        }

        public ent_page? doPageReadNew()
        {
            ent_page? ret = null;

            SqlParameter[] lParams = {
                new SqlParameter("@page_Id", SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, Guid.Empty)
            };

            string sp = "spPageReadById @page_Id";

            var retSP = this.dbCon?.lPage.FromSqlRaw(sp, lParams).AsEnumerable();

            ret = retSP?.FirstOrDefault();

            return ret;
        }

        public List<ent_page>? doPageReadList()
        {
            List<ent_page>? ret = null;

            SqlParameter[] lParams = { };

            string sp = "spPageReadList";

            var retSP = this.dbCon?.lPage.FromSqlRaw(sp, lParams).AsEnumerable();

            ret = retSP?.ToList();

            return ret;
        }

        public ent_page? doPageUpdate(ent_page p)
        {
            ent_page? ret = null;

            SqlParameter[] lParams = {
                new SqlParameter("@page_Id", SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.page_Id)
                , new SqlParameter("@page_author_id", SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.page_author_id)
                , new SqlParameter("@page_date", SqlDbType.DateTime, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.page_date)
                , new SqlParameter("@page_title", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.page_title)
                , new SqlParameter("@page_status", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.page_status)
            };

            string sp = "spPageUpdateById @page_Id,@page_author_id,@page_date,@page_title,@page_status";

            var retSP = this.dbCon?.lPage.FromSqlRaw(sp, lParams).AsEnumerable();

            ret = retSP?.FirstOrDefault();

            return ret;
        }


        public string? doDisplayDateFormat()
        {
            string? ret = "";
            if (page_date != null)
            {
                ret = page_date?.ToString("dd/MM/yyyy hh:mm");
            }
            return ret;
        }

        public string? doDisplayAuthor()
        {
            string? ret = "";
            ret = page_author;
            return ret;
        }

        public string doDisplayStatus()
        {
            string? ret = "";
            foreach (var opt in page_status_options)
            {
                if (opt[0].Equals(page_status))
                {
                    ret = opt[1];
                    break;
                }
            }
            return ret;
        }

        public string[][] page_status_options = new String[][] { new String [] {  "pagestatus_draft", "Draft" },
            new String [] { "pagestatus_public", "Public" }
        };
        public string[][] page_type_options = new String[][] {
            new String [] { "pagetype_home", "Home" }
            ,new String [] { "pagetype_normal", "Normal" }
        };


        [Key]
        public Guid? page_Id { get; set; }
        public Guid? page_author_id { get; set; }
        public string? page_author { get; set; }
        public DateTime? page_date { get; set; }
        public string? page_title { get; set; }
        public string? page_status { get; set; }
        public string? page_type { get; set; }
    }
}
