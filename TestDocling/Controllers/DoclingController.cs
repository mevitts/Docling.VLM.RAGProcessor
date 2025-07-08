using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TestDocling.Models;
using TestDocling.Services;

namespace TestDocling.Controllers;

[ApiController]
[Route("api/docling")]
public class DoclingController : ControllerBase
{
    private readonly IDoclingService _doclingService;
    private readonly IDoclingContentProcessorService _doclingContentProcessorService;
    private readonly ILogger<DoclingController> _logger;

    public DoclingController(IDoclingService doclingService, ILogger<DoclingController> logger, IDoclingContentProcessorService doclingContentProcessorService)
    {
        _doclingService = doclingService;
        _logger = logger;
        _doclingContentProcessorService =  doclingContentProcessorService;
    }

    [HttpPost("convert/file/async")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ConvertFile([FromForm] FileUploadRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest("No file uploaded.");
        try
        {
            var taskStatus = await _doclingService.StartFileConvertAsync(request.File);
            return Ok(taskStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting file conversion.");
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    [HttpGet("status/poll{taskId}")]
    public async Task<IActionResult> GetStatus(string taskId)
    {
        try
        {
            var status = await _doclingService.GetTaskStatusAsync(taskId);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task status for {TaskId}.", taskId);
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    [HttpGet("result/{taskId}")]
    public async Task<IActionResult> GetResult(string taskId)
    {
        try
        {
            var result = await _doclingService.GetTaskResultAsync(taskId);
            return Ok(result.Document.DoclingJsonContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting result for {TaskId}.", taskId);
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    [HttpPost("process-file")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ProcessFile([FromForm] FileUploadRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest("No file uploaded.");

        try
        {
            var taskStatus = await _doclingService.StartFileConvertAsync(request.File);
            if (taskStatus.TaskId == null)
                return NotFound("Failed to start conversion.");
            _logger.LogInformation("Task {TaskId} started successfully.", taskStatus.TaskId);

            TaskResultResponse? result = null;
            while (result == null || (result.Status != "success" && result.Status != "failure"))
            {
                await Task.Delay(2000); // Poll every 2 seconds
                var status = await _doclingService.GetTaskStatusAsync(taskStatus.TaskId);
                if (status?.TaskStatus != "success")
                    continue;
                result = await _doclingService.GetTaskResultAsync(taskStatus.TaskId);
            }

            if (result?.Document?.DoclingJsonContent == null)
                return NotFound("No document content found.");
            var processedContent = await _doclingContentProcessorService.ProcessDoclingResponse(result.Document);

            return Ok(processedContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file.");
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }
}
