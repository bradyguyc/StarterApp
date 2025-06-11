using System;
using System.Collections.Generic;
using System.Text;

namespace MyNextBook.Models
{
    public class ImportSeriesInfo
    {
        public string Name { get; set; }
        public List<string> Books { get; set; } = new List<string>();
    }
}
