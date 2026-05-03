using MultiTenantETL.Domain.Enums;

namespace MultiTenantETL.Application.Common.Models
{
    /// <summary>
    /// Represents an error response returned by API endpoints.
    /// </summary>
    public class ErrorResponse
    {
        public ErrorDetail Error { get; set; }

        public ErrorResponse(AuthErrorCode code, string message, IEnumerable<string>? errors = null)
        {
            Error = new ErrorDetail
            {
                Code = code.ToString(),
                Message = message,
                Errors = errors?.ToList() ?? new List<string>()
            };
        }
    }
}