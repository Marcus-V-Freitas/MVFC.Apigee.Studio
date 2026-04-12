using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Application.UseCases;

public sealed class CreateWorkspaceUseCase(IWorkspaceRepository repository)
{
    public ApigeeWorkspace Execute(string name, string path)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workspace name cannot be empty.", nameof(name));

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        return repository.Create(name, path);
    }
}
