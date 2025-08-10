using ImportSeries;

namespace MyNextBook;

public partial class App : Application
{
    private readonly IOpenLibraryService _openLibraryService;

    public App(IOpenLibraryService openLibraryService)
    {
        InitializeComponent();

        MainPage = new AppShell();
        _openLibraryService = openLibraryService;

        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    protected override void OnStart()
    {
        base.OnStart();
        // Run processor in the background
        _ = Task.Run(() => _openLibraryService.ProcessPendingTransactionsAsync());
    }

    private void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            // When connectivity is restored, run the processor
            _ = Task.Run(() => _openLibraryService.ProcessPendingTransactionsAsync());
        }
    }
}