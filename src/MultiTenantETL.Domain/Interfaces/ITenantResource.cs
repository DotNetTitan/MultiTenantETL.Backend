namespace MultiTenantETL.Domain.Interfaces
{
    public interface ITenantResource
    {
        Guid TenantId { get; }
    }
}