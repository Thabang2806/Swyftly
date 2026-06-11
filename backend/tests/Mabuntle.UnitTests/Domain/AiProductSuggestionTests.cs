using Mabuntle.Domain.Ai;

namespace Mabuntle.UnitTests.Domain;

public class AiProductSuggestionTests
{
    [Fact]
    public void NewSuggestion_StartsAsDraft()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var suggestion = CreateSuggestion(createdAt);

        Assert.Equal(AiProductSuggestionStatus.Draft, suggestion.Status);
        Assert.Equal(createdAt, suggestion.CreatedAtUtc);
        Assert.Null(suggestion.AcceptedAtUtc);
        Assert.Null(suggestion.AppliedAtUtc);
    }

    [Fact]
    public void AcceptAndApply_SetTimestampsThroughDomainMethods()
    {
        var suggestion = CreateSuggestion(DateTimeOffset.UtcNow);
        var acceptedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        var appliedAt = acceptedAt.AddMinutes(1);

        suggestion.Accept(acceptedAt);
        suggestion.MarkApplied(appliedAt);

        Assert.Equal(AiProductSuggestionStatus.Applied, suggestion.Status);
        Assert.Equal(acceptedAt, suggestion.AcceptedAtUtc);
        Assert.Equal(appliedAt, suggestion.AppliedAtUtc);
    }

    [Fact]
    public void AppliedSuggestion_MustBeAcceptedFirst()
    {
        var suggestion = CreateSuggestion(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => suggestion.MarkApplied(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Suggestion_RejectsInvalidQualityScore()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateSuggestion(
            DateTimeOffset.UtcNow,
            qualityScore: 101));
    }

    [Fact]
    public void SuggestionFieldAudit_RecordsAcceptedAndEditedValues()
    {
        var audit = new AiSuggestionFieldAudit(
            Guid.NewGuid(),
            "title",
            "\"AI title\"",
            "\"Seller title\"",
            wasAccepted: false,
            wasEdited: true,
            DateTimeOffset.UtcNow);

        Assert.Equal("title", audit.FieldName);
        Assert.Equal("\"AI title\"", audit.AiValue);
        Assert.Equal("\"Seller title\"", audit.SellerFinalValue);
        Assert.False(audit.WasAccepted);
        Assert.True(audit.WasEdited);
    }

    private static AiProductSuggestion CreateSuggestion(
        DateTimeOffset createdAt,
        decimal qualityScore = 82.5m) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Seller notes",
            "[]",
            "Suggested title",
            "Suggested short description",
            "Suggested full description",
            null,
            "Women > Clothing > Dresses",
            "{\"size\":\"M\"}",
            "[\"summer\"]",
            "[]",
            "[]",
            qualityScore,
            "gpt-test",
            "listing-assistant-v1",
            createdAt);
}
