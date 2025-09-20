
using GooseGooseGo_Net.ef;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace GooseGooseGo_Net.Models
{
    public class ent_pagedata
    {
        private Exception? exc = null;
        private cPageData? _cPageData { get; set; } = null;

        private dbContext? dbCon { get; } = null;

        public ent_pagedata() { }

        public ent_pagedata(dbContext dbCon)
        {
            this.dbCon = dbCon;
        }

        public List<cPageData>? pdPageDataList()
        {
            List<cPageData>? ret = null;

            SqlParameter[] lParams = { };

            string sp = "Execute spPageDataList";

            var retSP = this.dbCon?.lPageData.FromSqlRaw(sp, lParams).AsEnumerable();

            if (retSP != null)
            {
                ret = retSP?.ToList<cPageData>()!;
            }
            return ret;
        }

        
        public cPageData? pdPageDataRead(cRouteData _c_rd)
        {
            cPageData? ret = null;

            SqlParameter[] lParams = {
                new SqlParameter("@pd_controller", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, _c_rd.rd_controller)
                , new SqlParameter("@pd_action", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, _c_rd.rd_action)
            };

            string sp = "Execute spPageDataReadByControllerAction @pd_controller, @pd_action";

            var retSP = this.dbCon?.lPageData.FromSqlRaw(sp, lParams).AsEnumerable();

            if (retSP != null)
            {
                ret = retSP?.FirstOrDefault<cPageData>()!;
                if (ret != null)
                {
                    var l_pc = pdPageContentList(_c_rd);
                    ret.pd_contents = l_pc;
                }
            }
            return ret;
        }
        

        public cPageData? pdPageDataReadByControllerAction(cRouteData _c_rd)
        {
            cPageData? ret = new cPageData();
            
            SqlParameter[] lParams = {
                new SqlParameter("@pd_controller", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, _c_rd.rd_controller)
                , new SqlParameter("@pd_action", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, _c_rd.rd_action)
            };

            string sp = "exec spPageDataReadByControllerAction @pd_controller, @pd_action";

            try
            {
                var retSP = this.dbCon?.lPageData.FromSqlRaw(sp, lParams).AsEnumerable();

                if (retSP != null && retSP?.FirstOrDefault<cPageData>()! != null)
                {
                    ret = retSP?.FirstOrDefault<cPageData>()!;
                }
            }
            catch (Exception ex)
            {
                exc = ex;
            }

            return ret;
        }

        public cPageData? pdPageDataUpdate(cPageData p)
        {
            cPageData? ret = null;

            SqlParameter[] lParams = {
                new SqlParameter("@pd_id", SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.pd_id)
                , new SqlParameter("@pd_page", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, p.pd_page)
            };

            string sp = "Execute spPageDataUpdate @pd_id, @pd_page";

            var retSP = this.dbCon?.lPageData.FromSqlRaw(sp, lParams).AsEnumerable();

            if (retSP != null)
            {
                ret = retSP?.FirstOrDefault<cPageData>()!;
                if (ret != null)
                {
                    if (p.pd_contents != null)
                    {
                        foreach(var pc in p.pd_contents)
                        {
                            pc.pc_pd = p;
                            pc.pc_pdpd_id = ret.pd_id;
                            var retPC = pdPageContentUpdate(pc);
                        }

                    }
                }
            }
            return ret;

        }

        public cPageContent? pdPageContentUpdate(cPageContent pc)
        {
            cPageContent? ret = null;

            SqlParameter[] lParams = {
                new SqlParameter("@pc_id", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, pc.pc_id)
                , new SqlParameter("@pc_name", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, pc.pc_name)
                , new SqlParameter("@pc_value", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, pc.pc_value)
                , new SqlParameter("@pc_pdpd_id", SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, pc.pc_pdpd_id)
            };

            string sp = "Execute spPageContentUpdate @pc_id, @pc_name, @pc_value, @pc_pdpd_id";

            var retSP = this.dbCon?.lPageContent.FromSqlRaw(sp, lParams).AsEnumerable();

            if (retSP != null)
            {
                ret = retSP?.FirstOrDefault<cPageContent>()!;
            }
            return ret;

        }

        public List<cPageContent>? pdPageContentList(cRouteData _c_rd)
        {
            List<cPageContent>? ret = null;

            cRouteData _p_c_rd = _c_rd;
            if (_c_rd.rd_controller.Equals("admin"))
            {
                _p_c_rd = new cRouteData("*", "*", "*");
            }
            
            ret = pdPageContentListByControllerAction(_p_c_rd);
            
            return ret;
        }

        public List<cPageContent>? pdPageContentListByControllerAction(cRouteData _c_rd)
        {
            List<cPageContent>? ret = null;

            SqlParameter[] lParams = {
                new SqlParameter("@pd_controller", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, _c_rd.rd_controller)
                , new SqlParameter("@pd_action", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, _c_rd.rd_action)
            };

            string sp = "Execute spPageContentListByControllerAction @pd_controller, @pd_action";

            try
            {
                var retSP = this.dbCon?.lPageContent.FromSqlRaw(sp, lParams).AsEnumerable();

                if (retSP != null)
                {
                    ret = retSP?.ToList<cPageContent>()!;
                }
            }
            catch (Exception ex)
            {
                exc = ex;
            }
            return ret;
        }

        public List<cPageContent>? pdPageContentListAll()
        {
            List<cPageContent>? ret = null;

            SqlParameter[] lParams = { };

            string sp = "Execute spPageContentListAll";

            try
            {
                var retSP = this.dbCon?.lPageContent.FromSqlRaw(sp, lParams).AsEnumerable();

                if (retSP != null)
                {
                    ret = retSP?.ToList<cPageContent>()!;
                }
            }
            catch (Exception ex)
            {
                exc = ex;
            }
            return ret;
        }
        public List<cPageContent>? pdPageContentListByPage(string page_name)
        {
            List<cPageContent>? ret = null;

            SqlParameter[] lParams = {
                new SqlParameter("@pc_page", SqlDbType.NVarChar, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, page_name)
            };

            string sp = "Execute spPageContentListByPage @pc_page";

            try
            {
                var retSP = this.dbCon?.lPageContent.FromSqlRaw(sp, lParams).AsEnumerable();

                if (retSP != null)
                {
                    ret = retSP?.ToList<cPageContent>()!;
                }
            }
            catch (Exception ex)
            {
                exc = ex;
            }
            return ret;
        }

        public cPageContent? pdPageContentReadById(int pc_id)
        {
            cPageContent? ret = null;

            SqlParameter[] lParams = {
                new SqlParameter("@pc_id", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "", DataRowVersion.Current, pc_id)
            };

            string sp = "Execute spPageContentReadById @pc_id";

            try
            {
                var retSPP = this.dbCon?.lPageContent.FromSqlRaw(sp, lParams);

                var retSP = retSPP!.AsEnumerable();

                if (retSP != null)
                {
                    ret = retSP?.FirstOrDefault<cPageContent>()!;
                }
            }
            catch (Exception ex)
            {
                exc = ex;
            }

            return ret;
        }



        public pd_item pd_viva_mk_pd_90684()
        {
            var ret = new pd_item
            {
                pdi_desc = ["This light-up Christmas Tree ornament is accented with a gleaming gold star and multi-coloured pinecone lights, adding a touch of whimsy and festive cheer."
                , "Whether placed on a mantel, shelf or as a centrepiece, this ornament adds a touch of magic to any space."
                , "Comes boxed."
                , "Requires 2 x AAA batteries (not included). Dolomite. H14.5 x W9 x D9cm"
                ],
                pdi_source = "viva_mk",
                pdi_img = "~/GooseGooseGo_Net_data/VivaMK/Christmas-2024_Final-icat/christmas-2024_final-icat-1.png",
                pdi_url = "~/item/viva_mk/led_light_up_christmas_tree",
                pdi_category = "Christmas-2024_Final-icat-1",
                pdi_title = "Led Light-Up Christmas Tree",
                pdi_id = "viva_mk_pd_90684",
                pdi_priceUK = 7.00,
                pdi_priceEuro = 8.50
            };

            return ret;
        }
    }



    public class pd_item
    {
        public pd_item() { }

        public pd_item(string[] _desc, string _id, string _img, string _source, string _cat, string _subcat, string _title, double _priceUK, double _priceEuro)
        {
            pdi_desc = _desc;
            pdi_id = _id;
            pdi_img = _img;
            pdi_source = _source;
            pdi_category = _cat;
            pdi_subcat = _subcat;
            pdi_title = _title;
            pdi_priceUK = _priceUK;
            pdi_priceEuro = _priceEuro;
        }

        public string pdi_desc_display()
        {
            string ret = String.Empty;

            if (pdi_desc != null)
            {
                var sb = new StringBuilder();

                foreach (string _desc in pdi_desc)
                {
                    sb.AppendLine(_desc);
                }
                ret = sb.ToString();
            }
            return ret;
        }


        public string pdi_id { get; set; } = String.Empty;
        public string pdi_img { get; set; } = String.Empty;
        public string pdi_url { get; set; } = String.Empty;
        public string pdi_source { get; set; } = String.Empty;
        public string pdi_category {  get; set; } = String.Empty;
        public string pdi_subcat { get; set; } = String.Empty;
        public string pdi_title { get; set; } = String.Empty;
        public string[]? pdi_desc { get; set; } = null;
        public double pdi_priceUK { get; set; } = 0.0;
        public double pdi_priceEuro { get; set; } = 0.0;

        public string pdi_priceUKDisplay()
        {
            string ret = String.Empty;
            ret = pdi_priceUK.ToString("C", Cultures.UnitedKingdom);
            return ret;
        }

        public string pdi_priceEuroDisplay()
        {
            string ret = String.Empty;
            ret = pdi_priceEuro.ToString("\u20AC0.00");
            return ret;
        }
    }

    public class cPageContent
    {
        [Key]
        public int? pc_id { get; set; } = null;
        public string? pc_name { get; set; } = null;
        public string? pc_value { get; set; } = null;
        public Guid? pc_pdpd_id { get; set; } = null;
        public cPageData? pc_pd { get; set; } = null;
        public string? pd_page { get; set; } = null;

        public string pc_MimeType()
        {
            string ret = "";
            if (pc_value != null)
            {
                if (pc_value.ToLower().LastIndexOf(".jpg") > 2)
                {
                    ret = "image/jpeg";
                }
                else if (pc_value.ToLower().LastIndexOf(".png") > 2)
                {
                    ret = "image/png";
                }
                else if (pc_value.ToLower().LastIndexOf(".gif") > 2)
                {
                    ret = "image/gif";
                }
            }
            return ret;
        }

        public cPageContent()
        {
        }

    }

    public class cPageData
    {
        [Key]
        public Guid pd_id { get; set; }
        public string pd_page { get; set; } = "";
        public List<cPageContent>? pd_contents { get; set; } = null;

        public cPageData()
        {

        }

        public string? pdContent(string content_id)
        {
            string? ret = "";

            if (pd_contents != null)
            {
                var retObj = pd_contents.FirstOrDefault(s => String.Compare(s.pc_name, content_id, true) == 0);
                if (retObj != null)
                {
                    if (retObj.pc_value != null)
                    {
                        ret = retObj.pc_value;
                    }
                }
            }

            return ret;
        }

        public string? pdContentMimeType(string content_id)
        {
            string? ret = "";

            if (pd_contents != null)
            {
                var retObj = pd_contents.FirstOrDefault(s => String.Compare(s.pc_name, content_id, true) == 0);
                if (retObj != null)
                {
                    if (retObj.pc_value != null)
                    {
                        ret = retObj.pc_MimeType();
                    }
                }
            }

            return ret;
        }

        public string pd_isEditable()
        {
            string ret = pd_Editable ? "contenteditable" : string.Empty;
            return ret;
        }

        public string pd_isCallToActionHidden()
        {
            string ret = String.Empty;
            ret = "hidden";
            return ret;
        }

        private bool pd_Editable { get; set; } = false;

    }

    public static class Cultures
    {
        public static readonly CultureInfo UnitedKingdom =
            CultureInfo.GetCultureInfo("en-GB");
    }

}
