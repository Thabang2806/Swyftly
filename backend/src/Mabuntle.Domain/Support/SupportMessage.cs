using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Support;

public sealed class SupportMessage : Entity
{
    private SupportMessage()
    {
    }

    public SupportMessage(
        Guid supportTicketId,
        Guid senderUserId,
        string senderRole,
        string message,
        bool isInternal,
        DateTimeOffset createdAtUtc)
    {
        if (supportTicketId == Guid.Empty)
        {
            throw new ArgumentException("Support ticket id is required.", nameof(supportTicketId));
        }

        if (senderUserId == Guid.Empty)
        {
            throw new ArgumentException("Sender user id is required.", nameof(senderUserId));
        }

        SupportTicketId = supportTicketId;
        SenderUserId = senderUserId;
        SenderRole = Required(senderRole, nameof(senderRole), 64);
        Message = Required(message, nameof(message), 4000);
        IsInternal = isInternal;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid SupportTicketId { get; private set; }

    public Guid SenderUserId { get; private set; }

    public string SenderRole { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public bool IsInternal { get; private set; }

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
