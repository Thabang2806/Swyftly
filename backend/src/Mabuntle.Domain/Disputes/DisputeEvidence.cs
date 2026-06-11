using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Disputes;

public sealed class DisputeEvidence : Entity
{
    private DisputeEvidence()
    {
    }

    public DisputeEvidence(
        Guid disputeId,
        Guid submittedByUserId,
        string submittedByRole,
        string evidenceType,
        string storageReference,
        string? description,
        DateTimeOffset createdAtUtc)
    {
        if (disputeId == Guid.Empty)
        {
            throw new ArgumentException("Dispute id is required.", nameof(disputeId));
        }

        if (submittedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Submitted-by user id is required.", nameof(submittedByUserId));
        }

        DisputeId = disputeId;
        SubmittedByUserId = submittedByUserId;
        SubmittedByRole = Required(submittedByRole, nameof(submittedByRole), maxLength: 64);
        EvidenceType = Required(evidenceType, nameof(evidenceType), maxLength: 80);
        StorageReference = Required(storageReference, nameof(storageReference), maxLength: 500);
        Description = Optional(description, maxLength: 1000);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid DisputeId { get; private set; }

    public Guid SubmittedByUserId { get; private set; }

    public string SubmittedByRole { get; private set; } = string.Empty;

    public string EvidenceType { get; private set; } = string.Empty;

    public string StorageReference { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private static string? Optional(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
