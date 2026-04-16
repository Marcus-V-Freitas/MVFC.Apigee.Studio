# MVFC.Apigee.Studio — Blazor Server

Blazor Server UI para gerenciamento local de workspaces Apigee, deploy de bundles no Apigee Emulator e geração de políticas a partir de templates.

## Estrutura

```
src/
  MVFC.Apigee.Studio.Domain          → Entidades e interfaces (sem dependências externas)
  MVFC.Apigee.Studio.Application     → Casos de uso
  MVFC.Apigee.Studio.Infrastructure  → FileSystem, HttpClient para o emulator, policy templates
  MVFC.Apigee.Studio.Blazor          → UI Blazor Server (páginas, componentes, CSS)
```

## Como rodar

### Pré-requisitos
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Apigee Emulator rodando via Docker (opcional para deploy):

```bash
docker run -p 8080:8080 gcr.io/apigee-release/hybrid/apigee-emulator:latest
```

### Configuração

Edite `src/MVFC.Apigee.Studio.Blazor/appsettings.Development.json`:

```json
{
  "WorkspacesRoot": "C:/dev/apigee-workspaces",
  "ApigeeEmulator": {
    "BaseUrl": "http://localhost:8080"
  }
}
```

`WorkspacesRoot` é a pasta onde os workspaces serão criados/listados. Em Linux/macOS: `/home/user/apigee-workspaces`.

### Rodar

```bash
cd src/MVFC.Apigee.Studio.Blazor
dotnet run
```

Acesse `https://localhost:5001` (ou a porta exibida no terminal).

## Funcionalidades

| Funcionalidade | Status |
|---|---|
| Listar workspaces | ✅ |
| Criar workspace (estrutura de pastas padrão) | ✅ |
| Navegar árvore de arquivos (apiproxies, sharedflows, environments) | ✅ |
| Visualizar e editar arquivos XML/JSON/YAML | ✅ |
| Empacotar bundle (ZIP) e publicar no Apigee Emulator | ✅ |
| Galeria de policy templates (AssignMessage, VerifyAPIKey, SpikeArrest, OAuthV2, ResponseCache, RaiseFault, ExtractVariables) | ✅ |
| Gerar arquivo XML de política a partir de template + parâmetros | ✅ |
| Status do Apigee Emulator (health check) | ✅ |
| Deploy rápido via ZIP direto | ✅ |

## Extensões futuras

- Gerenciamento de containers Docker do emulator (start/stop/update)
- Suporte a deploy remoto (Apigee X via Management API)
- Editor de XML com syntax highlighting (Monaco via JS Interop)
- Histórico de deploys
- Import/export de workspaces
