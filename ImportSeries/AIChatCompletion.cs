using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Plugins.Web.Google;
using Microsoft.SemanticKernel.Plugins.Web;

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

        public async Task<string> FillViaAI(string jsonData)
        {
            try
            {
                // Initialize Semantic Kernel
                var builder = Kernel.CreateBuilder();

                if (_provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
                {
                    builder.AddAzureOpenAIChatCompletion(
                        _deploymentName ?? throw new InvalidOperationException("Azure OpenAI Deployment Name not found"),
                        _endPoint ?? throw new InvalidOperationException("Azure OpenAI Endpoint not found"),
                        _aiKey ?? throw new InvalidOperationException("Azure OpenAI API key not found")
                    );
                }
                else
                {
                    builder.AddOpenAIChatCompletion(
                        _deploymentName, // For OpenAI, this is the model ID
                        _aiKey ?? throw new InvalidOperationException("OpenAI API key not found")
                    );
                }

                var kernel = builder.Build();

                // Add search providers
                foreach (var provider in _searchProviders)
                {
                    if (provider.Equals("google", StringComparison.OrdinalIgnoreCase))
                    {
#pragma warning disable SKEXP0050
                        var textSearch = new GoogleTextSearch(
                                searchEngineId: _searchEngineId ?? throw new InvalidOperationException("Google Search Engine ID not found"),
                                apiKey: _searchAPIKey ?? throw new InvalidOperationException("Google API key not found"));
#pragma warning restore SKEXP0050
                        var searchPlugin = textSearch.CreateWithSearch("GoogleSearch");
                        kernel.Plugins.Add(searchPlugin);
                    }
                    else if (provider.Equals("openlibrary", StringComparison.OrdinalIgnoreCase))
                    {
                        var openLibraryPlugin = kernel.ImportPluginFromType<OpenLibraryPlugin>("OpenLibrary");
                    }
                }

                // Create the prompt for enhancing book/series data
                string query = $@"
You are an expert librarian and book data specialist. I will provide you with book series data in JSON format. 
Please enhance this data by:

1. Adding missing publication information (publication dates, publishers, etc.)
2. Standardizing author names and series information
3. Adding ISBN numbers if missing
4. Correcting any inconsistencies in the data
5. Adding Open Library IDs (OLID) if you can find them, do not provide examples, only provide the information if you can find the accurate and correct id.
6. Add missing books from the series using the same json format only providing data that you can find and is accurate, don't make data up or provide examples leave empty if you can't find the correct value.
7. The notes that you provide at the end of the query indicating what you did include in a new field in the json called ImportNotes.
8. Add series order, what order the book is in the series. 
9. Add a field called ImportNotes with any notes you have about the data, this will be added to the json and will be used to determine if the data is accurate and complete.
10. Add a field called ImportStatus with a value of 'Enhanced' if you enhanced the data or 'Failed' if you failed to enhance the data. Added if the book was added to the list, and RecommendDelete if you think the book does not belong in that series.
11.  Do not add any notes at the end the entire response should be valid json.

Use book title, author, and series name to search for additional information.

Here is the book series data:
{jsonData}

Please return the enhanced data in the  JSON format, maintaining the original structure but with additional/corrected information. 
If you need to search for information about these books, please do so to provide accurate data.

Enhanced JSON:";

                var hasGoogle = _searchProviders.Any(p => p.Equals("google", StringComparison.OrdinalIgnoreCase));
                var hasOpenLibrary = _searchProviders.Any(p => p.Equals("openlibrary", StringComparison.OrdinalIgnoreCase));
                
                var prompt = "{{$query}}";
                if (hasGoogle && hasOpenLibrary)
                {
                    prompt = "{{GoogleSearch.Search $jsonData}} {{OpenLibrary.SearchBooks $jsonData}} {{$query}}";
                }
                else if (hasGoogle)
                {
                    prompt = "{{GoogleSearch.Search $jsonData}} {{$query}}";
                }
                else if (hasOpenLibrary)
                {
                    prompt = "{{OpenLibrary.SearchBooks $jsonData}} {{$query}}";
                }

                var arguments = new KernelArguments(new OpenAIPromptExecutionSettings
                {
                    MaxTokens = 4000,
                    Temperature = 0.3,
                    TopP = 0.9
                })
                {
                    { "query", query },
                    { "jsonData", jsonData }
                };
                // Execute the AI request
           var response = await kernel.InvokePromptAsync(prompt, arguments);

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
