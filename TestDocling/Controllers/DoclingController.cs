using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TestDocling;
using TestDocling.Models;
using TestDocling.Services;

namespace TestDocling.Controllers;

[ApiController]
[Route("api/docling")]
public class DoclingController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IDoclingContentProcessorService _contentProcessorService;

    public DoclingController(IHttpClientFactory httpClientFactory, IDoclingContentProcessorService contentProcessorService)
    {
        string doclingURL = Environment.GetEnvironmentVariable("DOCLING_URL") ?? "http://localhost:5001";
        _httpClient = httpClientFactory.CreateClient("DoclingClient");
        _httpClient.BaseAddress = new Uri(doclingURL);

        _contentProcessorService = contentProcessorService;
    }

    [HttpPost("convert/file")] 
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ConvertFile([FromForm] FileUploadRequest request)
    {
        try
        {
            var file = request.File;
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            if (!FileFormatHelper.TryGetDoclingFormat(file.FileName, out string fromFormat))
                return BadRequest($"Unsupported file format: {Path.GetExtension(file.FileName)}");


            using var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(file.OpenReadStream());
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            form.Add(fileContent, "files", file.FileName);
            form.Add(new StringContent(fromFormat), "from_formats");
            form.Add(new StringContent("md"), "to_formats");
            form.Add(new StringContent("json"), "to_formats");
            form.Add(new StringContent("rapidocr"), "ocr_engine");
            form.Add(new StringContent("pypdfium2"), "pdf_backend");
            form.Add(new StringContent("accurate"), "table_mode");
            form.Add(new StringContent("embedded"), "image_export_mode");
            form.Add(new StringContent("true"), "include_images");
            form.Add(new StringContent("[PAGE BREAK]"), "md_page_break_placeholder");
            form.Add(new StringContent("true"), "do_picture_classification");


            var response = await _httpClient.PostAsync("/v1alpha/convert/file", form);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());

            var jsonString = await response.Content.ReadAsStringAsync();
            var pageContent = await _contentProcessorService.ProcessDoclingResponse(jsonString);
            return Ok(pageContent);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, $"Error processing file: {ex.Message}");
        }
    }
}
