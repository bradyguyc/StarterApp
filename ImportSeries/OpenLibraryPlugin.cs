using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace ImportSeries
{
    public class OpenLibraryPlugin
    {
        private readonly HttpClient _httpClient;

        public OpenLibraryPlugin()
        {
            _httpClient = new HttpClient();
        }

        [KernelFunction, Description("Search for books on OpenLibrary")]
        public async Task<string> SearchBooks([Description("Book title, author, or ISBN to search for")] string query)
        {
            try
            {
                var encodedQuery = Uri.EscapeDataString(query);
                var url = $"https://openlibrary.org/search.json?q={encodedQuery}&limit=10";
                
                var response = await _httpClient.GetStringAsync(url);
                return response;
            }
            catch (Exception ex)
            {
                return $"Error searching OpenLibrary: {ex.Message}";
            }
        }

        [KernelFunction, Description("Get book details by OpenLibrary ID")]
        public async Task<string> GetBookById([Description("OpenLibrary book ID (e.g., OL123456M)")] string bookId)
        {
            try
            {
                var url = $"https://openlibrary.org/books/{bookId}.json";
                var response = await _httpClient.GetStringAsync(url);
                return response;
            }
            catch (Exception ex)
            {
                return $"Error getting book details: {ex.Message}";
            }
        }
    }
}