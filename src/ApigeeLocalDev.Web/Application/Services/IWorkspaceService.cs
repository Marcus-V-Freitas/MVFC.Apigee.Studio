using ApigeeLocalDev.Web.Domain.Models;

namespace ApigeeLocalDev.Web.Application.Services;

public interface IWorkspaceService
{
    Task<IReadOnlyList<Workspace>> ListWorkspacesAsync();
    Task<WorkspaceDetail?> GetWorkspaceDetailAsync(string workspaceName);
}
