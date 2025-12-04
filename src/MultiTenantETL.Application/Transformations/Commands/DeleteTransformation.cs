namespace MultiTenantETL.Application.Transformations.Commands;

/// <summary>
/// Command to delete a transformation
/// </summary>
public record DeleteTransformationCommand(
    Guid Id,
    Guid TenantId);
