using Foundation;
using CommonCode.MSALClient;
using Microsoft.Identity.Client;

using UIKit;

namespace MyNextBook
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            // configure platform specific params
            PlatformConfig.Instance.RedirectUri = $"msal{PublicClientSingleton.Instance.MSALClientHelper.AzureAdConfig.ClientId}://auth";

            // Initialize MSAL and platformConfig is set
            _ = Task.Run(async () => await PublicClientSingleton.Instance.MSALClientHelper.InitializePublicClientAppAsync());

            return base.FinishedLaunching(application, launchOptions);
        }
    }
}