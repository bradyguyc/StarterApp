using System.Net;

using Azure.Data.AppConfiguration;
using Azure.Identity;

using CommunityToolkit.Maui;

using DevExpress.Maui;
using DevExpress.Maui.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Configuration;
using Sentry.Maui;
using Syncfusion.Maui.Core.Hosting;
using CommonCode.Helpers;
#if ANDROID
using MyNextBook.Platforms.Android;
#endif 
using MyNextBook.Services;
using MyNextBook.Views;
using MyNextBook.ViewModels;
using OpenLibraryNET;
using static System.Net.WebRequestMethods;

namespace MyNextBook
{
    public static class MauiProgram
    {
        public const string synFusionKey = "Mzc3NjM3MkAzMjM5MmUzMDJlMzAzYjMyMzkzYks2UjQ4YzlyazBnZXB4RS9VMjlJOGFnYTNCTGNNSmhOYzZ0VVdTU0lRYVk9";

        public static MauiApp CreateMauiApp()
        {
            SetThemeColor.SetAppThemeColor();
            var builder = MauiApp.CreateBuilder();
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(synFusionKey);

            var assembly = typeof(MauiProgram).Assembly;
            using var stream = assembly.GetManifestResourceStream("MyNextBook.appsettings.json");
            builder.Configuration.AddJsonStream(stream);

            builder
                .ConfigureMauiHandlers(handlers =>
                {
#if ANDROID
                    handlers.AddHandler(typeof(AppShell), typeof(CustomShellRenderer));
#endif
                })
                .UseMauiApp<App>()
                .UseDevExpress(useLocalization: false)
                .UseDevExpressEditors()
                .UseDevExpressCollectionView()
                .UseDevExpressControls()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionCore()
              
                .UseSentry(options =>
                {
                    // The DSN is the only required setting.
                    options.Dsn = "https://41990b90035138cb0a9dbdb374ca61e2@o4507073550155776.ingest.us.sentry.io/4507073559396352";

                    // Use debug mode if you want to see what the SDK is doing.
                    // Debug messages are written to stdout with Console.Writeline,
                    // and are viewable in your IDE's debug console or with 'adb logcat', etc.
                    // This option is not recommended when deploying your application.
#if DEBUG
                    options.Debug = false;
                    options.DiagnosticLevel = SentryLevel.Debug;
#endif
                    // Set TracesSampleRate to 1.0 to capture 100% of transactions for performance monitoring.
                    // We recommend adjusting this value in production.
                    options.TracesSampleRate = 1.0;
                    //options.AttachScreenshot = true;

                    // Other Sentry options can be set here.
                    //options.ExperimentalMetrics = new ExperimentalMetricsOptions { EnableCodeLocations = true };

                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MD");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // services.AddAzureAppConfiguration(Environment.GetEnvironmentVariable("ConnectionString"));
      
#if DEBUG
            builder.Logging.AddDebug();
#endif
            var services = builder.Services;

            // Register services
            services.AddSingleton<MainPage>();
            services.AddSingleton<MainPageViewModel>();

            services.AddTransient<SettingsPage>();
            services.AddTransient<SettingsViewModel>();
            services.AddScoped<IOpenLibraryService, OpenLibraryService>();
            services.AddScoped<IGetSecrets, GetSecrets>();
       
            // Register scoped services with their configuration


            var app = builder.Build();



            return app;
        }


    }
}
