using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartNest.Server.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "AspNetTenants",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Hosts = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetTenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "notificationStats",
                columns: table => new
                {
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
                    UnreadCount = table.Column<int>(type: "integer", nullable: false),
                    CriticalCount = table.Column<int>(type: "integer", nullable: false),
                    TodayCount = table.Column<int>(type: "integer", nullable: false),
                    ByCategoryCount = table.Column<Dictionary<string, int>>(type: "jsonb", nullable: false),
                    BySeverityCount = table.Column<Dictionary<string, int>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoles_AspNetTenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "AspNetTenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_AspNetTenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "AspNetTenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chicks",
                columns: table => new
                {
                    ChickId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false),
                    healthstate = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Healthy"),
                    Age = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Weight = table.Column<double>(type: "double precision", nullable: false, defaultValue: 40.0),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chicks", x => x.ChickId);
                    table.ForeignKey(
                        name: "FK_chicks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    DeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeviceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    StatusMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_devices_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriggerValue = table.Column<double>(type: "double precision", nullable: true),
                    ThresholdValue = table.Column<double>(type: "double precision", nullable: true),
                    ActionTaken = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notificationSettings",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EmailNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    PushNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    SmsNotifications = table.Column<bool>(type: "boolean", nullable: false),
                    TemperatureThreshold = table.Column<double>(type: "double precision", nullable: false),
                    HumidityThreshold = table.Column<int>(type: "integer", nullable: false),
                    DustThreshold = table.Column<int>(type: "integer", nullable: false),
                    WaterLevelThreshold = table.Column<int>(type: "integer", nullable: false),
                    FoodLevelThreshold = table.Column<int>(type: "integer", nullable: false),
                    CheckInterval = table.Column<int>(type: "integer", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notificationSettings", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_notificationSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sensordata",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    userid = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    topic = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    payload = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    temperature = table.Column<double>(type: "double precision", nullable: false),
                    humidity = table.Column<double>(type: "double precision", nullable: false),
                    dust = table.Column<double>(type: "double precision", nullable: false),
                    chickcount = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensordata", x => x.id);
                    table.ForeignKey(
                        name: "FK_sensordata_AspNetUsers_userid",
                        column: x => x.userid,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoFrames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    FrameData = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Quality = table.Column<string>(type: "text", nullable: false, defaultValue: "medium"),
                    Size = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CompressionRatio = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.69999999999999996)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoFrames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoFrames_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoStreamSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    StoppedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Quality = table.Column<string>(type: "text", nullable: false),
                    TargetFPS = table.Column<int>(type: "integer", nullable: false),
                    CompressionQuality = table.Column<double>(type: "double precision", nullable: false),
                    LastFrameReceived = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoStreamSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoStreamSessions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoles_TenantId",
                table: "AspNetRoles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TenantId",
                table: "AspNetUsers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chicks_health_state",
                table: "chicks",
                column: "healthstate");

            migrationBuilder.CreateIndex(
                name: "ix_chicks_last_updated",
                table: "chicks",
                column: "LastUpdated");

            migrationBuilder.CreateIndex(
                name: "ix_chicks_user_health",
                table: "chicks",
                columns: new[] { "UserId", "healthstate" });

            migrationBuilder.CreateIndex(
                name: "ix_chicks_user_id",
                table: "chicks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId",
                table: "devices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_UserId_DeviceId",
                table: "devices",
                columns: new[] { "UserId", "DeviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Timestamp",
                table: "notifications",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_Category",
                table: "notifications",
                columns: new[] { "UserId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead",
                table: "notifications",
                columns: new[] { "UserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSettings_UserId",
                table: "notificationSettings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SensorData_Timestamp",
                schema: "public",
                table: "sensordata",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_SensorData_UserId",
                schema: "public",
                table: "sensordata",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_SensorData_UserId_Timestamp",
                schema: "public",
                table: "sensordata",
                columns: new[] { "userid", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoFrames_UserId",
                table: "VideoFrames",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoFrames_UserId_Timestamp",
                table: "VideoFrames",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoStreamSessions_User_Active",
                table: "VideoStreamSessions",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "chicks");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "notificationSettings");

            migrationBuilder.DropTable(
                name: "notificationStats");

            migrationBuilder.DropTable(
                name: "sensordata",
                schema: "public");

            migrationBuilder.DropTable(
                name: "VideoFrames");

            migrationBuilder.DropTable(
                name: "VideoStreamSessions");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "AspNetTenants");
        }
    }
}
