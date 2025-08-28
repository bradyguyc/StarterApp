using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImportSeries;
using CommunityToolkit.Mvvm.Messaging;
using MyNextBook.Models;
using DataOLWork = OpenLibraryNET.Data.OLWorkData; // alias to the actual returned type

namespace MyNextBook.ViewModels;

public partial class BookSearchViewModel : ObservableObject
{
    private readonly IOpenLibraryService _olService;

    [ObservableProperty] private string title;
    [ObservableProperty] private string author;
    [ObservableProperty] private string keyword;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private ObservableCollection<DataOLWork> results = new();

    public BookSearchViewModel(IOpenLibraryService olService, string title, string author)
    {
        _olService = olService;
        this.title = title;
        this.author = author;
        keyword = string.Empty;
        _ = InitialSearchAsync();
    }

    private async Task InitialSearchAsync() => await SearchAsync();

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (isBusy) return;
        try
        {
            IsBusy = true;
            Results.Clear();
            var works = await _olService.OLSearchForBook(Title ?? string.Empty, Author ?? string.Empty, string.Empty, string.Empty);
            foreach (var w in works)
                Results.Add(w); // w already DataOLWork
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task SelectWork(DataOLWork work)
    {
        if (work != null && !string.IsNullOrWhiteSpace(work.Key))
        {
            WeakReferenceMessenger.Default.Send(new BookSelectedMessage(work.Key));
            Shell.Current.Navigation.PopAsync(true);
        }
        return Task.CompletedTask;
    }
}
