using System;
using System.Collections.Generic;
using System.Text;

namespace ImportSeries.Models
{
    public class BookInfo

    {
        public string? Name { get; set; }
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? ISBN { get; set; }
        public string? ISBN13 { get; set; } // Added ISBN-13 field
        public string? SeriesName { get; set; }
        public string? BookOrder { get; set; }
        public string? SeriesOrder { get; set; } // Added SeriesOrder property
        public string? Publisher { get; set; } // Added publisher field
        public string? PreviousWork { get; set; } // Added previous work field
        public string? SubsequentWork { get; set; } // Added next work field
        public string? ReleaseDate { get; set; } // Added Open Library key field

    }
}

