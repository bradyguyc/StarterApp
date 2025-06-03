using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyNextBook.Services;
using Xunit;
namespace MyNextBookTests;
public class OpenLibraryServiceTests
{
    [Fact]
    public async Task GetLists_WithValidCredentials_ReturnsNonEmptyList()
    {
        // Arrange
        var logger = new LoggerFactory().CreateLogger<OpenLibraryService>();
        var service = new OpenLibraryService(logger);

        // Set your test credentials here
        string testUsername = "bradyguychambers@outlook.com";
        string testPassword = "18Alone18#";
        service.SetUsernamePassword(testUsername, testPassword);

        // Act
        var lists = await service.GetLists();

        // Assert
        Assert.NotNull(lists);
        Assert.True(lists.Length > 0, "Expected at least one list to be returned.");
    }
}