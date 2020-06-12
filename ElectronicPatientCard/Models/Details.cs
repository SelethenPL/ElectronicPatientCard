using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ElectronicPatientCard.Models
{
    public class Details
    {
        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime date { get; set; }

        [Display(Name = "Reason")]
        public string reason { get; set; }

        [Display(Name = "Amount")]
        public string amount { get; set; }

        public string resourceName { get; set; }

        public string id { get; set; }
    }
}