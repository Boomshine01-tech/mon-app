using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartNest.Server.Models.postgres; 
using SmartNest.Server.Models;

namespace SmartNest.Server.Data
{
    /// <summary>
    /// Contexte unique de l'application gérant :
    /// - Identity (Users, Roles, Claims, etc.)
    /// - Multi-tenancy (Tenants)
    /// - Données métier (Devices, SensorData, Chicks, Notifications, VideoFrames, etc.)
    /// </summary>
    public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) 
            : base(options)
        {
        }

        public ApplicationDbContext()
        {
        }

        // ==============================
        // DbSets - Multi-tenancy
        // ==============================
        public DbSet<ApplicationTenant> Tenants { get; set; }

        // ==============================
        // DbSets - Données métier
        // ==============================
        public DbSet<device> Devices { get; set; }
        public DbSet<Sensordatum> SensorData { get; set; }
        public DbSet<Chick> Chicks { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<NotificationSettings> NotificationSettings { get; set; }
        public DbSet<NotificationStats> NotificationStats { get; set; }
        public DbSet<VideoFrame> VideoFrames { get; set; }
        public DbSet<VideoStreamSession> VideoStreamSessions { get; set; }

        // ==============================
        // Configuration
        // ==============================
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=awaawa1970");
            }
        }

        // ==============================
        // Modélisation
        // ==============================
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ========== Configuration des tables Identity ==========
            modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers");
            modelBuilder.Entity<ApplicationRole>().ToTable("AspNetRoles");
            modelBuilder.Entity<IdentityUserRole<string>>().ToTable("AspNetUserRoles");
            modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("AspNetUserClaims");
            modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("AspNetUserLogins");
            modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("AspNetRoleClaims");
            modelBuilder.Entity<IdentityUserToken<string>>().ToTable("AspNetUserTokens");

            // ========== Configuration Tenants ==========
            modelBuilder.Entity<ApplicationTenant>(entity =>
            {
                entity.ToTable("AspNetTenants");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Hosts).HasMaxLength(500);
            });

            // ========== Relations Identity + Tenants ==========
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.Roles)
                .WithMany(r => r.Users)
                .UsingEntity<IdentityUserRole<string>>();

            modelBuilder.Entity<ApplicationUser>()
                .HasOne(i => i.ApplicationTenant)
                .WithMany(i => i.Users)
                .HasForeignKey(i => i.TenantId)
                .HasPrincipalKey(i => i.Id);

            modelBuilder.Entity<ApplicationUser>()
                .Property(u => u.TenantId)
                .IsRequired(false);

            modelBuilder.Entity<ApplicationRole>()
                .HasOne(i => i.ApplicationTenant)
                .WithMany(i => i.Roles)
                .HasForeignKey(i => i.TenantId)
                .HasPrincipalKey(i => i.Id);

            // ========== Configuration Devices ==========
            modelBuilder.Entity<device>(entity =>
            {
                entity.ToTable("devices");
                entity.HasKey(e => e.DeviceId);
    
                entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.DeviceName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.DeviceType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.LastUpdated).IsRequired().HasDefaultValueSql("NOW()");
                entity.Property(e => e.StatusMessage).HasMaxLength(500);

                entity.HasIndex(e => e.UserId).HasDatabaseName("IX_Devices_UserId");
                entity.HasIndex(e => new { e.UserId, e.DeviceId })
                    .HasDatabaseName("IX_Devices_UserId_DeviceId")
                    .IsUnique();
                
                entity.HasOne(d => d.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== Configuration Chicks ==========
            modelBuilder.Entity<Chick>(entity =>
            {
                entity.ToTable("chicks");
                entity.HasKey(e => e.ChickId).HasName("pk_chicks");

                entity.Property(e => e.ChickId).HasMaxLength(50).IsRequired();
                entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
                entity.Property(e => e.X).HasColumnName("X").IsRequired();
                entity.Property(e => e.Y).HasColumnName("Y").IsRequired();
                entity.Property(e => e.Confidence).HasColumnType("double precision").IsRequired();
                entity.Property(e => e.healthstate).HasMaxLength(20).HasDefaultValue("Healthy").IsRequired();
                entity.Property(e => e.Age).HasDefaultValue(1).IsRequired();
                entity.Property(e => e.Weight).HasColumnType("double precision").HasDefaultValue(40.0).IsRequired();
                entity.Property(e => e.LastUpdated).HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP").IsRequired();

                entity.HasIndex(e => e.UserId).HasDatabaseName("ix_chicks_user_id");
                entity.HasIndex(e => e.healthstate).HasDatabaseName("ix_chicks_health_state");
                entity.HasIndex(e => e.LastUpdated).HasDatabaseName("ix_chicks_last_updated");
                entity.HasIndex(e => new { e.UserId, e.healthstate }).HasDatabaseName("ix_chicks_user_health");
                
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== Configuration SensorData ==========
            modelBuilder.Entity<Sensordatum>(entity =>
            {
                entity.ToTable("sensordata", "public");
                entity.HasKey(e => e.id);
                
                entity.Property(e => e.id).HasColumnName("id");
                entity.Property(e => e.userid).HasColumnName("userid").HasMaxLength(450);
                entity.Property(e => e.timestamp).HasColumnName("timestamp").IsRequired();
                entity.Property(e => e.temperature).HasColumnName("temperature");
                entity.Property(e => e.humidity).HasColumnName("humidity");
                entity.Property(e => e.dust).HasColumnName("dust");
                entity.Property(e => e.chickcount).HasColumnName("chickcount");
                entity.Property(e => e.topic).HasMaxLength(500);
                entity.Property(e => e.payload).HasMaxLength(2000).IsRequired(false);

                entity.HasIndex(e => e.userid).HasDatabaseName("IX_SensorData_UserId");
                entity.HasIndex(e => e.timestamp).HasDatabaseName("IX_SensorData_Timestamp");
                entity.HasIndex(e => new { e.userid, e.timestamp }).HasDatabaseName("IX_SensorData_UserId_Timestamp");
                
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.userid)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== Configuration Notifications ==========
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("notifications");
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.Severity).HasMaxLength(50);
                
                entity.HasIndex(e => e.UserId).HasDatabaseName("IX_Notifications_UserId");
                entity.HasIndex(e => e.Timestamp).HasDatabaseName("IX_Notifications_Timestamp");
                entity.HasIndex(e => new { e.UserId, e.IsRead }).HasDatabaseName("IX_Notifications_UserId_IsRead");
                entity.HasIndex(e => new { e.UserId, e.Category }).HasDatabaseName("IX_Notifications_UserId_Category");
                
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
            // ========== Configuration NotificationSettings ==========
            modelBuilder.Entity<NotificationSettings>(entity =>
            {
                entity.ToTable("notificationSettings");
                entity.HasKey(e => e.UserId);
                
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.NotificationsEnabled).IsRequired();
                entity.Property(e => e.CheckInterval).IsRequired();
                
                entity.HasIndex(e => e.UserId)
                    .IsUnique()
                    .HasDatabaseName("IX_NotificationSettings_UserId");
                
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== Configuration NotificationStats ==========
            modelBuilder.Entity<NotificationStats>(entity =>
            {
                entity.HasNoKey();
                entity.Property(ns => ns.ByCategoryCount).HasColumnType("jsonb");
                entity.Property(ns => ns.BySeverityCount).HasColumnType("jsonb");
            });

            // ========== Configuration VideoFrames ==========
            modelBuilder.Entity<VideoFrame>(entity =>
            {
                entity.ToTable("VideoFrames");
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("Npgsql:ValueGenerationStrategy", 
                        Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
                
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(450);
                entity.Property(e => e.Timestamp).IsRequired().HasDefaultValueSql("NOW()");
                entity.Property(e => e.FrameData).IsRequired();
                entity.Property(e => e.Quality).HasDefaultValue("medium");
                entity.Property(e => e.Size).HasDefaultValue(0);
                entity.Property(e => e.CompressionRatio).HasDefaultValue(0.7);
                
                entity.HasIndex(e => e.UserId).HasDatabaseName("IX_VideoFrames_UserId");
                entity.HasIndex(e => new { e.UserId, e.Timestamp }).HasDatabaseName("IX_VideoFrames_UserId_Timestamp");
                
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== Configuration VideoStreamSessions ==========
            modelBuilder.Entity<VideoStreamSession>(entity =>
            {
                entity.ToTable("VideoStreamSessions");
                entity.HasKey(e => e.Id);
                
                entity.Property(e => e.Id)
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("Npgsql:ValueGenerationStrategy", 
                        Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            
                entity.HasIndex(e => new { e.UserId, e.IsActive })
                    .HasDatabaseName("IX_VideoStreamSessions_User_Active");

                entity.Property(e => e.StartedAt).HasDefaultValueSql("NOW()");
                entity.Property(e => e.LastFrameReceived).HasDefaultValueSql("NOW()");
                
                entity.HasOne<ApplicationUser>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            OnModelBuilding(modelBuilder);
        }

        // ==============================
        // Méthode partielle extensible
        // ==============================
        partial void OnModelBuilding(ModelBuilder modelBuilder);

        // ==============================
        // Seed TenantsAdmin
        // ==============================
        public async Task SeedTenantsAdmin()
        {
            var user = new ApplicationUser
            {
                UserName = "tenantsadmin",
                NormalizedUserName = "TENANTSADMIN",
                Email = "tenantsadmin",
                NormalizedEmail = "TENANTSADMIN",
                EmailConfirmed = true,
                LockoutEnabled = false,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            if (!this.Users.Any(u => u.UserName == user.UserName))
            {
                var password = new PasswordHasher<ApplicationUser>();
                var hashed = password.HashPassword(user, "tenantsadmin");
                user.PasswordHash = hashed;
                var userStore = new UserStore<ApplicationUser>(this);
                await userStore.CreateAsync(user);
            }

            await this.SaveChangesAsync();
        }
    }

    // ==============================
    // MultiTenancyUserStore
    // ==============================
    public class MultiTenancyUserStore : UserStore<ApplicationUser, ApplicationRole, ApplicationDbContext>
    {
        private readonly IHttpContextAccessor httpContextAccessor;
    
        public MultiTenancyUserStore(IHttpContextAccessor httpContextAccessor, ApplicationDbContext context, IdentityErrorDescriber describer = null) : base(context, describer)
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        private ApplicationTenant? GetTenant()
        {
            if (httpContextAccessor?.HttpContext == null)
            {
                return null;
            }
            var tenants = Context.Set<ApplicationTenant>().ToList();
            var host = httpContextAccessor.HttpContext.Request.Host.Value;
            return tenants.Where(t => t.Hosts.Split(',').Any(h => h.Contains(host!))).FirstOrDefault()!;
        }

        protected override async Task<ApplicationRole> FindRoleAsync(string normalizedRoleName, System.Threading.CancellationToken cancellationToken)
        {
            var tenant = GetTenant();
            ApplicationRole? role = null;

            if (tenant != null)
            {
                role = await Context.Set<ApplicationRole>()
                    .SingleOrDefaultAsync(r => r.NormalizedName == normalizedRoleName && r.TenantId == tenant.Id, cancellationToken);
            }
        
            if (role == null)
            {
                role = await Context.Set<ApplicationRole>()
                    .SingleOrDefaultAsync(r => r.NormalizedName == normalizedRoleName && r.TenantId == null, cancellationToken);
            }

            return role!;
        }

        public override async Task<ApplicationUser> FindByNameAsync(string normalizedName, CancellationToken cancellationToken = default)
        {
            if (normalizedName.ToLower() == "tenantsadmin")
            {
                return await base.FindByNameAsync(normalizedName, cancellationToken);
            }

            var tenant = GetTenant();
            ApplicationUser? user = null;

            if (tenant != null)
            {
                user = await Context.Set<ApplicationUser>()
                    .SingleOrDefaultAsync(r => r.NormalizedUserName == normalizedName && r.TenantId == tenant.Id, cancellationToken);
            }
        
            if (user == null)
            {
                user = await Context.Set<ApplicationUser>()
                    .SingleOrDefaultAsync(r => r.NormalizedUserName == normalizedName && r.TenantId == null, cancellationToken);
            }

            return user!;
        }

        public override async Task AddToRoleAsync(ApplicationUser user, string normalizedRoleName, CancellationToken cancellationToken = default)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }
        
            if (string.IsNullOrWhiteSpace(normalizedRoleName))
            {
                throw new ArgumentException("Role name cannot be null or empty", nameof(normalizedRoleName));
            }
        
            if (user.NormalizedUserName?.ToLower() == "tenantsadmin")
            {
                await base.AddToRoleAsync(user, normalizedRoleName, cancellationToken);
                return;
            }

            var tenant = GetTenant();
            ApplicationRole role = null;

            if (tenant != null)
            {
                role = await Context.Set<ApplicationRole>()
                    .SingleOrDefaultAsync(r => r.NormalizedName == normalizedRoleName && r.TenantId == tenant.Id, cancellationToken);
            }
        
            if (role == null)
            {
                role = await Context.Set<ApplicationRole>()
                    .SingleOrDefaultAsync(r => r.NormalizedName == normalizedRoleName && r.TenantId == null, cancellationToken);
            }
        
            if (role == null)
            {
                throw new InvalidOperationException($"Role '{normalizedRoleName}' not found for tenant '{tenant?.Name ?? "global"}'");
            }
        
            var existingUserRole = await Context.Set<IdentityUserRole<string>>()
                .AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, cancellationToken);
        
            if (!existingUserRole)
            {
                Context.Set<IdentityUserRole<string>>().Add(new IdentityUserRole<string>
                {
                    RoleId = role.Id,
                    UserId = user.Id
                });

                await Context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}