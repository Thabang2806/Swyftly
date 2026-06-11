using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Disputes;

public sealed class DisputeMessage : Entity
{
    private DisputeMessage()
    {
    }

    public DisputeMessage(Guid disputeId, Guid senderUserId, string senderRole, string message, DateTimeOffset createdAtUtc)
    {
        if (disputeId == Guid.Empty)
        {
            throw new ArgumentException("Dispute id is required.", nameof(disputeId));
        }

        if (senderUserId == Guid.Empty)
        {
            throw new ArgumentException("Sender user id is required.", nameof(senderUserId));
        }

        DisputeId = disputeId;
        SenderUserId = senderUserId;
        SenderRole = Required(senderRole, nameof(senderRole), maxLength: 64);
        Message = Required(message, nameof(message), maxLength: 2000);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid DisputeId { get; private set; }

    public Guid SenderUserId { get; private set; }

    public string SenderRole { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

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
}
