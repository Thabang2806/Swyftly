using Mabuntle.Domain.Common;

namespace Mabuntle.Domain.Admin;

public sealed class AdminQueueTriage : AuditableEntity
{
    public const int LatestNoteMaxLength = 500;

    private readonly List<AdminQueueTriageNote> _notes = [];

    private AdminQueueTriage()
    {
    }

    public AdminQueueTriage(AdminQueueItemType itemType, Guid itemId, Guid actorUserId, DateTimeOffset now)
    {
        ItemType = itemType;
        ItemId = itemId;
        Priority = AdminQueuePriority.Normal;
        CreatedByUserId = actorUserId;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public AdminQueueItemType ItemType { get; private set; }

    public Guid ItemId { get; private set; }

    public Guid? AssignedToUserId { get; private set; }

    public AdminQueuePriority Priority { get; private set; }

    public string? LatestNote { get; private set; }

    public Guid? LatestNoteByUserId { get; private set; }

    public DateTimeOffset? LatestNoteAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public IReadOnlyCollection<AdminQueueTriageNote> Notes => _notes.AsReadOnly();

    public void Claim(Guid actorUserId, bool allowOverride, DateTimeOffset now)
    {
        if (AssignedToUserId.HasValue && AssignedToUserId.Value != actorUserId && !allowOverride)
        {
            throw new InvalidOperationException("This queue item is already claimed by another admin.");
        }

        AssignedToUserId = actorUserId;
        UpdatedAtUtc = now;
    }

    public void Unclaim(DateTimeOffset now)
    {
        AssignedToUserId = null;
        UpdatedAtUtc = now;
    }

    public void Assign(Guid? assignedToUserId, DateTimeOffset now)
    {
        AssignedToUserId = assignedToUserId;
        UpdatedAtUtc = now;
    }

    public void SetPriority(AdminQueuePriority priority, DateTimeOffset now)
    {
        Priority = priority;
        UpdatedAtUtc = now;
    }

    public AdminQueueTriageNote AddNote(Guid actorUserId, string note, DateTimeOffset now)
    {
        var cleaned = NormalizeNote(note);
        var entry = new AdminQueueTriageNote(Id, actorUserId, cleaned, now);
        _notes.Add(entry);
        LatestNote = cleaned.Length > LatestNoteMaxLength ? cleaned[..LatestNoteMaxLength] : cleaned;
        LatestNoteByUserId = actorUserId;
        LatestNoteAtUtc = now;
        UpdatedAtUtc = now;
        return entry;
    }

    private static string NormalizeNote(string note)
    {
        var cleaned = note.Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new ArgumentException("Note is required.", nameof(note));
        }

        if (cleaned.Length > AdminQueueTriageNote.NoteMaxLength)
        {
            throw new ArgumentException($"Note must be {AdminQueueTriageNote.NoteMaxLength} characters or fewer.", nameof(note));
        }

        return cleaned;
    }
}
