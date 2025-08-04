using System;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Newtonsoft.Json.Linq;

namespace ImportSeries
{
    public class WikipediaPlugin
    {
        private readonly HttpClient _httpClient = new HttpClient();

        [KernelFunction, Description("Searches Wikipedia for a given series name and author(s).")]
        public async Task<string> Search(
            [Description("The name of the series to search for.")] string seriesName = null,
            [Description("The author(s) of the series, can be a single author or comma-separated list of authors.")] string authors = null)
        {
            if (string.IsNullOrWhiteSpace(seriesName) && string.IsNullOrWhiteSpace(authors))
            {
                return "Please provide a series name or author(s) to search.";
            }

            // Construct search term using series name and authors instead of book titles
            var searchTerm = string.Empty;
            if (!string.IsNullOrWhiteSpace(seriesName) && !string.IsNullOrWhiteSpace(authors))
            {
                searchTerm = $"Book Series: {seriesName} author {authors}";
            }
            else if (!string.IsNullOrWhiteSpace(seriesName))
            {
                searchTerm = $"Book Series: {seriesName}";
            }
            else
            {
                searchTerm = $"Author: {authors} book series";
            }

            var requestUrl = $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(searchTerm)}&format=json";

            try
            {
                var response = await _httpClient.GetStringAsync(requestUrl);
                var jsonResponse = JObject.Parse(response);
                var searchResults = jsonResponse["query"]?["search"];

                if (searchResults != null && searchResults.HasValues)
                {
                    // Return the snippet of the first search result
                    return searchResults[0]["snippet"]?.ToString() ?? "No relevant information found.";
                }

                return "No results found on Wikipedia.";
            }
            catch (Exception ex)
            {
                return $"An error occurred while searching Wikipedia: {ex.Message}";
            }
        }
    }
}
