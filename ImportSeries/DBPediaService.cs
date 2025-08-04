using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Linq;

using VDS.RDF;
using Newtonsoft.Json;
using VDS.RDF.Query;
using VDS.RDF.Writing;

// Define a class to hold book information results
namespace ImportSeries.Services
{
    /// <summary>
    /// Represents information about a book, including title, author, ISBN, series name, and series order.
    /// </summary>
    public class BookInfo
    {
        public string? Name { get; set; }
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? ISBN { get; set; }
        public string? ISBN13 { get; set; } // Added ISBN-13 field
        public string? SeriesName { get; set; }
        public string? SeriesOrder { get; set; }
        public string? Publisher { get; set; } // Added publisher field
        public string? PreviousWork { get; set; } // Added previous work field
        public string? SubsequentWork { get; set; } // Added next work field
        public string? ReleaseDate { get; set; } // Added Open Library key field
        public string? ReleaseNumber { get; set; } // Added release number field
        public string? Number { get; set; } // Added number field
        public string? BookNumber { get; set; } // Added book number field
    }

    public static class DbpediaQueryService
    {
        /// <summary>
        /// Queries DBpedia for all books in the given series and returns their details.
        /// </summary>
        /// <param name="seriesName">Name of the book series (in English, as titled on DBpedia)</param>
        /// <returns>List of BookInfo objects containing title, author, ISBN, series name, and series order.</returns>
        public static List<BookInfo> GetBooksInSeries(string seriesName)
        {
            string query = $@"
            SELECT  ?title ?authorName ?isbn ?isbn13 ?seriesName ?releaseNumber ?number ?bookNumber ?previousWork ?subsequentWork ?releaseDate 
            WHERE {{
              
                ?book dbo:series ?series .
                ?series rdfs:label ""{seriesName}""@en .
                ?book rdfs:label ?title .
                 OPTIONAL {{ ?book dbo:author ?author }}.
                 OPTIONAL {{?author rdfs:label ?authorName }}.
                 OPTIONAL {{?series rdfs:label ?seriesName }}.
                OPTIONAL {{ ?book dbo:isbn ?isbn }} .
                OPTIONAL {{ ?book dbp:isbn13 ?isbn13 }} .
                OPTIONAL {{ ?book dbp:releaseNumber ?releaseNumber }} .
                OPTIONAL {{ ?book dbp:number ?number }} .
                OPTIONAL {{ ?book dbp:bookNumber ?bookNumber }} .
                OPTIONAL {{ ?book dbo:previousWork ?previousWork }} .
                OPTIONAL {{ ?book dbo:subsequentWork ?subsequentWork }} .
                OPTIONAL {{ ?book dbo:releaseDate ?releaseDate }} .
                FILTER(lang(?title) = 'en' && lang(?authorName) = 'en' && lang(?seriesName) = 'en')
            }}
            ";

            try
            {
                SparqlRemoteEndpoint endpoint = new SparqlRemoteEndpoint(new Uri("http://dbpedia.org/sparql"), "http://dbpedia.org");
                SparqlResultSet results = endpoint.QueryWithResultSet(query);

                List<BookInfo> books = new List<BookInfo>();

                foreach (SparqlResult result in results)
                {
                    BookInfo book = new BookInfo
                    {
                        Title = result["title"]?.ToString().Replace("@en", "").Replace(" ", "_") ?? "",
                        Author = result["authorName"]?.ToString() ?? "",
                        SeriesName = result["seriesName"]?.ToString() ?? "",
                        ISBN = result["isbn"]?.ToString() ?? "",
                        ISBN13 = result["isbn13"]?.ToString() ?? "",
                 
                        SeriesOrder = result["releaseNumber"]?.ToString() ?? result["number"]?.ToString() ?? result["bookNumber"]?.ToString() ?? "",
                        PreviousWork = result["previousWork"]?.ToString().Replace("http://dbpedia.org/resource/", "") ?? "",
                        SubsequentWork = result["subsequentWork"]?.ToString().Replace("http://dbpedia.org/resource/", "") ?? "",
                        ReleaseDate = result["releaseDate"]?.ToString() ?? "",
                        ReleaseNumber = result["releaseNumber"]?.ToString() ?? "",
                        Number = result["number"]?.ToString() ?? "",
                        BookNumber = result["bookNumber"]?.ToString() ?? ""
                    };
                    books.Add(book);
                }

                return SortBooksBySequence(books);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error querying DBpedia for series '{seriesName}': {ex.Message}");
                return new List<BookInfo>();
            }
        }

        /// <summary>
        /// Sorts a list of books based on their PreviousWork and SubsequentWork relationships.
        /// </summary>
        /// <param name="books">List of books to sort</param>
        /// <returns>List of books sorted in series order</returns>
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

                        if (string.IsNullOrEmpty(currentNode.SubsequentWork))               {
                            currentNode = null;
                        } else currentNode = uniqueBooks
                            .FirstOrDefault(b => string.Equals(b.Title.Replace(" ","_"), currentNode.SubsequentWork, StringComparison.OrdinalIgnoreCase));
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