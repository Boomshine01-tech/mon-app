using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;
using SmartNest.Client;
using SmartNest.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Globalization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7001";

// HttpClient de base
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl)  });

// Radzen Services
builder.Services.AddRadzenComponents();

// ✅ SERVICES CLIENT SEULEMENT
builder.Services.AddScoped<IVideoStreamService, VideoStreamService>();
builder.Services.AddScoped<IChickMonitoringService, ChickMonitoringService>();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<postgresService>();
builder.Services.AddScoped<Radzen.NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<IYoloDataService, YoloDataService>();
builder.Services.AddScoped<ContextMenuService>();
builder.Services.AddScoped<NotificationUIService>();
builder.Services.AddScoped<SmartNest.Client.postgresService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<RoleService>();
builder.Services.AddScoped<TenantService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<MqttConnectionService>();

builder.Services.AddAuthorizationCore();

builder.Services.AddHttpClient("SmartNest.Server", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddTransient(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("SmartNest.Server"));
builder.Services.AddScoped(sp=>
    new HttpClient {BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)});
builder.Services.AddScoped<INotificationUIService, NotificationUIService>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddScoped<SmartNest.Client.SecurityService>();
builder.Services.AddScoped<AuthenticationStateProvider, SmartNest.Client.ApplicationAuthenticationStateProvider>();
builder.Services.AddLocalization();

var host = builder.Build();
var jsRuntime = host.Services.GetRequiredService<Microsoft.JSInterop.IJSRuntime>();
var culture = await jsRuntime.InvokeAsync<string>("Radzen.getCulture");
if (!string.IsNullOrEmpty(culture))
{
    CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(culture);
    CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(culture);
}

await host.RunAsync();