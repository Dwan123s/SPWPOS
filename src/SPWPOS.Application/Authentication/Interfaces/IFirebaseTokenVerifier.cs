using SPWPOS.Application.Authentication.DTOs;
namespace SPWPOS.Application.Authentication.Interfaces;

public interface IFirebaseTokenVerifier
{
    Task<GoogleIdentity> VerifyGoogleAsync(string idToken, CancellationToken ct);
}
