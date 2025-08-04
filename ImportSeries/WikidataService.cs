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
                endpoint.UserAgent = "MyNextBook/1.0 (https://github.com/dracan/MyNextBook; dracan@users.noreply.github.com)";
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

                return DbpediaQueryService.SortBooksBySequence(books);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error querying Wikidata for series '{seriesName}': {ex.Message}");
                return new List<BookInfo>();
            }
        }
    }
}
