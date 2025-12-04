using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Models;
using MultiTenantETL.Application.Transformations;
using MultiTenantETL.Application.Transformations.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Authorization.Requirements;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransformationsController : ControllerBase
{
    private readonly ITransformationService _transformationService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<TransformationsController> _logger;

    public TransformationsController(
        ITransformationService transformationService,
        IAuthorizationService authorizationService,
        ILogger<TransformationsController> logger)
    {
        _transformationService = transformationService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Get all transformations with optional filters and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedTransformationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll([FromQuery] TransformationSearchRequest request)
    {
        try
        {
            // Check permission
            var authResult = await _authorizationService.AuthorizeAsync(
                User,
                null,
                new PermissionRequirement(Permissions.Transformations.Read));

            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var result = await _transformationService.GetAllAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transformations");
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while retrieving transformations",
                new[] { ex.Message }
            ));
        }
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
        try
        {
            // Check permission
            var authResult = await _authorizationService.AuthorizeAsync(
                User,
                null,
                new PermissionRequirement(Permissions.Transformations.Read));

            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var transformation = await _transformationService.GetByIdAsync(id);
            return Ok(transformation);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(
                AuthErrorCode.UserNotFound,
                ex.Message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transformation {TransformationId}", id);
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while retrieving the transformation",
                new[] { ex.Message }
            ));
        }
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
        try
        {
            // Check permission
            var authResult = await _authorizationService.AuthorizeAsync(
                User,
                null,
                new PermissionRequirement(Permissions.Transformations.Create));

            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var transformation = await _transformationService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = transformation.Id }, transformation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(
                AuthErrorCode.ValidationError,
                ex.Message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transformation");
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while creating the transformation",
                new[] { ex.Message }
            ));
        }
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
        try
        {
            // Check permission
            var authResult = await _authorizationService.AuthorizeAsync(
                User,
                null,
                new PermissionRequirement(Permissions.Transformations.Update));

            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var transformation = await _transformationService.UpdateAsync(id, request);
            return Ok(transformation);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(
                AuthErrorCode.UserNotFound,
                ex.Message
            ));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(
                AuthErrorCode.ValidationError,
                ex.Message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating transformation {TransformationId}", id);
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while updating the transformation",
                new[] { ex.Message }
            ));
        }
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
        try
        {
            // Check permission
            var authResult = await _authorizationService.AuthorizeAsync(
                User,
                null,
                new PermissionRequirement(Permissions.Transformations.Delete));

            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            await _transformationService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(
                AuthErrorCode.UserNotFound,
                ex.Message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting transformation {TransformationId}", id);
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while deleting the transformation",
                new[] { ex.Message }
            ));
        }
    }
}
