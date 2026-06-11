using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Returns;

public sealed class ReturnMessage : Entity
{
    private ReturnMessage()
    {
    }

    public ReturnMessage(
        Guid returnRequestId,
        Guid senderUserId,
        string senderRole,
        string message,
        DateTimeOffset createdAtUtc)
    {
        if (returnRequestId == Guid.Empty)
        {
            throw new ArgumentException("Return request id is required.", nameof(returnRequestId));
        }

        if (senderUserId == Guid.Empty)
        {
            throw new ArgumentException("Sender user id is required.", nameof(senderUserId));
        }

        ReturnRequestId = returnRequestId;
        SenderUserId = senderUserId;
        SenderRole = RequiredText(senderRole, nameof(senderRole), maxLength: 64);
        Message = RequiredText(message, nameof(message), maxLength: 2000);
        CreatedAtUtc = createdAtUtc;
    }

    public Guid ReturnRequestId { get; private set; }

    public Guid SenderUserId { get; private set; }

    public string SenderRole { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string RequiredText(string value, string parameterName, int maxLength)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
