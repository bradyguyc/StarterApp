using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

namespace MyNextBook.Models
{
    public partial  class ImportSeriesResults : ObservableObject
    {
        [ObservableProperty] string seriesName;
        [ObservableProperty] int booksMatched;
        [ObservableProperty] int booksNotFound;
        public int TotalBooks { get { return BooksMatched + BooksNotFound; }  }

    }
}