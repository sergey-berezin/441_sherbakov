using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AntonContracts
{
    public class photoLine //for binding list of myClass in .xaml
    {
        [Key]
        public int photoId { get; set; }
        public string fileName { get; set; }
        public int imgHashCode { get; set; }
        public photoDetails Details { get; set; }
        public ICollection<emotion> emotions { get; set; }
        public photoLine()
        {
            emotions = new List<emotion>();
        }
        public string option_emotion { get; set; } = "Calculations in process...";
        public void CreateHashCode (byte[] img)
        {
            int hc = img.Length;
            foreach (int val in img)
            {
                hc = unchecked(hc * 314159 + val);
            }
            imgHashCode = hc;
        }
    }
}
