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
using StarterApp.Services;
using StarterApp.ViewModels;

using Syncfusion.Maui.Core.Hosting;

using static System.Net.WebRequestMethods;

namespace StarterApp
{
    public static class MauiProgram
    {
        public const string synFusionKey = "Mzc3NjM3MkAzMjM5MmUzMDJlMzAzYjMyMzkzYks2UjQ4YzlyazBnZXB4RS9VMjlJOGFnYTNCTGNNSmhOYzZ0VVdTU0lRYVk9";

        public static MauiApp CreateMauiApp()
        {
            SetThemeColor();
            var builder = MauiApp.CreateBuilder();
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(synFusionKey);

            var assembly = typeof(MauiProgram).Assembly;
            using var stream = assembly.GetManifestResourceStream("StarterApp.appsettings.json");
            builder.Configuration.AddJsonStream(stream);

            builder
                .UseMauiApp<App>()
                .UseDevExpress()
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
                    options.ExperimentalMetrics = new ExperimentalMetricsOptions { EnableCodeLocations = true };

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
            services.AddTransient<MainPageViewModel>();
            services.AddScoped<IGetSecrets, GetSecrets>();
            // Register scoped services with their configuration
         

            var app = builder.Build();
            
           

            return app;
        }

        static void SetThemeColor()
        {
            // Define the default theme seed color
            ThemeSeedColor themeSeedColor = ThemeSeedColor.DeepSeaBlue;

            // Check if a custom color theme is enabled
            bool isCustomColorTheme = Preferences.Default.Get("isCustomColorTheme", false);

            // Get the custom color or theme color from preferences
            string customColor = Preferences.Default.Get("CustomColorTheme", "#FF006C50");
            string themeColor = Preferences.Default.Get("themeColor", themeSeedColor.ToString());

            // Determine the theme color
            Theme theme;

            if (isCustomColorTheme || themeColor == "Custom")
            {
                // Attempt to parse the custom color string to a Color object
                if (Color.TryParse(customColor, out Color parsedColor))
                {
                    theme = new Theme(parsedColor);
                }
                else
                {
                    // Fallback to the default theme seed color if parsing fails
                    themeSeedColor = ThemeSeedColor.TealGreen;
                    theme = new Theme(themeSeedColor);
                }
            }
            else
            {
                // Attempt to parse the theme color string to a ThemeSeedColor enum
                if (Enum.TryParse(themeColor, out ThemeSeedColor parsedThemeSeedColor))
                {
                    themeSeedColor = parsedThemeSeedColor;
                    theme = new Theme(themeSeedColor);
                }
                else
                {
                    // Fallback to the default theme seed color if parsing fails
                    themeSeedColor = ThemeSeedColor.TealGreen;
                    theme = new Theme(themeSeedColor);
                    // Save the default value back to preferences
                    Preferences.Default.Set("themeColor", themeSeedColor.ToString());
                }
            }

            // Set the theme manager's theme
            ThemeManager.UseAndroidSystemColor = false;
            ThemeManager.Theme = theme;
        }
    }
}
