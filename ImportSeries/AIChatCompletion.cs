using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Plugins.Web.Google;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using System.Diagnostics;


namespace ImportSeries
{
    public class AIChatCompletion
    {
        public string errorMessage { get; set; }
        private readonly string _provider;
        private readonly string _deploymentName;
        private readonly string _endPoint;
        private readonly string _aiKey;
        private readonly string[] _searchProviders;
        private readonly string _searchAPIKey;
        private readonly string _searchEngineId;

        public AIChatCompletion(string provider, string deploymentName, string endPoint, string aiKey, string searchProvider, string searchAPIKey, string searchEngineId)
        {
            _provider = provider;
            _deploymentName = deploymentName;
            _endPoint = endPoint;
            _aiKey = aiKey;
            _searchProviders = searchProvider?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? new string[0];
            _searchAPIKey = searchAPIKey;
            _searchEngineId = searchEngineId;
        }

        public async Task<string> FillViaAI(string jsonData, string seriesName = null, string seriesBooks = null, List<string> uniqueAuthors = null)
        {
            try
            {
                // Initialize Semantic Kernel
                var builder = Kernel.CreateBuilder();

                if (_provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
                {
                    // Configure HttpClient with 10-minute timeout for Azure OpenAI
                    var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(10);

                    builder.AddAzureOpenAIChatCompletion(
                        _deploymentName ?? throw new InvalidOperationException("Azure OpenAI Deployment Name not found"),
                        _endPoint ?? throw new InvalidOperationException("Azure OpenAI Endpoint not found"),
                        _aiKey ?? throw new InvalidOperationException("Azure OpenAI API key not found"),
                        httpClient: httpClient
                    );
                }
                else
                {
                    // Configure HttpClient with 10-minute timeout for regular OpenAI
                    var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromMinutes(10);

                    builder.AddOpenAIChatCompletion(
                        _deploymentName, // For OpenAI, this is the model ID
                        _aiKey ?? throw new InvalidOperationException("OpenAI API key not found"),
                        httpClient: httpClient
                    );
                }

                var kernel = builder.Build();

                // Add search providers
                foreach (var provider in _searchProviders)
                {
                    if (provider.Trim().Equals("google", StringComparison.OrdinalIgnoreCase))
                    {
#pragma warning disable SKEXP0050
                        var googleConnector = new GoogleConnector(
                                apiKey: _searchAPIKey ?? throw new InvalidOperationException("Google API key not found"),
                                searchEngineId: _searchEngineId ?? throw new InvalidOperationException("Google Search Engine ID not found"));
                        var searchPlugin = new WebSearchEnginePlugin(googleConnector);
                        kernel.Plugins.AddFromObject(searchPlugin, "Google");
#pragma warning restore SKEXP0050
                    }
                    else if (provider.Trim().Equals("bing", StringComparison.OrdinalIgnoreCase))
                    {
#pragma warning disable SKEXP0051
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                        var bingConnector = new BingConnector(_searchAPIKey ?? throw new InvalidOperationException("Bing API key not found"));
                        var webSearchPlugin = new WebSearchEnginePlugin(bingConnector);
                        kernel.Plugins.AddFromObject(webSearchPlugin, "Bing");
#pragma warning restore SKEXP0050 
#pragma warning restore SKEXP0051
                    }
                    else if (provider.Trim().Equals("openlibrary", StringComparison.OrdinalIgnoreCase))
                    {
                        var openLibraryPlugin = kernel.ImportPluginFromType<OpenLibraryPlugin>("OpenLibrary");
                    }
                    else if (provider.Trim().Equals("wikipedia", StringComparison.OrdinalIgnoreCase))
                    {
#pragma warning disable SKEXP0052
                        kernel.ImportPluginFromObject(new WikipediaPlugin(), "Wikipedia");
#pragma warning restore SKEXP0052
                    }
                }

                var hasGoogle = _searchProviders.Any(p => p.Trim().Equals("google", StringComparison.OrdinalIgnoreCase));
                var hasBing = _searchProviders.Any(p => p.Trim().Equals("bing", StringComparison.OrdinalIgnoreCase));
                var hasOpenLibrary = _searchProviders.Any(p => p.Trim().Equals("openlibrary", StringComparison.OrdinalIgnoreCase));
                var hasWikipedia = _searchProviders.Any(p => p.Trim().Equals("wikipedia", StringComparison.OrdinalIgnoreCase));

                var searchQueries = new List<string>();
                if (hasGoogle) searchQueries.Add("{{Google.Search $jsonData}}");
                if (hasBing) searchQueries.Add("{{Bing.Search $jsonData}}");
                if (hasOpenLibrary) searchQueries.Add("{{OpenLibrary.SearchBooks $jsonData}}");
                if (hasWikipedia && !string.IsNullOrEmpty(seriesName)) 
                {
                    // Use seriesName and authors instead of seriesBooks with named parameters
                    var authorsString = uniqueAuthors != null && uniqueAuthors.Any() 
                        ? string.Join(", ", uniqueAuthors) 
                        : "";
                    searchQueries.Add("{{Wikipedia.Search seriesName=$seriesName authors=$authorsString}}");
                }

                var searchPrefix = searchQueries.Any() ? string.Join(" ", searchQueries) + " " : "";
                
                var prompt = searchPrefix + @"
You are an expert librarian and book data specialist. I will provide the name of a book series and the book titles in the series. 

1. Determine if there are any missing books in the series. If so provide the isbn_13 and isbn_10 for the missing books.
2.  for books already in the json:
    a. only update the seriesOrder 
    b. update the isbn13 if it is missing or invalid where invalid would be a value that is not 13 digits long. 
       Onnly provide the isbn13 if it is the correct isbn value that matches to the same book as the isbn10 if isbn10 is provided.  
    c. If neither isbn10 or 13 is provided provide the best isbn10 and 13 edition values for the book that is the first published book in english.
    d. if isbn infomration is updated or added in the ImportNotes fields add the text 'ISBNUpdated'
3. add any missing books in the series with information for each field in the json that you can find, do not make  up information.
4. Add an ImportNotes field summarizing your changes.
5. Add an ImportStatus field with one of: 'Enhanced', 'Failed', 'Added', or 'RecommendDelete' for each book in the provided json.
6. Do not include any notes outside the JSON.
7. Return only valid JSON, preserving the original structure with your enhancements.

Here are the books in the series {{$seriesName}}:
{{$seriesBooks}}

Return the enhanced JSON: {{$jsonData}}";

                var arguments = new KernelArguments(new OpenAIPromptExecutionSettings
                {
                    MaxTokens = 16384,
                    Temperature = 0.3,
                    TopP = 0.9
                })
                {
                    { "jsonData", jsonData },
                    { "seriesName", seriesName ?? "" },
                    { "seriesBooks", seriesBooks ?? "" },
                    { "authorsString", uniqueAuthors != null && uniqueAuthors.Any() ? string.Join(", ", uniqueAuthors) : "" }
                };
                
                // Execute the AI request with the extended timeout
                var response = await kernel.InvokePromptAsync(prompt, arguments);
                Debug.WriteLine($"[Info]AI response received.\n{response.GetValue<string>()}");

                return response.GetValue<string>() ?? string.Empty;
            }
            catch (InvalidOperationException ex)
            {
                errorMessage = $"AI enhancement failed due to invalid configuration: {ex.Message}";
                throw new Exception($"Failed to enhance data via AI due to invalid configuration: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                errorMessage = $"AI enhancement failed: {ex.Message}";
                throw new Exception($"Failed to enhance data via AI: {ex.Message}", ex);
            }
        }
    }
}
