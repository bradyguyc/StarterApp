using System;
using System.Collections.Generic;
using System.Text;

namespace MyNextBook.Helpers
{
    public static  class Constants
    {
        public const string LightModeKey = "LightMode";
        public const string ThemeColorKey = "ThemeColorString";
        public const string CustomColorKey = "CustomColorString";
        public const string OpenLibraryUsernameKey = "OpenLibraryUsername";
        public const string OpenLibraryPasswordKey = "OpenLibraryPassword";



           // public const double bookTokenMatch = 90;
          //  public const double bookMatchThreshold = .50;

         //   public const string ol_baseurl = "https://openlibrary.org/";
            public const string matchedText = "When filling in missing data.  Matched is the number of books found and data was filled in.  Not means that  number of books in the series were not found.  This process could take some time depending on the number of series and books being imported.";
            public const string ol_readingbooks = "https://openlibrary.org/people/bradyguy/books/currently-reading.json";
            public const string ol_finishedbooks = "https://openlibrary.org/people/bradyguy/books/already-read.json";
            //curl -i -H "Content-Type: application/json" -d "{\"access\": \"BuFnKv6bsbFVzo0q\", \"secret\": \"W9Nj88SuJYzb6BQ5\"}" https://openlibrary.org/account/login.json
            public const string FillinMissingDataText = "Check to have the import process attempt to fill in missing data.  Subtitles, description, images, and other data about the book.";

