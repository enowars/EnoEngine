using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models
{
    public class Round
    {
        public long Id { get; set; }
        public DateTime Begin { get; set; }
        public DateTime Quarter2 { get; set; }
        public DateTime Quarter3 { get; set; }
        public DateTime Quarter4 { get; set; }
        public DateTime End { get; set; }
    }
}
