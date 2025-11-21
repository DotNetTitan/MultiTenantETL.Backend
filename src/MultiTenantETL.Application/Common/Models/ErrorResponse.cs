using MultiTenantETL.Domain.Enums;

namespace MultiTenantETL.Application.Common.Models
{
    public class ErrorResponse
    {
        public ErrorDetail Error { get; set; }

        public ErrorResponse(AuthErrorCode code, string message, IEnumerable<string> errors = null)
        {
            Error = new ErrorDetail
            {
                Code = code.ToString(),
                Message = message,
                Errors = errors?.ToList()
            };
        }
    }
}