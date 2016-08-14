using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PassToBusBot.Models
{
    public class RideRequest
    {
        public City FromCity { get; set; }
        public City ToCity { get; set; }
        public DateTime? Date { get; set; }
    }
}