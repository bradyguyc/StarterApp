using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;
using MyNextBook.Services;
using OpenLibraryNET;
using OpenLibraryNET.Data;
using OpenLibraryNET.OLData;

namespace MyNextBook.Models
{
    public record OlWorkDataExt : OLWorkData
    {
        public string Status;
        public string OpenImageUrl
        {
            get
            {
                if (CoverIDs != null && CoverIDs.Count > 0)
                {
                    int coverId = CoverIDs[0];
                    string size = "M";
                    return $"https://covers.openlibrary.org/b/id/{coverId}-{size}.jpg";
                }
                else
                {
                    string key = "OLID";
                    string value = ID;
                    string size = "M";
                    return $"https://covers.openlibrary.org/b/{key}/{value}-{size}.jpg";
                }

            }
        }

        public string authors
        {
            get
            {
                if (OpenLibraryService.authorsList == null || AuthorKeys == null)
                    return string.Empty;

                var filteredAuthors = OpenLibraryService.authorsList
                    .Where(author => AuthorKeys.Contains(author.Key))
                    .Select(author => author.Name);

                return string.Join(", ", filteredAuthors);
            }
        }
    }
    public partial class Series : ObservableObject
    {
        [ObservableProperty]
        private OLListData seriesData;

        [ObservableProperty]
        private ObservableCollection<OLEditionData> editions = new();

        [ObservableProperty]
        public ObservableCollection<OlWorkDataExt> works = new();

        public OLSeedData[] seeds;


        public int BookCount => (editions?.Count ?? 0) + (works?.Count ?? 0);
        [ObservableProperty] private int userBooksRead = 2;

        public Series()
        {
            editions.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(BookCount));
            };
            works.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(BookCount));
            };
        }





        public string OpenImageUrl
        {
            get
            {
                if (works != null && works.Count > 0 && !string.IsNullOrEmpty(works[0].ID))
                {
                    return works[0].OpenImageUrl;
                }
                return string.Empty;
            }
        }
       
        public string authors
        {
          
            get
            {
                if (works != null && works.Count > 0 && !string.IsNullOrEmpty(works[0].ID))
                {
                    return works[0].authors;
                }
                return string.Empty;

            }
        }
    }
}
