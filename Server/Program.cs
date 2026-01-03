using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Routing.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.OData.ModelBuilder;
using Radzen;
using SmartNest.Server.Data;
using SmartNest.Server.Services;
using SmartNest.Server.Hubs;
using Microsoft.AspNetCore.Identity;
using SmartNest.Server.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Npgsql;
using System.Text;

try
{
    Console.WriteLine("========================================");
    Console.WriteLine("🚀 DÉMARRAGE SMARTNEST");
    Console.WriteLine("========================================");
var builder = WebApplication.CreateBuilder(args);

// ========================================
// CONFIGURATION DU PORT - MÉTHODE SIMPLE
// ========================================
Console.WriteLine("🔧 Configuration du port...");
Console.WriteLine($"🌍 Environnement: {builder.Environment.EnvironmentName}");

// Configuration automatique du port via Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
  try
  {
    // Render définit la variable PORT
    var portString = Environment.GetEnvironmentVariable("PORT");
    
    if (!string.IsNullOrEmpty(portString) && int.TryParse(portString, out int port))
    {
        Console.WriteLine($"📡 Port détecté depuis variable d'environnement: {port}");
        serverOptions.ListenAnyIP(port);
    }
    else
    {
        // Fallback pour développement local
        Console.WriteLine("📡 Utilisation du port par défaut: 5000");
        serverOptions.ListenAnyIP(5000);
    }
    
    // Limites pour optimisation mémoire
    serverOptions.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    serverOptions.Limits.MaxConcurrentConnections = 50;
  }
  catch (Exception ex)
  {
       Console.WriteLine($"❌ ERREUR configuration Kestrel: {ex.Message}");
       throw;
  }
});


// =========================================
// 🔧 Services de base
// =========================================
try
{
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERREUR ajout services de base: {ex.Message}");
    throw;
}
// =========================================
// 🧩 Services Radzen
// =========================================
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<Radzen.NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();

// =========================================
// 🌐 HTTP Client (auto-baseAddress)
// =========================================
builder.Services.AddSingleton(sp =>
{
    var server = sp.GetRequiredService<IServer>();
    var addressFeature = server.Features.Get<IServerAddressesFeature>();
    string baseAddress = addressFeature!.Addresses.First();
    return new HttpClient
    {
        BaseAddress = new Uri(baseAddress)
    };
});

// =========================================
// 🗄️ Configuration des bases de données
// =========================================
Console.WriteLine("========== 🔍 CONFIGURATION DEBUG ==========");
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"ContentRootPath: {builder.Environment.ContentRootPath}");

// Récupérer la connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"🔗 DefaultConnection: {connectionString ?? "NULL"}");

// Afficher toutes les ConnectionStrings disponibles
Console.WriteLine("\n📋 Toutes les ConnectionStrings:");
var connectionStrings = builder.Configuration.GetSection("ConnectionStrings");
foreach (var child in connectionStrings.GetChildren())
{
    Console.WriteLine($"  ✓ {child.Key} = {child.Value}");
}
Console.WriteLine("============================================\n");

// Validation
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("⚠️ DefaultConnection manquante, tentative de récupération depuis les variables d'environnement...");
    connectionString = builder.Configuration["ConnectionStrings__DefaultConnection"];
    
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("❌ ERREUR CRITIQUE: Aucune chaîne de connexion trouvée!");
    }
    
    Console.WriteLine($"✅ Connection string récupérée: {connectionString}");
}

// ========================================
// CONFIGURATION DE LA BASE DE DONNÉES
// ========================================
Console.WriteLine("🔍 === DÉBUT CONFIGURATION DATABASE ===");

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
Console.WriteLine($"📋 DATABASE_URL présente: {!string.IsNullOrEmpty(databaseUrl)}");

if (!string.IsNullOrEmpty(databaseUrl))
{
    Console.WriteLine($"🌐 DATABASE_URL (premiers 50 car): {databaseUrl.Substring(0, Math.Min(50, databaseUrl.Length))}...");
    
    try
    {
        // Production : Render
        var connectiondbString = ConvertDatabaseUrl(databaseUrl);
        Console.WriteLine($"✅ ConnectionString convertie: {connectiondbString.Substring(0, Math.Min(100, connectiondbString.Length))}...");
        
        // Afficher les détails de la connexion (sans le mot de passe)
        var details = connectiondbString.Split(';');
        foreach (var detail in details)
        {
            if (!detail.Contains("Password", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"   • {detail}");
            }
            else
            {
                Console.WriteLine($"   • Password=***");
            }
        }
        
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectiondbString);
            options.EnableDetailedErrors();
            options.EnableSensitiveDataLogging(); // Seulement pour debug
        });
        
        Console.WriteLine("✅ DbContext configuré avec PostgreSQL (Production)");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERREUR lors de la conversion DATABASE_URL: {ex.Message}");
        Console.WriteLine($"📋 Stack trace: {ex.StackTrace}");
        throw;
    }
}
else
{
    Console.WriteLine("⚠️  DATABASE_URL non trouvée, utilisation configuration locale");
    
    // Développement : local
    var connectiondbString = builder.Configuration.GetConnectionString("DefaultConnection");
    Console.WriteLine($"📋 DefaultConnection: {connectiondbString ?? "NULL"}");
    
    if (string.IsNullOrEmpty(connectiondbString))
    {
        Console.WriteLine("❌ ERREUR: Aucune ConnectionString disponible!");
        throw new InvalidOperationException("ConnectionString manquante");
    }
    
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(connectiondbString);
        options.EnableDetailedErrors();
    });
    
    Console.WriteLine("✅ DbContext configuré avec PostgreSQL (Local)");
}

