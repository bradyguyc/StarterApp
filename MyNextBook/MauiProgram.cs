using System.Net;

using Azure.Data.AppConfiguration;
using Azure.Identity;

using CommunityToolkit.Maui;

using DevExpress.Maui;
using DevExpress.Maui.Core;
using DevExpress.Maui.DataGrid;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.Configuration;
//using Sentry.Maui;
using Syncfusion.Maui.Core.Hosting;

using CommonCode.Helpers;
using CommonCode.MSALClient; // Required for PublicClientSingleton

#if ANDROID
using MyNextBook.Platforms.Android;
#endif 
using MyNextBook.Services;
using MyNextBook.Views;
using MyNextBook.ViewModels;

using OpenLibraryNET;

using System.Reflection;

using ImportSeries;
using ImportSeries.Services;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace MyNextBook
{
    public static class MauiProgram
    {
        //public const string synFusionKey = "Mzc3NjM3MkAzMjM5MmUzMDJlMzAzYjMyMzkzYks2UjQ4YzlyazBnZXB4RS9VMjlJOGFnYTNCTGNNSmhOYzZ0VVdTU0lRYVk9";
        public const string synFusionKey = "MzkzMTI2OUAzMzMwMmUzMDJlMzAzYjMzMzAzYmlpTCthR3AweEhRMmhOMW95cmM1ajMvd0huRC9KT0ZIak1HVnFrby9ONUE9";
        // Add static Services property
        public static IServiceProvider Services { get; private set; }

        public static MauiApp CreateMauiApp()
        {
            SetThemeColor.SetAppThemeColor();
            var builder = MauiApp.CreateBuilder();
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(synFusionKey);

            var assembly = typeof(MauiProgram).Assembly;
            using var stream = assembly.GetManifestResourceStream("MyNextBook.appsettings.json");
            builder.Configuration.AddJsonStream(stream);

#pragma warning disable CA1416 // Validate platform compatibility
            builder
                .ConfigureMauiHandlers(handlers =>
                {
#if ANDROID
                    handlers.AddHandler(typeof(AppShell), typeof(CustomShellRenderer));
#endif
                })
                .UseMauiApp<App>()
                .UseSkiaSharp   ()
                .UseDevExpress(useLocalization: false)
                .UseDevExpressEditors()
                .UseDevExpressCollectionView()
                .UseDevExpressControls()
                .UseDevExpressDataGrid()
                .UseDevExpressGauges()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionCore()
              /*
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
                    //options.ExperimentalMetrics = new ExperimentalMetricsOptions { EnableCode Locations = true };

                })
              */
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MD");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
#pragma warning restore CA1416 // Validate platform compatibility

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Load appsettings.json from the MAUI application's assembly
            // Ensure 'appsettings.json' is in your MyNextBook project and its Build Action is set to EmbeddedResource.
            var mauiAppAssembly = Assembly.GetExecutingAssembly();
            string resourceName = $"{mauiAppAssembly.GetName().Name}.appsettings.json"; // e.g., MyNextBook.appsettings.json

            Stream resourceStream = mauiAppAssembly.GetManifestResourceStream(resourceName);

            if (resourceStream == null)
            {
                // Fallback or more specific error handling if your appsettings.json might be named differently or missing.
                // For example, you could try a default name if the assembly name logic fails.
                // Or, if CommonCode should have its own fallback, that logic would be internal to it,
                // but the explicit initialization is preferred.
                throw new FileNotFoundException($"Embedded resource '{resourceName}' not found in assembly '{mauiAppAssembly.FullName}'. Ensure 'appsettings.json' is an EmbeddedResource in the MAUI project (MyNextBook).");
            }

            // Initialize the CommonCode singleton
            // The stream will be disposed by the ConfigurationBuilder within the singleton's constructor
            PublicClientSingleton.Initialize(resourceStream);

            var services = builder.Services;

            // Register services
            services.AddSingleton<MainPage>();
            services.AddSingleton<MainPageViewModel>();
            services.AddSingleton<ImportCSV>();
            services.AddSingleton<ImportCSVViewModel>();

            services.AddTransient<SettingsPage>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<WelcomeScreen>();
            services.AddTransient<WelcomeScreenViewModel>();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IOpenLibraryService, OpenLibraryService>(services);
            services.AddSingleton<IGetSecrets, GetSecrets>();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IPendingTransactionService, PendingTransactionService>(services);

            // Now that PublicClientSingleton is initialized, you can register its instance
            // with the DI container if other services need to resolve it.
            // Or, services can access it via PublicClientSingleton.Instance directly.
            // Example for DI:
            // services.AddSingleton(PublicClientSingleton.Instance);
            //var assembly = Assembly.GetExecutingAssembly();
            //using var stream = assembly.GetManifestResourceStream("mynextbook.appsettings.json"); // Ensure this matches your assembly name and file path

            Stream resourceStream2 = mauiAppAssembly.GetManifestResourceStream(resourceName);

            var config = new ConfigurationBuilder()
                        .AddJsonStream(resourceStream2)
                        .Build();

            builder.Configuration.AddConfiguration(config);

            // Initialize the static AppConfig
            AppConfig.Initialize(builder.Configuration);

            var app = builder.Build();

            // Set the static Services property
            Services = app.Services;

            return app;
        }
    }
}
