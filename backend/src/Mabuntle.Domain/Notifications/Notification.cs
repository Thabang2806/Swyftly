using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Notifications;

public sealed class Notification : Entity
{
    private Notification()
    {
    }

    public Notification(
        Guid recipientUserId,
        string type,
        string title,
        string message,
        string? relatedEntityType,
        Guid? relatedEntityId,
        DateTimeOffset createdAtUtc,
        bool isInAppVisible = true)
    {
        if (recipientUserId == Guid.Empty)
        {
            throw new ArgumentException("Recipient user id is required.", nameof(recipientUserId));
        }

        if (relatedEntityId == Guid.Empty)
        {
            throw new ArgumentException("Related entity id cannot be empty.", nameof(relatedEntityId));
        }

        RecipientUserId = recipientUserId;
        Type = Required(type, nameof(type), maxLength: 120);
        Title = Required(title, nameof(title), maxLength: 200);
        Message = Required(message, nameof(message), maxLength: 1000);
        RelatedEntityType = Optional(relatedEntityType, maxLength: 120);
        RelatedEntityId = relatedEntityId;
        CreatedAtUtc = createdAtUtc;
        IsInAppVisible = isInAppVisible;
    }

    public Guid RecipientUserId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public string? RelatedEntityType { get; private set; }

    public Guid? RelatedEntityId { get; private set; }

    public DateTimeOffset? ReadAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public bool IsInAppVisible { get; private set; } = true;

    public bool IsRead => ReadAtUtc.HasValue;

    public void MarkRead(DateTimeOffset readAtUtc)
    {
        ReadAtUtc ??= readAtUtc;
    }

    private static string Required(string? value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Value is required.", parameterName);
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