Console.WriteLine("🔍 === FIN CONFIGURATION DATABASE ===");
Console.WriteLine("");

builder.Services.AddControllers();

// =========================================
// 🧠 Services métier
// =========================================

// Configuration Email
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

// Configuration SMS
builder.Services.Configure<SmsSettings>(
    builder.Configuration.GetSection("SmsSettings"));
builder.Services.AddHttpClient();
builder.Services.AddScoped<ISmsService, SmsService>();

// Services
builder.Services.AddScoped<SmartNest.Server.postgresService>();
builder.Services.AddSingleton<MqttService>();
builder.Services.AddSingleton<IYoloProcessManager, YoloProcessManager>();
builder.Services.AddHostedService<YoloServerHostedService>();
builder.Services.AddScoped<INotificationService, NotificationUIService>();
builder.Services.AddScoped<INotificationSettingsService, NotificationSettingsService>();
builder.Services.AddScoped<INotificationDispatcherService, NotificationDispatcherService>();
builder.Services.AddScoped<IVideoStreamService, VideoStreamService>();
builder.Services.AddScoped<IChickMonitoringService, ChickMonitoringService>();
builder.Services.AddScoped<IYoloAnalysisService, YoloAnalysisService>();
builder.Services.AddHostedService<NotificationMonitoringService>();

// HTTP Clients
builder.Services.AddHttpClient<IYoloAnalysisService, YoloAnalysisService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IVideoStreamService, VideoStreamService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["BaseUrl"] ?? "https://localhost:7001/");
});

builder.Services.AddHttpClient("SmartNest.Server").AddHeaderPropagation(o => o.Headers.Add("Cookie"));
builder.Services.AddHeaderPropagation(o => o.Headers.Add("Cookie"));

// =========================================
// ⚡ SignalR
// =========================================
builder.Services.AddSignalR();

// =========================================
// 📦 OData Configuration
// =========================================
builder.Services.AddControllers().AddOData(opt =>
{
    // OData PostgreSQL
    var postgresBuilder = new ODataConventionModelBuilder();
    postgresBuilder.EntitySet<SmartNest.Server.Models.postgres.Chick>("Chicks");
    postgresBuilder.EntitySet<SmartNest.Server.Models.postgres.ChickStatistics>("ChickStatistics");
    postgresBuilder.EntitySet<SmartNest.Server.Models.postgres.device>("Devices");
    postgresBuilder.EntitySet<SmartNest.Server.Models.postgres.Notification>("Notifications");
    postgresBuilder.EntitySet<SmartNest.Server.Models.postgres.Sensordatum>("Sensordata");

    opt.AddRouteComponents("odata/postgres", postgresBuilder.GetEdmModel())
        .Select().Filter().OrderBy().Expand().Count().SetMaxTop(100).TimeZone = TimeZoneInfo.Utc;
    
    // OData Identity
    var oDataBuilder = new ODataConventionModelBuilder();
    oDataBuilder.EntitySet<ApplicationUser>("ApplicationUsers");
    var usersType = oDataBuilder.StructuralTypes.First(x => x.ClrType == typeof(ApplicationUser));
    usersType.AddProperty(typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.Password)));
    usersType.AddProperty(typeof(ApplicationUser).GetProperty(nameof(ApplicationUser.ConfirmPassword)));
    oDataBuilder.EntitySet<ApplicationRole>("ApplicationRoles");
    oDataBuilder.EntitySet<ApplicationTenant>("ApplicationTenants");
    
    opt.AddRouteComponents("odata/Identity", oDataBuilder.GetEdmModel())
        .Count().Filter().OrderBy().Expand().Select().SetMaxTop(null).TimeZone = TimeZoneInfo.Utc;
});

// =========================================
// 🔐 Authentication & Authorization
// =========================================
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddScoped<SmartNest.Client.SecurityService>();

// Multi-tenancy User Store
builder.Services.AddTransient<IUserStore<ApplicationUser>, MultiTenancyUserStore>();

