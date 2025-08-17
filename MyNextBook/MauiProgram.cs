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
using Microsoft.Maui.Controls; // for Shell
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
        // Add static Services property
        public static IServiceProvider Services { get; private set; }

        public static MauiApp CreateMauiApp()
        {
            SetThemeColor.SetAppThemeColor();
            var builder = MauiApp.CreateBuilder();
            
            // Load configuration first
            var assembly = typeof(MauiProgram).Assembly;
            var resourceName = $"{assembly.GetName().Name}.appsettings.json";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            
            if (stream != null)
            {
                var configBuilder = new ConfigurationBuilder()
                    .AddJsonStream(stream);
                var config = configBuilder.Build();
                
                // Get Syncfusion license key from configuration and register it before configuring Syncfusion
                var syncfusionKey = config["Syncfusion:LicenseKey"];
                
                // Register Syncfusion license if a valid key is provided
                if (!string.IsNullOrEmpty(syncfusionKey) && syncfusionKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
                {
                    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionKey);
                }
                
                // Add the configuration to the builder
                builder.Configuration.AddConfiguration(config);
            }

#pragma warning disable CA1416 // Validate platform compatibility
            builder
                .ConfigureMauiHandlers(handlers =>
                {
#if ANDROID
                    // Register the Android compatibility renderer for Shell (not AppShell)
                    handlers.AddHandler(typeof(AppShell), typeof(CustomShellRenderer));
#endif
                })
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .UseDevExpress(useLocalization: false)
                .UseDevExpressEditors()
                .UseDevExpressCollectionView()
                .UseDevExpressControls()
                .UseDevExpressDataGrid()
                .UseDevExpressGauges()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionCore()
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

            // Load appsettings.json from the MAUI application's assembly again for PublicClientSingleton
            var mauiAppAssembly = Assembly.GetExecutingAssembly();
            string resourceNameForSingleton = $"{mauiAppAssembly.GetName().Name}.appsettings.json";

            Stream resourceStreamForSingleton = mauiAppAssembly.GetManifestResourceStream(resourceNameForSingleton);

            if (resourceStreamForSingleton == null)
            {
                throw new FileNotFoundException($"Embedded resource '{resourceNameForSingleton}' not found in assembly '{mauiAppAssembly.FullName}'. Ensure 'appsettings.json' is an EmbeddedResource in the MAUI project (MyNextBook).");
            }

            // Initialize the CommonCode singleton
            PublicClientSingleton.Initialize(resourceStreamForSingleton);

            var services = builder.Services;

            // Register services
            services.AddSingleton<MainPage>();
            services.AddSingleton<MainPageViewModel>();
            services.AddScoped<ImportCSV>();
            services.AddScoped<ImportCSVViewModel>();

            services.AddScoped<SettingsPage>();
            services.AddScoped<SettingsViewModel>();
            services.AddSingleton<WelcomeScreen>();
            services.AddSingleton<WelcomeScreenViewModel>();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IOpenLibraryService, OpenLibraryService>(services);
            services.AddSingleton<IGetSecrets, GetSecrets>();
            Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<IPendingTransactionService, PendingTransactionService>(services);

            // Initialize the static AppConfig
            AppConfig.Initialize(builder.Configuration);

            var app = builder.Build();

            // Set the static Services property
            Services = app.Services;

            return app;
        }
    }
}
