namespace MultiTenantETL.Domain.Enums
{
    public enum AuthErrorCode
    {
        // Authentication errors
        InvalidCredentials,
        AccountLocked,
        EmailNotConfirmed,
        InvalidToken,
        TokenExpired,

        // Registration errors
        EmailAlreadyExists,
        RegistrationFailed,
        WeakPassword,

        // Email confirmation
        ConfirmationFailed,
        InvalidConfirmationToken,

        // Password reset
        ResetFailed,
        InvalidResetToken,

        // Password change
        ChangePasswordFailed,
        CurrentPasswordIncorrect,

        // Tenant operations
        TenantAccessDenied,
        TenantNotFound,
        TenantAlreadyExists,
        UserAlreadyInTenant,
        UserNotInTenant,

        // General
        UserNotFound,
        ValidationError,
        InternalError
    }
}