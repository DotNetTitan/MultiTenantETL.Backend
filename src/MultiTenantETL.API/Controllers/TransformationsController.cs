using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Transformations.Commands;
using MultiTenantETL.Application.Transformations.Models;
using MultiTenantETL.Application.Transformations.Queries;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Authorization.Requirements;
using Wolverine;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransformationsController : ControllerBase
{
    private readonly IMessageBus _bus;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;

    public TransformationsController(
        IMessageBus bus,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService)
    {
        _bus = bus;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Get all transformations with optional filters and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedTransformationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll([FromQuery] TransformationSearchRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Transformations.Read));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var query = new SearchTransformationsQuery(
            request.Name,
            request.Type,
            request.Search,
            request.Sort,
            request.Page,
            request.PageSize,
            tenantId);

        var result = await _bus.InvokeAsync<PagedTransformationResponse>(query);
        return Ok(result);
    }

    /// <summary>
    /// Get transformation by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TransformationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Transformations.Read));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var query = new GetTransformationByIdQuery(id, tenantId);
        var transformation = await _bus.InvokeAsync<TransformationResponse>(query);
        return Ok(transformation);
    }

    /// <summary>
    /// Create a new transformation
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TransformationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateTransformationRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Transformations.Create));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        var command = new CreateTransformationCommand(
            request.Name,
            request.Description,
            request.Type,
            request.Config,
            tenantId,
            userId);

        var transformation = await _bus.InvokeAsync<TransformationResponse>(command);
        return CreatedAtAction(nameof(GetById), new { id = transformation.Id }, transformation);
    }

    /// <summary>
    /// Update an existing transformation
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TransformationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTransformationRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Transformations.Update));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        var command = new UpdateTransformationCommand(
            id,
            request.Name,
            request.Description,
            request.Config,
            tenantId,
            userId);

        var transformation = await _bus.InvokeAsync<TransformationResponse>(command);
        return Ok(transformation);
    }

    /// <summary>
    /// Delete a transformation
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Transformations.Delete));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var command = new DeleteTransformationCommand(id, tenantId);
        await _bus.InvokeAsync(command);
        return NoContent();
    }
}
