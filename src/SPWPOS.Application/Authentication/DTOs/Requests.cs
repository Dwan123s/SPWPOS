using System.ComponentModel.DataAnnotations;
namespace SPWPOS.Application.Authentication.DTOs;

public sealed class RegisterRequest(string email, string password, string confirmPassword, string firstName, string lastName)
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; init; } = email;
    [Required, StringLength(32, MinimumLength = 8)] public string Password { get; init; } = password;
    [Required, Compare(nameof(Password))] public string ConfirmPassword { get; init; } = confirmPassword;
    [Required, StringLength(100)] public string FirstName { get; init; } = firstName;
    [Required, StringLength(100)] public string LastName { get; init; } = lastName;
}
public sealed class EmailRequest(string email)
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; init; } = email;
}
public sealed class OtpRequest(string email, string otp)
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; init; } = email;
    [Required, RegularExpression("^[0-9]{6,8}$")] public string Otp { get; init; } = otp;
}
public sealed class LoginRequest(string email, string password)
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; init; } = email;
    [Required, StringLength(128)] public string Password { get; init; } = password;
}
public sealed class RefreshRequest(string refreshToken)
{
    [Required, StringLength(128, MinimumLength = 86)] public string RefreshToken { get; init; } = refreshToken;
}
public sealed class ResetPasswordRequest(string resetToken, string newPassword, string confirmNewPassword)
{
    [Required, StringLength(128, MinimumLength = 86)] public string ResetToken { get; init; } = resetToken;
    [Required, StringLength(32, MinimumLength = 8)] public string NewPassword { get; init; } = newPassword;
    [Required, Compare(nameof(NewPassword))] public string ConfirmNewPassword { get; init; } = confirmNewPassword;
}
public sealed class ChangePasswordRequest(string currentPassword, string otp, string newPassword, string confirmNewPassword)
{
    [Required, StringLength(128)] public string CurrentPassword { get; init; } = currentPassword;
    [Required, RegularExpression("^[0-9]{6,8}$")] public string Otp { get; init; } = otp;
    [Required, StringLength(32, MinimumLength = 8)] public string NewPassword { get; init; } = newPassword;
    [Required, Compare(nameof(NewPassword))] public string ConfirmNewPassword { get; init; } = confirmNewPassword;
}
