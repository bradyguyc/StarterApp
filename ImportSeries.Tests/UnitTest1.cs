using System.IO;
using System.Threading.Tasks;
using Xunit;
using ImportSeries;
using ImportSeries.Models;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Data;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace ImportSeries.Tests
{
    public class ImportTests
    {
        private readonly IConfiguration _configuration;

        public ImportTests()
        {
            // Load configuration the same way as MauiProgram.cs
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{assembly.GetName().Name}.appsettings.json";

            using var stream = assembly.GetManifestResourceStream(resourceName);

            var configBuilder = new ConfigurationBuilder();

            if (stream != null)
            {
                // Add the embedded resource stream first
                configBuilder.AddJsonStream(stream);
            }
            else
            {
                // Fallback to file-based loading if embedded resource is not found
                configBuilder.AddJsonFile("appsettings.json", optional: true);
            }

            // Add additional configuration sources (corrected filename to match what exists)
            configBuilder
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables();

            _configuration = configBuilder.Build();

            // Initialize AppConfig similar to MauiProgram.cs
            AppConfig.Initialize(_configuration);
        }

        [Fact]
        public async Task Import_ValidCsvFile_ThrowsExceptionOnAiCall()
        {
            // Arrange
            var importCsvData = new ImportCSVData();
            string dir = "c:\\data";
            string csvFilePath = Path.Combine(dir, "seriesTestFile2.csv");



            await using var stream = File.OpenRead(csvFilePath);

            // Act & Assert
            await importCsvData.Import(stream);
            DataTable dt = importCsvData.GetResultsDataTable();




            // Write DataTable to CSV file for verification
            string outputCsvPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(csvFilePath) + "_output.csv");

            // Delete existing output file if it exists
            if (File.Exists(outputCsvPath))
            {
                File.Delete(outputCsvPath);
            }

            using (var writer = new StringWriter())
            using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                ShouldQuote = _ => true, // Quote all fields
                HasHeaderRecord = true
            }))
            {
                // Write headers
                foreach (DataColumn column in dt.Columns)
                {
                    csv.WriteField(column.ColumnName);
                }
                csv.NextRecord();

                // Write data rows
                foreach (DataRow row in dt.Rows)
                {
                    foreach (var field in row.ItemArray)
                    {
                        csv.WriteField(field?.ToString() ?? string.Empty);
                    }
                    csv.NextRecord();
                }

                File.WriteAllText(outputCsvPath, writer.ToString());
            }
        }
    }
}
