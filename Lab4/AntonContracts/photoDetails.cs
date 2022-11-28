using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AntonContracts
{
    public class photoDetails
    {
        [Key]
        public int detailsId {get; set; }
        [ForeignKey(nameof(photoLine))]
        public int photoLineId { get; set; }
        public byte[] imageBLOB { get; set; }
    }
}
