using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;
using OpenLibraryNET;
using OpenLibraryNET.Data;
using OpenLibraryNET.OLData;

namespace MyNextBook.Models
{
      public partial class Series : ObservableObject
    {
        [ObservableProperty] public OLListData seriesData;
        [ObservableProperty] public ObservableCollection<OLEditionData> editions;
       
        [ObservableProperty] public ObservableCollection<OLWork> works;
        public OLSeedData[] seeds;

    }
}
