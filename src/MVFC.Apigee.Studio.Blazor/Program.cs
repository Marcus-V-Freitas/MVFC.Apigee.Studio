/// <summary>
/// Entry point for the MVFC.Apigee.Studio Blazor application.
/// Configures services, middleware, and application pipeline.
/// </summary>
var builder = WebApplication.CreateBuilder(args);

/// <summary>
/// Adds Blazor server-side components and infrastructure services.
/// </summary>
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddInfrastructure(builder.Configuration);

/// <summary>
/// Configures a named HttpClient ("EmulatorRuntime") for reverse proxy to the emulator runtime (default: http://localhost:8998).
/// </summary>
var runtimeUrl = builder.Configuration["EmulatorRuntime:BaseUrl"] ?? "http://localhost:8998";
builder.Services.AddHttpClient("EmulatorRuntime", client =>
{
    client.BaseAddress = new Uri(runtimeUrl);
    client.Timeout     = TimeSpan.FromSeconds(30);
});

/// <summary>
/// Registers application use cases for dependency injection.
/// </summary>
builder.Services.AddScoped<CreateWorkspaceUseCase>();
builder.Services.AddScoped<DeployToEmulatorUseCase>();
builder.Services.AddScoped<GeneratePolicyUseCase>();

/// <summary>
/// Registers UI and linting services for dependency injection.
/// </summary>
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<ApigeeLintService>();
builder.Services.AddScoped<EditorStateService>();
builder.Services.AddSingleton<SkeletonTemplateService>();

/// <summary>
/// Registers the proxy trace service as a singleton to persist across middleware requests.
/// </summary>
builder.Services.AddSingleton<IProxyTraceService, ProxyTraceService>();

var app = builder.Build();

/// <summary>
/// Configures error handling for non-development environments.
/// </summary>
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();


/// <summary>
/// Maps the root Blazor component and enables interactive server render mode.
/// </summary>
app.MapRazorComponents<MVFC.Apigee.Studio.Blazor.Components.App>().AddInteractiveServerRenderMode();

await app.RunAsync();
