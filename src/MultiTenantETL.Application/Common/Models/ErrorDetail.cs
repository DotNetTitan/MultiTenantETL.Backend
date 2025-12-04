namespace MultiTenantETL.Application.Common.Models
{
    public class ErrorDetail
    {
        public required string Code { get; set; }
        public required string Message { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}