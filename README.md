# apigee-localdev-blazor

Blazor UI and tooling for managing Apigee Emulator workspaces, bundles, and policy templates in .NET 8.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Running the app

```bash
cd src/ApigeeLocalDev.Web
dotnet run
```

The app listens on `http://localhost:5000` (or the URL printed in the console).

## Configuration

All settings are in `src/ApigeeLocalDev.Web/appsettings.json` (or via environment variables / user secrets).

| Key | Default | Description |
|-----|---------|-------------|
| `Workspaces:WorkspacesRoot` | `../../workspaces` | Path to the root folder that contains workspace subfolders. Relative paths are resolved from the app's content root (`src/ApigeeLocalDev.Web`). |
| `ApigeeEmulator:BaseUrl` | `http://localhost:8080` | Base URL of the running Apigee Emulator. |
| `TestBundles:SampleZipPath` | `bundles/sample.zip` | Path to a sample ZIP bundle used by the Deploy page. |

### Workspaces folder structure

```
workspaces/
  my-workspace/
    apiproxies/
      my-proxy/
    sharedflows/
      my-flow/
    environments/
      test/
        config.json
```

Each workspace is a **direct subfolder** of `WorkspacesRoot`. The app automatically lists `apiproxies`, `sharedflows`, and `environments` subfolders and shows any `.xml`, `.json`, `.yaml`, or `.yml` files for in-browser editing.

A sample workspace is already included under `workspaces/sample-workspace/`.

## Project structure

```
src/
  ApigeeLocalDev.Web/
    Domain/          # Models (Workspace, WorkspaceDetail, TextFile)
    Application/     # Service interfaces (IWorkspaceService, IFileService, IApigeeEmulatorClient)
    Infrastructure/  # Concrete services (WorkspaceService, FileService, ApigeeEmulatorClient)
    Components/      # Blazor pages and layout (Presentation)
    Program.cs
    appsettings.json
workspaces/          # Sample workspaces
bundles/             # Sample ZIP bundles for deploy testing
```

## Deploying to the Apigee Emulator

1. Start your Apigee Emulator and note its URL.
2. Set `ApigeeEmulator:BaseUrl` to the emulator URL.
3. Set `TestBundles:SampleZipPath` to the path of your API proxy ZIP bundle.
4. Navigate to a workspace and click **Deploy to Emulator →**.
5. Choose the target environment and confirm the ZIP path, then click **Deploy**.
