using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ElectronicPatientCard.Models
{
    public class PatientEdit
    {
        [Display(Name = "Surname")]
        
        public string surname { get; set; }

        [Display(Name = "birthDate")]
        public string birthDate { get; set; }

        [Display(Name = "maritalStatus")]
        public string mStatus { get; set; }

        public string id { get; set; }
    }
}