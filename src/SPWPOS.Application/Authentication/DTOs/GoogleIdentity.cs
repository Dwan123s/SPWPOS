namespace SPWPOS.Application.Authentication.DTOs;

// Constructed from a Firebase ID token verified by the server; never accepted as browser input.
public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string FirstName, string LastName);
