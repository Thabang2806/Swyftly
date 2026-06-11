using Microsoft.EntityFrameworkCore;
using Mabuntle.Domain.Ai;
using Mabuntle.Infrastructure.Persistence;

namespace Mabuntle.UnitTests.Infrastructure;

public class AiPersistenceTests
{
    [Fact]
    public async Task AiSuggestion_WithJsonPayloadsAndAudit_CanBePersisted()
    {
        var options = new DbContextOptionsBuilder<MabuntleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var dbContext = new MabuntleDbContext(options);

        var suggestion = new AiProductSuggestion(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Seller notes",
            "[\"11111111-1111-1111-1111-111111111111\"]",
            "Suggested title",
            "Suggested short",
            "Suggested full",
            null,
            "Women > Clothing > Dresses",
            "{\"size\":\"M\",\"colour\":\"Black\"}",
            "[\"summer\",\"dress\"]",
            "[\"image\"]",
            "[\"low-confidence-category\"]",
            76.25m,
            "gpt-test",
            "listing-assistant-v1",
            DateTimeOffset.UtcNow);

        dbContext.AiProductSuggestions.Add(suggestion);
        dbContext.AiSuggestionFieldAudits.Add(new AiSuggestionFieldAudit(
            suggestion.Id,
            "title",
            "\"Suggested title\"",
            "\"Edited title\"",
            wasAccepted: false,
            wasEdited: true,
            DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync();

        var savedSuggestion = await dbContext.AiProductSuggestions.SingleAsync();
        var savedAudit = await dbContext.AiSuggestionFieldAudits.SingleAsync();

        Assert.Contains("\"size\"", savedSuggestion.SuggestedAttributesJson);
        Assert.Equal(suggestion.Id, savedAudit.SuggestionId);
        Assert.True(savedAudit.WasEdited);
    }
}