// Authentication State Provider
builder.Services.AddScoped<AuthenticationStateProvider, SmartNest.Client.ApplicationAuthenticationStateProvider>();

// Localization
builder.Services.AddLocalization();

// Identity Configuration
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    
    options.User.RequireUniqueEmail = true;
    
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager<SignInManager<ApplicationUser>>()
.AddDefaultTokenProviders();

// CORS
try
{
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERREUR configuration services additionnels: {ex.Message}");
    throw;
}
// Optimisation mémoire pour 512 MB
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Cookie Configuration
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
    options.SlidingExpiration = true;

    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});

// =========================================
// 🚀 Build Application
// =========================================
Console.WriteLine("");
Console.WriteLine("🏗️  Construction de l'application...");
    
var app = builder.Build();
Console.WriteLine("✅ Application construite");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    Console.WriteLine("✅ Swagger activé (dev)");
}

// =========================================
// 🗄️ DATABASE INITIALIZATION (Docker-Ready)
// =========================================
Console.WriteLine("\n========== 🗄️ DATABASE INITIALIZATION ==========");

try
{
    try
    {
        Console.WriteLine("⏳ Connexion à PostgreSQL...");
    
        using var scope1 = app.Services.CreateScope();
        var db = scope1.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
        Console.WriteLine("🔌 Test de connexion...");
        var canConnect = await db.Database.CanConnectAsync();
        Console.WriteLine($"   Connexion: {(canConnect ? "✅ OK" : "❌ ÉCHEC")}");
    
        if (canConnect)
        {
            Console.WriteLine("📊 Application des migrations...");
            await db.Database.MigrateAsync();
        
            Console.WriteLine("📋 Tables dans la base:");
            var tables = db.Model.GetEntityTypes().Select(t => t.GetTableName()).ToList();
            foreach (var table in tables)
            {
                Console.WriteLine($"   • {table}");
            }
        
            Console.WriteLine("✅ Base de données PostgreSQL prête");
        }
        else
        {
            Console.WriteLine("❌ Impossible de se connecter à PostgreSQL");
        }
    }
    catch (Npgsql.NpgsqlException ex)
    {
        Console.WriteLine($"❌ ERREUR PostgreSQL: {ex.Message}");
        Console.WriteLine($"   Code: {ex.ErrorCode} | SqlState: {ex.SqlState}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"   Inner: {ex.InnerException.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERREUR: {ex.GetType().Name}");
        Console.WriteLine($"   Message: {ex.Message}");
        Console.WriteLine($"   Stack: {ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0))}");
    }
    
    // =========================================
    // 👥 Seed Roles & Admin User
    // =========================================
    Console.WriteLine("\n👥 Initializing Roles & Admin User...");
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    // Récupérer TOUS les contextes
    var DbContext = services.GetRequiredService<ApplicationDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
    
    // Créer les rôles
    string[] roleNames = { "Admin", "User", "Manager" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var roleResult = await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
            if (roleResult.Succeeded)
            {
                Console.WriteLine($"  ✅ Role '{roleName}' created");
            }
            else
            {
                Console.WriteLine($"  ❌ Failed to create role '{roleName}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            Console.WriteLine($"  ℹ️ Role '{roleName}' already exists");
        }
    }
    
    // Créer l'utilisateur admin
    var adminEmail = "admin@smartnest.com";
    var adminPassword = "Admin@123";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };
        
        var createResult = await userManager.CreateAsync(adminUser, adminPassword);
        
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            Console.WriteLine($"✅ Admin user created successfully!");
            Console.WriteLine($"   📧 Email: {adminEmail}");
            Console.WriteLine($"   🔑 Password: {adminPassword}");
        }
        else
        {
            Console.WriteLine($"❌ Failed to create admin user:");
            foreach (var error in createResult.Errors)
            {
                Console.WriteLine($"   - {error.Description}");
            }
        }
    }
    else
    {
        Console.WriteLine($"ℹ️ Admin user already exists");
    }
    
    // Seed Tenants Admin (si la méthode existe)
    try
    {
        await DbContext!.SeedTenantsAdmin();
        Console.WriteLine("✅ Tenants seeded successfully");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "⚠️ Could not seed tenants (method may not exist)");
    }
    
    Console.WriteLine("============================================\n");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ CRITICAL ERROR during database initialization:");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    
    // En production, on peut décider de ne pas crasher l'app
    if (!app.Environment.IsDevelopment())
    {
        Console.WriteLine("⚠️ Continuing without database initialization (Production mode)");
    }
    else
    {
        throw; // En dev, on crash pour voir l'erreur
    }
}

// =========================================
// 🧭 Middleware Pipeline
// =========================================
app.MapHub<RealtimeHub>("/realtimeHub");

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}


