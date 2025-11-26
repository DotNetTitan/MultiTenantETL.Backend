using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Interfaces;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[EnableCors("AllowFrontend")]
public class MetadataController : ControllerBase
{
    private readonly IMetadataService _metadataService;

    public MetadataController(IMetadataService metadataService)
    {
        _metadataService = metadataService;
    }

    /// <summary>
    /// Get all metadata at once (for initial app load)
    /// </summary>
    [HttpGet("all")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Language")]
    public IActionResult GetAllMetadata()
    {
        var metadata = _metadataService.GetAllMetadata();
        return Ok(metadata);
    }

    /// <summary>
    /// Get connector configuration metadata
    /// </summary>
    [HttpGet("connector-config")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Language")]
    public IActionResult GetConnectorConfig()
    {
        var config = _metadataService.GetConnectorConfig();
        return Ok(config);
    }

    /// <summary>
    /// Get transformation types metadata
    /// </summary>
    [HttpGet("transformation-types")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Language")]
    public IActionResult GetTransformationTypes()
    {
        var types = _metadataService.GetTransformationTypes();
        return Ok(types);
    }

    /// <summary>
    /// Get data types metadata
    /// </summary>
    [HttpGet("data-types")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Language")]
    public IActionResult GetDataTypes()
    {
        var types = _metadataService.GetDataTypes();
        return Ok(types);
    }

    /// <summary>
    /// Get schedule frequencies metadata
    /// </summary>
    [HttpGet("schedule-frequencies")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Language")]
    public IActionResult GetScheduleFrequencies()
    {
        var frequencies = _metadataService.GetScheduleFrequencies();
        return Ok(frequencies);
    }

    /// <summary>
    /// Get days of week metadata
    /// </summary>
    [HttpGet("days-of-week")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "Accept-Language")]
    public IActionResult GetDaysOfWeek()
    {
        var days = _metadataService.GetDaysOfWeek();
        return Ok(days);
    }

    /// <summary>
    /// Get application constants (roles, OAuth config, supported languages)
    /// </summary>
    [HttpGet("app-constants")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public IActionResult GetAppConstants()
    {
        var constants = _metadataService.GetAppConstants();
        return Ok(constants);
    }
}
