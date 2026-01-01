using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartNest.Server.Models.postgres
{
    [Table("sensordata", Schema = "public")]
    public partial class Sensordatum
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        [Required]
        public string userid { get; set; }

        [Required]
        public string topic { get; set; }

        [Required]
        public string payload { get; set; }

        public DateTime timestamp { get; set; }
        
        public double temperature { get; set; }

        public double humidity { get; set; }

        public double dust { get; set; }

        public int? chickcount { get; set; } 

        [Required]
        [Column("deviceid")]
        public string deviceid { get; set; }

        [NotMapped]
        public device device { get; set; }   
    }
}
