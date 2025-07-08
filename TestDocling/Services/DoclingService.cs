using Azure.Core;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using System.Net.Http.Headers;
using TestDocling.Models;

namespace TestDocling.Services;

public class DoclingService : IDoclingService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<DoclingService> _logger;

    public DoclingService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<DoclingService> logger)
        {
            _httpClient = httpClientFactory.CreateClient("DoclingClient");
            _config = config;
            _logger = logger;
    }
    
    public async Task<TaskStatusResponse> StartFileConvertAsync(IFormFile file)
    {
        if (!FileFormatHelper.TryGetDoclingFormat(file.FileName, out string fromFormat))
            throw new NotSupportedException($"Unsupported file format: {Path.GetExtension(file.FileName)}");
        
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

        var response = await _httpClient.PostAsync("/v1alpha/convert/file/async", form);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TaskStatusResponse>();
    }
    public async Task<TaskStatusResponse?> GetTaskStatusAsync(string taskId)
    {
        return await _httpClient.GetFromJsonAsync<TaskStatusResponse>($"/v1alpha/status/poll/{taskId}");
    }
    public async Task<TaskResultResponse?> GetTaskResultAsync(string taskId)
    {
        return await _httpClient.GetFromJsonAsync<TaskResultResponse>($"/v1alpha/result/{taskId}");
    }
}
