namespace MultiTenantETL.Domain.Enums
{
    /// <summary>
    /// Error codes for authentication and authorization operations.
    /// </summary>
    public enum AuthErrorCode
    {
        // Authentication errors

        /// <summary>The provided credentials are invalid.</summary>
        InvalidCredentials,

        /// <summary>The account has been locked.</summary>
        AccountLocked,
        
        /// <summary>The account is inactive or deleted.</summary>
        UserInactive,

        /// <summary>The email address has not been confirmed.</summary>
        EmailNotConfirmed,

        /// <summary>The token provided is invalid.</summary>
        InvalidToken,

        /// <summary>The token has expired.</summary>
        TokenExpired,

        // Registration errors

        /// <summary>The email address already exists.</summary>
        EmailAlreadyExists,

        /// <summary>Registration failed.</summary>
        RegistrationFailed,

        /// <summary>The password is too weak.</summary>
        WeakPassword,

        // Email confirmation

        /// <summary>Confirmation failed.</summary>
        ConfirmationFailed,

        /// <summary>The confirmation token is invalid.</summary>
        InvalidConfirmationToken,

        // Password reset

        /// <summary>Password reset failed.</summary>
        ResetFailed,

        /// <summary>The reset token is invalid.</summary>
        InvalidResetToken,

        // Password change

        /// <summary>Changing the password failed.</summary>
        ChangePasswordFailed,

        /// <summary>The current password is incorrect.</summary>
        CurrentPasswordIncorrect,

        // Tenant operations

        /// <summary>Access to the tenant was denied.</summary>
        TenantAccessDenied,

        /// <summary>The tenant was not found.</summary>
        TenantNotFound,

        /// <summary>The tenant already exists.</summary>
        TenantAlreadyExists,

        /// <summary>The user is already in the tenant.</summary>
        UserAlreadyInTenant,

        /// <summary>The user is not in the tenant.</summary>
        UserNotInTenant,

        // General

        /// <summary>The user was not found.</summary>
        UserNotFound,

        /// <summary>A validation error occurred.</summary>
        ValidationError,

        /// <summary>An internal error occurred.</summary>
        InternalError
    }
}