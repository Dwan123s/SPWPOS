using System.ComponentModel.DataAnnotations;
namespace SPWPOS.Application.Workspace;

// Public projections deliberately exclude email, credentials, calendars, tasks and availability.
public sealed record PersonCard(Guid Id, string DisplayName, string Bio, string AvatarUrl, string Relationship);
public sealed record PeoplePage(PersonCard[] Items, bool HasMore, int? NextOffset);
public sealed record SocialProfileView(Guid Id, string DisplayName, string Bio, string AvatarUrl, string CoverUrl,
    DateTimeOffset JoinedAt, string Relationship, int FriendCount, int MutualFriendCount);
public sealed record FriendshipResult(string Relationship);
public sealed record ProfileCoverInput([StringLength(3000000)] string CoverUrl = "");
public interface ISocialService
{
    Task<SocialProfileView> ProfileAsync(Guid viewerId, Guid profileId, CancellationToken ct);
    Task<PeoplePage> PeopleAsync(Guid viewerId, string? search, int offset, CancellationToken ct);
    Task<PeoplePage> ConnectionsAsync(Guid viewerId, Guid ownerId, string kind, int offset, CancellationToken ct);
    Task<FriendshipResult> RequestAsync(Guid userId, Guid otherId, CancellationToken ct);
    Task<FriendshipResult> AcceptAsync(Guid userId, Guid otherId, CancellationToken ct);
    Task<FriendshipResult> RemoveAsync(Guid userId, Guid otherId, CancellationToken ct);
    Task UpdateCoverAsync(Guid userId, ProfileCoverInput input, CancellationToken ct);
}
