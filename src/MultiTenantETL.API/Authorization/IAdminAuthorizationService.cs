using MultiTenantETL.Infrastructure.Identity;

namespace MultiTenantETL.API.Authorization;

public interface IAdminAuthorizationService
{
    Task<bool> IsGlobalAdminAsync();
    Task<bool> IsSuperAdminAsync();
    Task<bool> IsSuperAdminUserAsync(Guid userId);
    Task<bool> IsPlatformAdminUserAsync(Guid userId);
    Task<bool> IsProtectedGlobalAdminTargetAsync(Guid userId);
    Task<bool> CanMutateGlobalAdminTargetAsync(Guid targetUserId, bool allowSelf = false);
}
