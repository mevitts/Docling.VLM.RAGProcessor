using Microsoft.AspNetCore.Http;

namespace TestDocling.Models
{
    public class FileUploadRequest
    {
        public IFormFile File { get; set; } = null!;
    }
}