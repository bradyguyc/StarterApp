using System;
using System.Collections.Generic;
using System.Text;

namespace MyNextBook.Helpers
{
    public class StaticHelpers
    {
        //todo move to constants file
        private const string OpenLibraryUsernameKey = "OpenLibraryUsername";
        private const string OpenLibraryPasswordKey = "OpenLibraryPassword";
        public static async Task<bool> OLAreCredentialsSetAsync()
        {




            string OpenLibraryUsername =
                await SecureStorage.Default.GetAsync(OpenLibraryUsernameKey) ?? string.Empty;

            string OpenLibraryPassword =
                await SecureStorage.Default.GetAsync(OpenLibraryPasswordKey) ?? string.Empty;

            bool r = ((!string.IsNullOrEmpty(OpenLibraryUsername)) || (!string.IsNullOrEmpty(OpenLibraryPassword)));
            return r;

        }
    }

}



