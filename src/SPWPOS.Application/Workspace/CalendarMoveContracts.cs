using System.ComponentModel.DataAnnotations;
namespace SPWPOS.Application.Workspace;

public sealed record CalendarMoveInput([Required, RegularExpression("^(Event|Session)$")] string Kind, Guid Id,
    DateTimeOffset Start, DateTimeOffset End, Guid Revision, DateTimeOffset ExpectedStart, DateTimeOffset ExpectedEnd);
public sealed record CalendarMoveResult(string Kind, Guid Id, DateTimeOffset Start, DateTimeOffset End,
    DateTimeOffset PreviousStart, DateTimeOffset PreviousEnd, Guid Revision);