            //  curl https://openlibrary.org/people/mekBot/books/want-to-read.json -H "Accept: application/json" -b "session=/people/bradyguy%2C2024-01-25T21%3A36%3A37%2C18a41%241e429bd3b08139dbdb194a8a1f26a413"
            //public const string synFusionKey = "MjUyNzU2NEAzMjMyMmUzMDJlMzBUbWFaejNtTEpPeGdZNWtpOVpTakY4V0ZGMlVhK3BsVFBNSU9ndEpjc2VRPQ==";
            //public const string synFusionKey = "MjkyMDI2NUAzMjMzMmUzMDJlMzBKZG1ZTXZkcXNsSkg5cUZUVHZ4cDdjTUtibm81M3pWV25GNXZYcndsQVNzPQ==";
            //public const string synFusionKey = "MzQxNzgzN0AzMjM2MmUzMDJlMzBCa3lMdTM1TGdycWRIcG41VjFPUHU2TGp0aVQwbk9aWmtDTnRlQVFFQUd3PQ==";
            //"Ngo9BigBOggjHTQxAR8/V1NCaF1cWWhBYVFpR2Nbe05xdF9HZ1ZTQ2YuP1ZhSXxXdkJhX35ecXNXQWheVkA=";
            //public const string synFusionKey = "MTk1NjkzMUAzMjMxMmUzMjJlMzNBVTFaQUpEUkZ5Tnp6MUtjNndQaEk3YllkejdaSER4Q1FvVEpYbXA0azN3PQ==";
            //public const string bingSubscriptionKey = "2f722b25-ae06-4525-b30a-a4b302b2ea9a";
            public const string ImportInstructions = "Click the select file button and a new screen will appear that lets you select a file from your device to import.  Once you select the file the file will be parsed and you will be presented with a choice to import the results or make adjustments if needed.";
            //public const string BingChatDescriptionKeyword = "What are some  book series that have the word {0} in their description? List about 10 book series at a minimum. Please return the list of book series in JSON format with a structure of title, description, author, first book in series, and the ISBN of the first book in the series  and number of books in the series.  return just the json";
            //public const string BackupFolderInfo = "The backup / sync folder is folder on your device where a back up of your data is copied to during a sync operation.  If this location is a location such as onedrive or google drive then sharing data with multiple devices is enabled.  When data is saved to a shared folder the backup file is available to each device that has access to that shared file.  When updates are made on once device and synced to another device your data is synced without having to upgrade to a paid feature wher eyou data is saved in the cloud.";
            //public const string BingChatAuthor = "What are some  book series written by the author {0}? List about 10 book series at a minimum. Please return the list of book series in JSON format with a structure of title, description, author, first book in series, and the ISBN of the first book in the series  and number of books in the series.  return just the json";
            //public const string CreateOLBook = "";
            //public const string BingChatCharacter = "What are some  book series with a character named {0}? List about 10 book series at a minimum. Please return the list of book series in JSON format with a structure of title, description, author, first book in series, and the ISBN of the first book in the series  and number of books in the series.  return just the json";
            //public const string BingChatGetBooksInSeries = "what are all the books in the book series {0}; output title and isbn";
            /*public const string GetSeriesByKeyword = @"
			PREFIX dbo: <http://dbpedia.org/ontology/>
			PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

			select ?label, ?series ,?genre, ?numberOfBooks, ?thumbnail ,?notableWork  ,?comment, ?abstract where {{
				{{		select ?label,?series,?numberOfBooks ,?genre, ?numberOfBooks, ?thumbnail ,?notableWork  ,?comment, ?abstract where {{
						{{
							SELECT DISTINCT ?series
							WHERE {{
								?book a dbo:Book.
								?book dbo:series ?series.
							}}
						}}
						OPTIONAL {{ ?series dbo:abstract ?abstract.     FILTER(LANGMATCHES(LANG(?abstract), 'en'))    }}
						OPTIONAL {{ ?series dbo:thumbnail ?thumbnail.     FILTER(LANGMATCHES(LANG(?thumbnail), 'en'))  }}
						OPTIONAL {{ ?series dbp:genre ?genre.     FILTER(LANGMATCHES(LANG(?genre), 'en'))  }}
						OPTIONAL {{ ?series dbp:numberOfBooks ?numberOfBooks.     FILTER(LANGMATCHES(LANG(?numberOfBooks), 'en'))  }}
						OPTIONAL {{ ?series rdfs:comment ?comment .     FILTER(LANGMATCHES(LANG(?comment ), 'en'))  }}
						OPTIONAL {{ ?series rdfs:label ?label .     FILTER(LANGMATCHES(LANG(?label ), 'en'))  }}
						OPTIONAL {{ ?series dbo:notableWork  ?notableWork  .     FILTER(LANGMATCHES(LANG(?notableWork  ), 'en'))  }}
						
					}}

				}}
			FILTER(contains(lcase(?abstract),""{0}""))
			}}
		";
            public const string GetSeriesBooks = @"
		    PREFIX dbpedia: <http://dbpedia.org/resource/>
			PREFIX dbo: <http://dbpedia.org/ontology/>

			SELECT DISTINCT *
			WHERE {{
				?book a dbo:Book.
					?book rdfs:label ?name.
					FILTER(LANGMATCHES(LANG(?name), 'en'))  
				    OPTIONAL {{	?book dbo:author ?author.  	 ?author rdfs:label ?authorLabel.   FILTER(LANGMATCHES(LANG(?authorLabel), 'en'))   }}  
					OPTIONAL {{ ?book dbo:previousWork ?previousWork }}
					OPTIONAL {{ ?book dbp:followedBy ?followedBy }}
					OPTIONAL {{ ?book dbo:subsequentWork ?subsequentWork }}
					OPTIONAL {{ ?book dbo:isbn ?isbn }}
					OPTIONAL {{ ?book dbo:releaseDate ?releaseDate }}
					OPTIONAL {{ ?book dbo:abstract ?abstract.     FILTER(LANGMATCHES(LANG(?abstract), 'en'))    }}
					?book dbo:series ?series. 
					FILTER (?{0}  in ({1})).
			}}
		"; */
            /*
            public const string GetMissingBooks = @"
                PREFIX dbpedia: <http://dbpedia.org/resource/>
                PREFIX dbo: <http://dbpedia.org/ontology/>

                SELECT DISTINCT *
                WHERE {{
                    ?book a dbo:Book.
                        ?book rdfs:label ?name.
                        FILTER(LANGMATCHES(LANG(?name), 'en'))  
                        OPTIONAL {{ ?book dbp:author ?author.  }}
                        OPTIONAL {{ ?author rdfs:label ?authorLabel.   FILTER(LANGMATCHES(LANG(?authorLabel), 'en'))   }}
                        OPTIONAL {{ ?book dbo:previousWork ?previousWork }}
                        OPTIONAL {{ ?book dbp:followedBy ?followedBy }}
                        OPTIONAL {{ ?book dbo:subsequentWork ?subsequentWork }}
                        OPTIONAL {{ ?book dbo:isbn ?isbn }}
                        OPTIONAL {{ ?book dbo:publicationDate ?publicationDate }}
                        OPTIONAL {{ ?book dbo:abstract ?abstract.     FILTER(LANGMATCHES(LANG(?abstract), 'en'))    }}
                        ?book dbo:series ?series. 
                        FILTER (?book in ({0})).
                }}
            ";
            */
            }
    }

