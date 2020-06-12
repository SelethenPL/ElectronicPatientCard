using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace ElectronicPatientCard.Models
{
    public class DataPoint
    {
        [Display(Name = "Date")]
        public string label = "";

        [Display(Name = "Temperature")]
        [DataMember(Name = "y")]
        public double y = 0;

        public DataPoint(string label, double y)
        {
            this.label = label;
            this.y = y;
        }
    }
}
