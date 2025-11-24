using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Interfaces;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    public IActionResult GetAllMetadata()
    {
        var metadata = _metadataService.GetAllMetadata();
        return Ok(metadata);
    }

    /// <summary>
    /// Get connector configuration metadata
    /// </summary>
    [HttpGet("connector-config")]
    public IActionResult GetConnectorConfig()
    {
        var config = _metadataService.GetConnectorConfig();
        return Ok(config);
    }

    /// <summary>
    /// Get transformation types metadata
    /// </summary>
    [HttpGet("transformation-types")]
    public IActionResult GetTransformationTypes()
    {
        var types = _metadataService.GetTransformationTypes();
        return Ok(types);
    }

    /// <summary>
    /// Get data types metadata
    /// </summary>
    [HttpGet("data-types")]
    public IActionResult GetDataTypes()
    {
        var types = _metadataService.GetDataTypes();
        return Ok(types);
    }

    /// <summary>
    /// Get schedule frequencies metadata
    /// </summary>
    [HttpGet("schedule-frequencies")]
    public IActionResult GetScheduleFrequencies()
    {
        var frequencies = _metadataService.GetScheduleFrequencies();
        return Ok(frequencies);
    }

    /// <summary>
    /// Get days of week metadata
    /// </summary>
    [HttpGet("days-of-week")]
    public IActionResult GetDaysOfWeek()
    {
        var days = _metadataService.GetDaysOfWeek();
        return Ok(days);
    }
}
