using ApigeeLocalDev.Application.UseCases;
using ApigeeLocalDev.Blazor.Services;
using ApigeeLocalDev.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<CreateWorkspaceUseCase>();
builder.Services.AddScoped<DeployToEmulatorUseCase>();
builder.Services.AddScoped<GeneratePolicyUseCase>();

// UI services
builder.Services.AddScoped<ToastService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<ApigeeLocalDev.Blazor.Components.App>().AddInteractiveServerRenderMode();

app.Run();
