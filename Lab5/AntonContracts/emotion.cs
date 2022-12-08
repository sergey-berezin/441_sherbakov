using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AntonContracts
{
    public class emotion
    {
        [Key]
        public int emotionID { get; set; }
        [ForeignKey(nameof(photoLine))]
        public int photoLineId { get; set; }
        public double emoOdds { get; set; }
        public string emoName { get; set; }
        public override string ToString()
        {
            return "  " + emoName + ": " + String.Format("{0:0.000}", emoOdds) + "\n";
        }
    }
}
