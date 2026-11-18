namespace SPWPOS.Application.Workspace;

public sealed record TeamInviteFriendView(Guid UserId, string Name, string AvatarUrl, string Status, Guid? InvitationId, Guid? Version);
public sealed record TeamInviteFriendsPage(TeamInviteFriendView[] Items, int? NextOffset);
public sealed record TeamFriendInvitationView(Guid Id, Guid TeamId, string TeamName, string SenderName, DateTimeOffset ExpiresAt, Guid Version);
public sealed record TeamFriendInviteInput(Guid UserId);
public sealed record TeamFriendInviteResponse(Guid Version, bool Accepted);
