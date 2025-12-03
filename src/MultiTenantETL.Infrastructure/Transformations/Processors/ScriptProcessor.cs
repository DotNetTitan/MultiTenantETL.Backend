using System.Diagnostics;
using System.Text.Json;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Transformations;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.Configuration;

namespace MultiTenantETL.Infrastructure.Transformations.Processors;

/// <summary>
/// Executes JavaScript code on rows using Jint (sandboxed JavaScript engine)
/// Supports: row transformation, filtering, and custom logic
/// </summary>
public class ScriptProcessor : ITransformationProcessor
{
    private readonly ILogger<ScriptProcessor> _logger;
    private readonly EtlSettings _settings;

    public string TransformationType => "Script";

    public ScriptProcessor(ILogger<ScriptProcessor> logger, IOptions<EtlSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public Task<TransformationResult> ProcessBatchAsync(
        ReadBatch batch,
        Transformation transformation,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new TransformationResult
        {
            BatchId = batch.BatchId,
            RowsProcessed = batch.RowCount
        };

        try
        {
            var config = ParseConfig(transformation.ConfigJson);
            var transformedRows = new List<Dictionary<string, object?>>();

            // Create Jint engine with security constraints
            var engine = CreateSecureEngine(config);

            for (int i = 0; i < batch.Rows.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var row = batch.Rows[i];
                
                try
                {
                    var transformedRow = ExecuteScript(engine, row, config, i);
                    
                    if (transformedRow != null)
                    {
                        transformedRows.Add(transformedRow);
                    }
                    else
                    {
                        // Script returned null/undefined - filter out this row
                        result.RowsFiltered++;
                    }
                }
                catch (JavaScriptException jsEx)
                {
                    result.RowsWithErrors++;
                    result.Errors.Add(new TransformationError
                    {
                        RowIndex = i,
                        Message = $"JavaScript error: {jsEx.Message}",
                        ErrorCode = "SCRIPT_ERROR",
                        RowData = row
                    });
                    
                    // Include original row on error if configured
                    if (config.IncludeErrorRows)
                    {
                        transformedRows.Add(row);
                    }
                }
                catch (Exception ex)
                {
                    result.RowsWithErrors++;
                    result.Errors.Add(new TransformationError
                    {
                        RowIndex = i,
                        Message = ex.Message,
                        ErrorCode = "EXECUTION_ERROR",
                        RowData = row
                    });
                    
                    if (config.IncludeErrorRows)
                    {
                        transformedRows.Add(row);
                    }
                }
            }

            result.TransformedRows = transformedRows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script transformation failed for batch {BatchId}", batch.BatchId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            result.ExecutionTime = stopwatch.Elapsed;
        }

        return Task.FromResult(result);
    }

    private Engine CreateSecureEngine(ScriptConfig config)
    {
        var engine = new Engine(options =>
        {
            // Security constraints
            options.LimitRecursion(config.MaxRecursionDepth);
            options.TimeoutInterval(TimeSpan.FromMilliseconds(config.TimeoutMs));
            options.MaxStatements(config.MaxStatements);
            
            // Enable strict mode for better security
            options.Strict();
        });

        // Add safe utility functions
        engine.SetValue("log", new Action<object>(obj => 
        {
            if (_settings.EnableDetailedLogging)
            {
                _logger.LogDebug("Script log: {Message}", obj?.ToString() ?? "null");
            }
        }));

        return engine;
    }

    private Dictionary<string, object?>? ExecuteScript(Engine engine, Dictionary<string, object?> row, ScriptConfig config, int rowIndex)
    {
        // Set row data in engine
        engine.SetValue("row", row);
        engine.SetValue("rowIndex", rowIndex);

        // Execute the script
        var scriptResult = engine.Evaluate(config.Script);

        // If the script defined a function but didn't return a value (undefined),
        // check if there's a 'transform' function we should call
        if (scriptResult.IsUndefined())
        {
            var transformFunc = engine.GetValue("transform");
            if (transformFunc.IsObject() && transformFunc.AsObject() is Jint.Native.Function.Function)
            {
                // Call the transform function with the row
                scriptResult = engine.Invoke("transform", row);
            }
        }

        // Handle different return types
        if (scriptResult.IsNull() || scriptResult.IsUndefined())
        {
            return null; // Filter out this row
        }

        if (scriptResult.IsBoolean())
        {
            // If script returns boolean, use it as filter
            return scriptResult.AsBoolean() ? row : null;
        }

        if (scriptResult.IsObject())
        {
            // Script returned an object - convert to dictionary
            return ConvertJsObjectToDictionary(scriptResult.AsObject());
        }

        // Unexpected return type
        throw new InvalidOperationException($"Script returned unexpected type: {scriptResult.Type}");
    }

    private Dictionary<string, object?> ConvertJsObjectToDictionary(Jint.Native.Object.ObjectInstance jsObject)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var property in jsObject.GetOwnProperties())
        {
            var key = property.Key.ToString();
            var value = property.Value.Value;

            dict[key] = ConvertJsValue(value);
        }

        return dict;
    }

    private object? ConvertJsValue(JsValue value)
    {
        if (value.IsNull() || value.IsUndefined())
            return null;

        if (value.IsBoolean())
            return value.AsBoolean();

        if (value.IsNumber())
        {
            var num = value.AsNumber();
            // Try to preserve integer types
            if (num % 1 == 0 && num >= int.MinValue && num <= int.MaxValue)
                return (int)num;
            if (num % 1 == 0 && num >= long.MinValue && num <= long.MaxValue)
                return (long)num;
            return num;
        }

        if (value.IsString())
            return value.AsString();

        if (value.IsDate())
            return value.AsDate().ToDateTime();

        if (value.IsArray())
        {
            var array = value.AsArray();
            var list = new List<object?>();
            for (uint i = 0; i < array.Length; i++)
            {
                list.Add(ConvertJsValue(array.Get(i.ToString())));
            }
            return list;
        }

        if (value.IsObject())
            return ConvertJsObjectToDictionary(value.AsObject());

        return value.ToString();
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private ScriptConfig ParseConfig(string configJson)
    {
        return JsonSerializer.Deserialize<ScriptConfig>(configJson, s_jsonOptions)
            ?? throw new InvalidOperationException("Invalid script configuration");
    }

    private class ScriptConfig
    {
        /// <summary>
        /// JavaScript code to execute
        /// Should return: transformed row object, boolean (filter), or null/undefined (filter out)
        /// </summary>
        public string Script { get; set; } = string.Empty;

        /// <summary>
        /// Maximum recursion depth (default: 100)
        /// </summary>
        public int MaxRecursionDepth { get; set; } = 100;

        /// <summary>
        /// Script timeout in milliseconds (default: 5000ms = 5 seconds)
        /// </summary>
        public int TimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Maximum number of statements to execute (default: 10000)
        /// </summary>
        public int MaxStatements { get; set; } = 10000;

        /// <summary>
        /// Whether to include rows that had errors in the output
        /// </summary>
        public bool IncludeErrorRows { get; set; } = false;
    }
}
