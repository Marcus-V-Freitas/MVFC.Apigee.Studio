using ApigeeLocalDev.Application.UseCases;
using ApigeeLocalDev.Blazor.Middleware;
using ApigeeLocalDev.Blazor.Services;
using ApigeeLocalDev.Domain.Interfaces;
using ApigeeLocalDev.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddInfrastructure(builder.Configuration);

// Proxy reverso para o emulator runtime (:8998)
var runtimeUrl = builder.Configuration["EmulatorRuntime:BaseUrl"] ?? "http://localhost:8998";
builder.Services.AddHttpClient("EmulatorRuntime", client =>
{
    client.BaseAddress = new Uri(runtimeUrl);
    client.Timeout     = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<CreateWorkspaceUseCase>();
builder.Services.AddScoped<DeployToEmulatorUseCase>();
builder.Services.AddScoped<GeneratePolicyUseCase>();

// UI services
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<ApigeeLintService>();

// Trace service — singleton para sobreviver entre requests do middleware
builder.Services.AddSingleton<IProxyTraceService, ProxyTraceService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

// TraceMiddleware intercepta /emulator-runtime/**
app.UseMiddleware<TraceMiddleware>();

app.MapRazorComponents<ApigeeLocalDev.Blazor.Components.App>().AddInteractiveServerRenderMode();

app.Run();
