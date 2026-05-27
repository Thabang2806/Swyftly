using Swyftly.Domain.Common;

namespace Swyftly.Domain.Admin;

public sealed class AdminQueueTriageNote : Entity
{
    public const int NoteMaxLength = 1000;

    private AdminQueueTriageNote()
    {
    }

    public AdminQueueTriageNote(Guid triageId, Guid actorUserId, string note, DateTimeOffset createdAtUtc)
    {
        TriageId = triageId;
        ActorUserId = actorUserId;
        Note = note;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid TriageId { get; private set; }

    public Guid ActorUserId { get; private set; }

    public string Note { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
