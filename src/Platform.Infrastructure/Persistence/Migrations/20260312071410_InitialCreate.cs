using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.CreateTable(
                name: "admin_users",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastLoginUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "licenses",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseKeyHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LicenseKeyMasked = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LookupPrefix = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    MaxDevices = table.Column<int>(type: "integer", nullable: false),
                    OfflineGracePeriodHours = table.Column<int>(type: "integer", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    UpdateChannel = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_licenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "security_incidents",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_incidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_events",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ClientVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "update_channels",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_update_channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceFingerprintHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    InstallationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MachineName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OperatingSystem = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LastKnownIp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastKnownUserAgent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CurrentClientVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    FirstSeenUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_devices_licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "platform",
                        principalTable: "licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_releases",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdateChannelDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    MinimumSupportedVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PublishedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_releases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_application_releases_update_channels_UpdateChannelDefinitio~",
                        column: x => x.UpdateChannelDefinitionId,
                        principalSchema: "platform",
                        principalTable: "update_channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_sessions",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessTokenHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RefreshTokenHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AccessTokenExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RefreshTokenExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastKnownIp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CurrentClientVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_sessions_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "platform",
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_client_sessions_licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "platform",
                        principalTable: "licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "license_activations",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RequestedInstallationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestedDeviceFingerprintHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RequestedClientVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_activations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_license_activations_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "platform",
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_license_activations_licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "platform",
                        principalTable: "licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "license_audit_events",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorType = table.Column<int>(type: "integer", nullable: false),
                    ActorIdentifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_license_audit_events_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "platform",
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_license_audit_events_licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "platform",
                        principalTable: "licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "release_artifacts",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationReleaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SignatureBase64 = table.Column<string>(type: "text", nullable: false),
                    SignaturePayload = table.Column<string>(type: "text", nullable: false),
                    SignatureAlgorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_release_artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_release_artifacts_application_releases_ApplicationReleaseId",
                        column: x => x.ApplicationReleaseId,
                        principalSchema: "platform",
                        principalTable: "application_releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_users_Email",
                schema: "platform",
                table: "admin_users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_users_UserName",
                schema: "platform",
                table: "admin_users",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_application_releases_UpdateChannelDefinitionId_Version",
                schema: "platform",
                table: "application_releases",
                columns: new[] { "UpdateChannelDefinitionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_sessions_AccessTokenHash",
                schema: "platform",
                table: "client_sessions",
                column: "AccessTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_sessions_DeviceId",
                schema: "platform",
                table: "client_sessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_client_sessions_LicenseId",
                schema: "platform",
                table: "client_sessions",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_client_sessions_RefreshTokenHash",
                schema: "platform",
                table: "client_sessions",
                column: "RefreshTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_LicenseId_DeviceFingerprintHash_InstallationId",
                schema: "platform",
                table: "devices",
                columns: new[] { "LicenseId", "DeviceFingerprintHash", "InstallationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_license_activations_DeviceId",
                schema: "platform",
                table: "license_activations",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_license_activations_LicenseId",
                schema: "platform",
                table: "license_activations",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_license_audit_events_CreatedUtc",
                schema: "platform",
                table: "license_audit_events",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_license_audit_events_DeviceId",
                schema: "platform",
                table: "license_audit_events",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_license_audit_events_LicenseId",
                schema: "platform",
                table: "license_audit_events",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_licenses_LicenseKeyHash",
                schema: "platform",
                table: "licenses",
                column: "LicenseKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_licenses_LookupPrefix",
                schema: "platform",
                table: "licenses",
                column: "LookupPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_release_artifacts_ApplicationReleaseId",
                schema: "platform",
                table: "release_artifacts",
                column: "ApplicationReleaseId");

            migrationBuilder.CreateIndex(
                name: "IX_security_incidents_OccurredUtc",
                schema: "platform",
                table: "security_incidents",
                column: "OccurredUtc");

            migrationBuilder.CreateIndex(
                name: "IX_telemetry_events_ReceivedUtc",
                schema: "platform",
                table: "telemetry_events",
                column: "ReceivedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_update_channels_Code",
                schema: "platform",
                table: "update_channels",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_users",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "client_sessions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "license_activations",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "license_audit_events",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "release_artifacts",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "security_incidents",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "telemetry_events",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "devices",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "application_releases",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "licenses",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "update_channels",
                schema: "platform");
        }
    }
}
