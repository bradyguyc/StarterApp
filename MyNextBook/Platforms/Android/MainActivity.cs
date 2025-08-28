using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

using Microsoft.Identity.Client;

using CommonCode.MSALClient;

namespace MyNextBook
{
    // Apply the app (Material) theme immediately so no view inflates under the splash theme missing TextAppearance attrs
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            // No need for base.SetTheme() now; Activity already has Maui.MainTheme
            base.SetTheme(Resource.Style.Theme_Material3_DayNight_NoActionBar);

            base.OnCreate(savedInstanceState);

            // configure platform specific params
            PlatformConfig.Instance.RedirectUri = $"msal{PublicClientSingleton.Instance.MSALClientHelper.AzureAdConfig.ClientId}://auth";
            PlatformConfig.Instance.ParentWindow = this;

            _ = Task.Run(async () => await PublicClientSingleton.Instance.MSALClientHelper.InitializePublicClientAppAsync());
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);
            AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(requestCode, resultCode, data);
        }
    }
}
