using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Extensions.Logging;
using MyNextBook.Helpers;

namespace MyNextBook.Services
{
    public interface IOpenLibraryService
    {
         Task<string> GetOpenLibraryUsernameAsync();
    }

    public class OpenLibraryService: IOpenLibraryService
    {
        private readonly ILogger<OpenLibraryService> _logger;
        public OpenLibraryService(ILogger<OpenLibraryService> logger)
        {
            _logger = logger;
        }

        public async Task<string> GetOpenLibraryUsernameAsync()
        {
            try
            {
                string OpenLibraryUsername = SecureStorage.Default.GetAsync(Constants.OpenLibraryUsernameKey).Result ?? string.Empty;
                return OpenLibraryUsername;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving OpenLibrary username from secure storage.");
            }
            return "";
        }
    }
}
