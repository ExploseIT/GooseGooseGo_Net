
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace GooseGooseGo_Net.ef
{
    [Table("tblUserRole")]
    public class ent_userrole
    {
       [Key]
       public int userRoleId { get; set; }
       public int userRoleUserId { get; set; }
       public string? userRoleKey { get; set; }
       public string? userRoleDescription { get; set; }
    }
}

