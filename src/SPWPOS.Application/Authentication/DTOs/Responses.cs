namespace SPWPOS.Application.Authentication.DTOs;

public sealed record ApiResponse<T>(bool Success, T Data)
{
    public static ApiResponse<T> Ok(T data) => new(true, data);
}
public sealed record MessageResponse(string Message);
public sealed record ResetTicketResponse(string ResetToken);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, string TokenType = "Bearer");
public sealed record CurrentUserResponse(Guid Id, string Email, string FirstName, string LastName,
    bool EmailVerified, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt);
public sealed record RequestContext(string? IpAddress, string? UserAgent);
