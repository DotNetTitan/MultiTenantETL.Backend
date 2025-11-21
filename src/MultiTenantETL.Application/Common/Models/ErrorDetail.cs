namespace MultiTenantETL.Application.Common.Models
{
    public class ErrorDetail
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public List<string> Errors { get; set; }
    }
}