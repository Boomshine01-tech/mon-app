using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;

namespace SmartNest.Server.Models
{
    public partial class ApplicationRole : IdentityRole
    {
        [JsonIgnore]
        public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();


        public string? TenantId { get; set; }

        [ForeignKey("TenantId")]
        public ApplicationTenant ApplicationTenant { get; set; } = null!;
    }


    [Table("AspNetTenants")]
    public partial class ApplicationTenant
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Id  { get; set; } = Guid.NewGuid().ToString();

        public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();

        public ICollection<ApplicationRole>? Roles { get; set; } = new List<ApplicationRole>();

        public string Name { get; set; } = null!;

        public string Hosts { get; set; }   = null!;
    }
}