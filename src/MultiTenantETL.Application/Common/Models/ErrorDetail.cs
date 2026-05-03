namespace MultiTenantETL.Application.Common.Models
{
    /// <summary>
    /// Contains detailed error information including code, message, and individual validation errors.
    /// </summary>
    public class ErrorDetail
    {
        public required string Code { get; set; }
        public required string Message { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}