using ApigeeLocalDev.Web.Domain.Models;

namespace ApigeeLocalDev.Web.Application.Services;

public interface IFileService
{
    Task<TextFile?> ReadFileAsync(string workspaceName, string relativePath);
    Task SaveFileAsync(string workspaceName, string relativePath, string content);
}
