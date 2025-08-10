using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using OpenLibraryNET;
using OpenLibraryNET.Data;
using OpenLibraryNET.OLData;

namespace ImportSeries.Models
{
    public partial class OlWorkDataExt : ObservableObject
    {
        // Copy the properties from OLWorkData that you need
        public string ID { get; set; }
        public string Key { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public IReadOnlyList<string> Subjects { get; set; }
        public IReadOnlyList<string> AuthorKeys { get; set; }
        public IReadOnlyList<int> CoverIDs { get; set; }

        [ObservableProperty] private string status = "";

        [ObservableProperty] private int displayOrder = 0;

        [ObservableProperty] private IList<string> chipStatusGroup;
        //[ObservableProperty] private string selectedStatus;

        public OlWorkDataExt()
        {
            ChipStatusGroup = new List<string> { "To Read", "Reading", "Read" };
            Subjects = new List<string>();
            AuthorKeys = new List<string>();
            CoverIDs = new List<int>();
        }
        [RelayCommand]
        private async Task UpdateStatus(object parameter)
        {
            if (parameter is OlWorkDataExt work)
            {
             
                Debug.WriteLine($"SeriesClasss: UpdateStatus status: {work.Status} key: {work.Key}");
                
                // Use messaging to notify the MainPageViewModel
                WeakReferenceMessenger.Default.Send(new StatusUpdateMessage(work, work.Status));
            }
        
        
        


        }
        // Constructor to create from OLWorkData
        public OlWorkDataExt(OLWorkData workData) : this()
        {
            if (workData != null)
            {
                ID = workData.ID;
                Key = workData.Key;
                Title = workData.Title;
                Description = workData.Description;
                Subjects = workData.Subjects ?? new List<string>();
                AuthorKeys = workData.AuthorKeys ?? new List<string>();
                CoverIDs = workData.CoverIDs ?? new List<int>();
            }
        }

        public string OpenImageUrl
        {
            get
            {
                if (CoverIDs != null && CoverIDs.Count > 0)
                {
                    int coverId = CoverIDs[0];
                    string size = "M";
                    string url = $"https://covers.openlibrary.org/b/id/{coverId}-{size}.jpg";
                    return url;
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
        [ObservableProperty] private OLListData seriesData;

        [ObservableProperty] private ObservableCollection<OLEditionData> editions = new();

        [ObservableProperty] public ObservableCollection<OlWorkDataExt> works = new();

        public OLSeedData[] seeds;

        [ObservableProperty] int displayOrder = 0;
        public int BookCount => (Editions?.Count ?? 0) + (Works?.Count ?? 0);
        [ObservableProperty] private int userBooksRead;
        private readonly IOpenLibraryService _openLibraryService;

        public Series(IOpenLibraryService openLibraryService)
        {
            _openLibraryService = openLibraryService;
            editions.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(BookCount));
                OnPropertyChanged(nameof(UserBooksRead));
            };
            Works.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(BookCount));
                if (e.NewItems != null)
                {
                    foreach (OlWorkDataExt work in e.NewItems.Cast<OlWorkDataExt>())
                    {
                        if (_openLibraryService != null)
                        {
                            work.Status = _openLibraryService.OLGetBookStatus(work.Key);
                        }
                    }
                }
                OnPropertyChanged(nameof(UserBooksRead));
            };
        }

        public string OpenImageUrl
        {
            get
            {
                if (Works != null && Works.Count > 0)
                {
                    foreach (var work in Works)
                    {
                        if (!string.IsNullOrWhiteSpace(work?.OpenImageUrl))
                        {
                            if (work.authors.Count() > 0)
                            {
                                return work.OpenImageUrl;
                            }
                        }
                    }
                }
                return string.Empty;
            }
        }

        public string authors
        {
            get
            {
                if (Works != null && Works.Count > 0 && !string.IsNullOrEmpty(Works[0].ID))
                {
                    return Works[0].authors;
                }
                return string.Empty;
            }
        }
    }
}
