using MultiTenantETL.Domain.Enums;

namespace MultiTenantETL.Application.Common.Models;

/// <summary>
/// Generic service result for operations without return data.
/// </summary>
public class ServiceResult
{
    public bool Success { get; set; }
    public AuthErrorCode? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static ServiceResult SuccessResult() => new() { Success = true };
    
    public static ServiceResult FailureResult(AuthErrorCode errorCode, string errorMessage) =>
        new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>
/// Generic service result with data for operations that return data
/// </summary>
/// <typeparam name="T">The type of data returned on success</typeparam>
public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; set; }

    public static ServiceResult<T> SuccessResult(T data) => new() { Success = true, Data = data };
    
    public static new ServiceResult<T> FailureResult(AuthErrorCode errorCode, string errorMessage) =>
        new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
