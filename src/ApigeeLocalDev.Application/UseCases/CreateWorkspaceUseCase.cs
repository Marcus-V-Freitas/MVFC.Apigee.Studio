using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Application.UseCases;

public sealed class CreateWorkspaceUseCase(IWorkspaceRepository repository)
{
    public ApigeeWorkspace Execute(string name, string? customPath)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workspace name cannot be empty.", nameof(name));

        return repository.Create(name, customPath);
    }
}
