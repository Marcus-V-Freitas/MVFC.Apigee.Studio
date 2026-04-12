using ApigeeLocalDev.Domain.Entities;
using ApigeeLocalDev.Domain.Interfaces;

namespace ApigeeLocalDev.Application.UseCases;

public sealed class CreateWorkspaceUseCase(IWorkspaceRepository repository)
{
    /// <param name="name">Nome do workspace.</param>
    /// <param name="customPath">Caminho absoluto opcional. Nulo/vazio usa WorkspacesRoot/name.</param>
    /// <param name="initialProxies">Lista de nomes de proxies a criar com estrutura completa (apiproxy/, policies/, proxies/, targets/).</param>
    public ApigeeWorkspace Execute(string name, string? customPath, IReadOnlyList<string>? initialProxies = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workspace name cannot be empty.", nameof(name));

        return repository.Create(name, customPath, initialProxies);
    }
}
