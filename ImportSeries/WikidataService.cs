using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Linq;

using VDS.RDF;
using Newtonsoft.Json;
using VDS.RDF.Query;
using VDS.RDF.Writing;
using ImportSeries.Models; // Add missing using statement

// Define a class to hold book information results
namespace ImportSeries.Services
{
    // Note: BookInfo is also defined in DBPediaService.cs. Consider moving to a shared Models folder.
    // For now, it's duplicated to keep this service self-contained.
    // public class BookInfo ... (assuming it's accessible or will be moved)

    public static class WikidataQueryService
    {
        /// <summary>
        /// Queries Wikidata for all books in the given series and returns their details.
        /// </summary>
        /// <param name="seriesName">Name of the book series (in English, as titled on Wikidata)</param>
        /// <returns>List of BookInfo objects containing title, author, ISBN, series name, and series order.</returns>
        public static List<BookInfo> GetBooksInSeries(string seriesName)
        {
            // Wikidata SPARQL query to get books in a series
            string query = $@"
            SELECT ?bookLabel ?authorLabel ?seriesLabel ?isbn13 ?isbn10 ?precededByLabel ?followedByLabel ?seriesOrdinal ?publicationDate
            WHERE
            {{
              # Find the series by its English label
              ?series rdfs:label ""{seriesName}""@en;
                      wdt:P31 wd:Q277759. # instance of book series

              # Get all books in that series
              ?book wdt:P179 ?series.

              # Get book details
              OPTIONAL {{ ?book wdt:P50 ?author. }}
              OPTIONAL {{ ?book wdt:P212 ?isbn13. }}      # Direct ISBN-13 value
              OPTIONAL {{ ?book wdt:P957 ?isbn10. }}      # Direct ISBN-10 value
              OPTIONAL {{ ?book wdt:P155 ?precededBy. }}
              OPTIONAL {{ ?book wdt:P156 ?followedBy. }}
              OPTIONAL {{ ?book wdt:P1545 ?seriesOrdinal. }}
              OPTIONAL {{ ?book wdt:P577 ?publicationDate. }}

              # Use the service to get labels for entities only
              SERVICE wikibase:label {{ 
                bd:serviceParam wikibase:language ""[AUTO_LANGUAGE],en"". 
                ?book rdfs:label ?bookLabel.
                ?author rdfs:label ?authorLabel.
                ?series rdfs:label ?seriesLabel.
                ?precededBy rdfs:label ?precededByLabel.
                ?followedBy rdfs:label ?followedByLabel.
              }}
            }}
            ";

            try
            {
                // Wikidata SPARQL endpoint
                SparqlRemoteEndpoint endpoint = new SparqlRemoteEndpoint(new Uri("https://query.wikidata.org/sparql"));
                endpoint.UserAgent = "MyNextBook/1.0 (https://github.com/bradyguy/MyNextBook; bradyguy@users.noreply.github.com)";//todo: validate this line
                SparqlResultSet results = endpoint.QueryWithResultSet(query);

                List<BookInfo> books = new List<BookInfo>();

                foreach (SparqlResult result in results)
                {
                    BookInfo book = new BookInfo
                    {
                        Title = result["bookLabel"]?.ToString().Replace("@en", "") ?? "",
                        Author = result["authorLabel"]?.ToString().Replace("@en", "") ?? "",
                        SeriesName = result["seriesLabel"]?.ToString().Replace("@en", "") ?? "",
                        ISBN = result["isbn10"]?.ToString() ?? "",
                        ISBN13 = result["isbn13"]?.ToString() ?? "",
                        SeriesOrder = result["seriesOrdinal"]?.ToString() ?? "",
                        PreviousWork = result["precededByLabel"]?.ToString().Replace("@en", "").Replace(" ", "_") ?? "",
                        SubsequentWork = result["followedByLabel"]?.ToString().Replace("@en", "").Replace(" ", "_") ?? "",
                        ReleaseDate = result["publicationDate"]?.ToString() ?? ""
                    };
                    books.Add(book);
                }

                return SortBooksBySequence(books);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error querying Wikidata for series '{seriesName}': {ex.Message}");
                return new List<BookInfo>();
            }
        }
        public static List<BookInfo> SortBooksBySequence(List<BookInfo> books)
        {
            try
            {
                if (books == null || books.Count == 0)
                    return new List<BookInfo>();

                var uniqueBooks = books
                    .Where(b => !string.IsNullOrEmpty(b.Title))
                    .GroupBy(b => b.Title!, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                if (uniqueBooks.Count == 0)
                    return new List<BookInfo>();

                var bookDict = uniqueBooks.ToDictionary(b => b.Title!, b => b, StringComparer.OrdinalIgnoreCase);
                var sortedBooks = new List<BookInfo>();
                var processedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Find all books that are the start of a chain (no previous work in the set)
                var startingPoints = uniqueBooks
                    .Where(b => string.IsNullOrEmpty(b.PreviousWork) && !string.IsNullOrWhiteSpace(b.SubsequentWork))
                    .ToList();

                // Process all chains that have a clear starting point
                foreach (var startNode in startingPoints)
                {
                    if (processedTitles.Contains(startNode.Title!))
                    {
                        continue;
                    }

                    var currentNode = startNode;
                    var visitedInPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // For cycle detection in the current chain

                    while (currentNode != null && !processedTitles.Contains(currentNode.Title!))
                    {
                        if (!visitedInPath.Add(currentNode.Title!)) // Cycle detected
                        {
                            break;
                        }

                        sortedBooks.Add(currentNode);
                        processedTitles.Add(currentNode.Title!);

                        if (string.IsNullOrEmpty(currentNode.SubsequentWork))
                        {
                            currentNode = null;
                        }
                        else currentNode = uniqueBooks
                            .FirstOrDefault(b => string.Equals(b.Title.Replace(" ", "_"), currentNode.SubsequentWork, StringComparison.OrdinalIgnoreCase));
                    }
                }

                // Process remaining books, which might be in cycles or separate sub-chains
                var remainingBooks = uniqueBooks.Where(b => !processedTitles.Contains(b.Title!)).ToList();
                while (remainingBooks.Any())
                {
                    var startOfChunk = remainingBooks.First();
                    var currentNode = startOfChunk;
                    var visitedInPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    while (currentNode != null && !processedTitles.Contains(currentNode.Title!))
                    {
                        if (!visitedInPath.Add(currentNode.Title!)) // Cycle detected
                        {
                            break;
                        }

                        sortedBooks.Add(currentNode);
                        processedTitles.Add(currentNode.Title!);

                        if (string.IsNullOrEmpty(currentNode.SubsequentWork) || !bookDict.TryGetValue(currentNode.SubsequentWork, out currentNode))
                        {
                            currentNode = null;
                        }
                    }
                    remainingBooks = uniqueBooks.Where(b => !processedTitles.Contains(b.Title!)).ToList();
                }

                return sortedBooks;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error sorting books by sequence: {ex.Message}");
                // Return a safe fallback - deduplicated books sorted by title
                return books?.Where(b => !string.IsNullOrEmpty(b.Title))
                            .GroupBy(b => b.Title!, StringComparer.OrdinalIgnoreCase)
                            .Select(g => g.First())
                            .OrderBy(b => b.Title)
                            .ToList() ?? new List<BookInfo>();
            }
        }
    }
}
