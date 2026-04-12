using ApigeeLocalDev.Web.Application.Services;
using ApigeeLocalDev.Web.Components;
using ApigeeLocalDev.Web.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Options
builder.Services.Configure<WorkspaceOptions>(
    builder.Configuration.GetSection(WorkspaceOptions.SectionName));
builder.Services.Configure<ApigeeEmulatorOptions>(
    builder.Configuration.GetSection(ApigeeEmulatorOptions.SectionName));

// Application services
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IFileService, FileService>();

// ApigeeEmulatorClient via HttpClientFactory
builder.Services.AddHttpClient<IApigeeEmulatorClient, ApigeeEmulatorClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApigeeEmulatorOptions>>();
    client.BaseAddress = new Uri(options.Value.BaseUrl);
});

// Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

