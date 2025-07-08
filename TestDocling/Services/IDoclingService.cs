using TestDocling.Models;

namespace TestDocling.Services;

public interface IDoclingService
{
    Task<TaskStatusResponse?> StartFileConvertAsync(IFormFile file);
    Task<TaskStatusResponse?> GetTaskStatusAsync(string taskId);
    Task<TaskResultResponse?> GetTaskResultAsync(string taskId);
}
