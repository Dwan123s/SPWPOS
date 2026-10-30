namespace SPWPOS.Infrastructure.Calendar;

public static class CalendarOAuthErrors
{
    // Only fixed application codes may reach the redirect URL and logs. Google
    // response bodies, codes, tokens and raw error descriptions stay private.
    public static string Code(CalendarProviderException error) => error.Reason switch
    {
        "missing_scope" => "CALENDAR_SCOPE_REQUIRED",
        "invalid_client" or "unauthorized_client" or "deleted_client" or "disabled_client" => "CALENDAR_CLIENT_REJECTED",
        "invalid_grant" => "CALENDAR_CODE_EXPIRED",
        "redirect_uri_mismatch" => "CALENDAR_CALLBACK_MISMATCH",
        "access_denied" => "CALENDAR_CONSENT_DENIED",
        "invalid_identity" => "CALENDAR_IDENTITY_REQUIRED",
        "org_internal" => "CALENDAR_ORGANIZATION_ONLY",
        _ => "CALENDAR_PROVIDER_ERROR"
    };
}