//app.UseHttpsRedirection();
try
{
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
Console.WriteLine("✅ Fichiers statiques configurés");
app.UseHeaderPropagation();
app.UseRequestLocalization(options => 
    options.AddSupportedCultures("en", "fr")
           .AddSupportedUICultures("en", "fr")
           .SetDefaultCulture("fr"));
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
Console.WriteLine("✅ Routing et Authorization configurés");
app.UseCors("AllowAll");
app.UseResponseCompression();
Console.WriteLine("✅ CORS et Compression activés");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERREUR configuration middleware: {ex.Message}");
    throw;
}

try
{
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
Console.WriteLine("✅ Health check configuré");
app.MapRazorPages();
app.MapControllers();
Console.WriteLine("✅ Controllers mappés");
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
Console.WriteLine("✅ Fallback configuré");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERREUR configuration endpoints: {ex.Message}");
    throw;
}

Console.WriteLine("🚀 SmartNest application starting...");
Console.WriteLine($"🌍 Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"🔗 Listening on: {string.Join(", ", app.Urls)}");

app.Run();
}
catch (Exception ex)
{
    Console.WriteLine("");
    Console.WriteLine("╔════════════════════════════════════════╗");
    Console.WriteLine("║  ❌ ERREUR FATALE AU DÉMARRAGE        ║");
    Console.WriteLine("╚════════════════════════════════════════╝");
    Console.WriteLine($"Type: {ex.GetType().FullName}");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine("");
    Console.WriteLine("Stack Trace:");
    Console.WriteLine(ex.StackTrace);
    Console.WriteLine("");
    
    if (ex.InnerException != null)
    {
        Console.WriteLine("Inner Exception:");
        Console.WriteLine($"Type: {ex.InnerException.GetType().FullName}");
        Console.WriteLine($"Message: {ex.InnerException.Message}");
        Console.WriteLine("");
        Console.WriteLine("Inner Stack Trace:");
        Console.WriteLine(ex.InnerException.StackTrace);
    }
    
    Console.WriteLine("========================================");
    
    // Quitter avec code d'erreur
    Environment.Exit(1);
}

static string ConvertDatabaseUrl(string databaseUrl)
{
    Console.WriteLine("🔄 === CONVERSION DATABASE_URL ===");
    
    try
    {
        Console.WriteLine($"📥 Input URL: {databaseUrl.Substring(0, Math.Min(50, databaseUrl.Length))}...");
        
        var databaseUri = new Uri(databaseUrl);
        Console.WriteLine($"✅ URI parsée avec succès");
        Console.WriteLine($"   • Host: {databaseUri.Host}");
        Console.WriteLine($"   • Port détecté: {databaseUri.Port}");
        
        // ⚠️ CORRECTION : Si le port est -1, utiliser le port par défaut PostgreSQL
        var port = databaseUri.Port == -1 ? 5432 : databaseUri.Port;
        Console.WriteLine($"   • Port utilisé: {port}");
        Console.WriteLine($"   • Database: {databaseUri.LocalPath.TrimStart('/')}");
        
        var userInfo = databaseUri.UserInfo.Split(':');
        Console.WriteLine($"   • Username: {userInfo[0]}");
        Console.WriteLine($"   • Password: {new string('*', userInfo.Length > 1 ? userInfo[1].Length : 0)}");
        
        if (userInfo.Length != 2)
        {
            throw new ArgumentException($"UserInfo invalide. Parties trouvées: {userInfo.Length}");
        }
        
        var connectionString = new System.Text.StringBuilder();
        connectionString.Append($"Host={databaseUri.Host};");
        connectionString.Append($"Port={port};"); // ✅ Utiliser le port corrigé
        connectionString.Append($"Database={databaseUri.LocalPath.TrimStart('/')};");
        connectionString.Append($"Username={userInfo[0]};");
        connectionString.Append($"Password={userInfo[1]};");
        connectionString.Append("SSL Mode=Require;");
        connectionString.Append("Trust Server Certificate=true");
        
        var result = connectionString.ToString();
        Console.WriteLine($"✅ ConnectionString générée (longueur: {result.Length})");
        Console.WriteLine("🔄 === FIN CONVERSION ===");
        
        return result;
    }
    catch (UriFormatException ex)
    {
        Console.WriteLine($"❌ ERREUR: Format d'URI invalide");
        Console.WriteLine($"   Message: {ex.Message}");
        throw new ArgumentException($"Format DATABASE_URL invalide: {ex.Message}", ex);
    }
    catch (IndexOutOfRangeException ex)
    {
        Console.WriteLine($"❌ ERREUR: UserInfo mal formaté");
        Console.WriteLine($"   Message: {ex.Message}");
        throw new ArgumentException("DATABASE_URL: UserInfo invalide (format attendu: user:password)", ex);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERREUR inattendue: {ex.GetType().Name}");
        Console.WriteLine($"   Message: {ex.Message}");
        throw;
    }
}
