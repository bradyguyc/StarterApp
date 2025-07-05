using System.IO;
using System.Threading.Tasks;
using Xunit;
using ImportSeries;
using ImportSeries.Models;

namespace ImportSeries.Tests
{
    public class ImportTests
    {
        [Fact]
        public async Task Import_ValidCsvFile_ThrowsExceptionOnAiCall()
        {
            // Arrange
            var importCsvData = new ImportCSVData();
            string dir = "c:\\data";
            string csvFilePath = Path.Combine(dir, "starwars.csv");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllTextAsync(csvFilePath, "Series,Title\nStar Wars,A New Hope");

            await using var stream = File.OpenRead(csvFilePath);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<System.Exception>(() => importCsvData.Import(stream));

            // Verify that the CSV was read correctly before the AI call failed
            Assert.Equal(1, importCsvData.rowsRead);
            Assert.Equal(1, importCsvData.BooksFound);
            Assert.Equal(1, importCsvData.SeriesFound);
            Assert.StartsWith("Failed to enhance data via AI", ex.Message);

            // Cleanup
            File.Delete(csvFilePath);
            Directory.Delete(dir);
        }
    }
}
