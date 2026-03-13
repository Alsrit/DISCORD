using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mod_analysis_snapshots",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ModVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalModReference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SourceLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PayloadSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileCount = table.Column<int>(type: "integer", nullable: false),
                    SegmentCount = table.Column<int>(type: "integer", nullable: false),
                    CharacterCount = table.Column<int>(type: "integer", nullable: false),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    FilesJson = table.Column<string>(type: "jsonb", nullable: false),
                    WarningsJson = table.Column<string>(type: "jsonb", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mod_analysis_snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_mod_analysis_snapshots_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "platform",
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mod_analysis_snapshots_licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "platform",
                        principalTable: "licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "translation_glossaries",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SourceLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    TermsJson = table.Column<string>(type: "jsonb", nullable: false),
                    SkipTermsJson = table.Column<string>(type: "jsonb", nullable: false),
                    FrozenTermsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_glossaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_translation_glossaries_licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "platform",
                        principalTable: "licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "translation_provider_settings",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    LanguagesEndpoint = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FolderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SecretReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    MaxBatchCharacters = table.Column<int>(type: "integer", nullable: false),
                    FailureThreshold = table.Column<int>(type: "integer", nullable: false),
                    CircuitBreakSeconds = table.Column<int>(type: "integer", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false),
                    LastHealthCheckUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastKnownStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_provider_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "translation_quotas",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaxFilesPerJob = table.Column<int>(type: "integer", nullable: false),
                    MaxSegmentsPerJob = table.Column<int>(type: "integer", nullable: false),
                    MaxCharactersPerJob = table.Column<int>(type: "integer", nullable: false),
                    MaxCharactersPerDay = table.Column<int>(type: "integer", nullable: false),
                    MaxConcurrentJobs = table.Column<int>(type: "integer", nullable: false),
                    MaxJobsPerHour = table.Column<int>(type: "integer", nullable: false),
                    MaxAnalysisPerHour = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_quotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_translation_quotas_licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "platform",
                        principalTable: "licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "translation_usage",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsageDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReservedCharacters = table.Column<int>(type: "integer", nullable: false),
                    ConsumedCharacters = table.Column<int>(type: "integer", nullable: false),
                    JobsCreated = table.Column<int>(type: "integer", nullable: false),
                    JobsCompleted = table.Column<int>(type: "integer", nullable: false),
                    JobsFailed = table.Column<int>(type: "integer", nullable: false),
                    JobsCancelled = table.Column<int>(type: "integer", nullable: false),
                    AnalysisRequests = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_usage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_translation_usage_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "platform",
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_translation_usage_licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "platform",
                        principalTable: "licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "translation_jobs",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnalysisSnapshotId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    QueueName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OriginalModReference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RequestedSubmodName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourceLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    TotalFiles = table.Column<int>(type: "integer", nullable: false),
                    TotalSegments = table.Column<int>(type: "integer", nullable: false),
                    TotalCharacters = table.Column<int>(type: "integer", nullable: false),
                    ProcessedSegments = table.Column<int>(type: "integer", nullable: false),
                    ProcessedCharacters = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetryCount = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    RequestedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelRequestedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastHeartbeatUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ResultStoragePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_translation_jobs_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "platform",
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_translation_jobs_licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalSchema: "platform",
                        principalTable: "licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_translation_jobs_mod_analysis_snapshots_AnalysisSnapshotId",
                        column: x => x.AnalysisSnapshotId,
                        principalSchema: "platform",
                        principalTable: "mod_analysis_snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "submod_build_artifacts",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TranslationJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactType = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ManifestJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submod_build_artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submod_build_artifacts_translation_jobs_TranslationJobId",
                        column: x => x.TranslationJobId,
                        principalSchema: "platform",
                        principalTable: "translation_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "translation_audit_events",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TranslationJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorType = table.Column<int>(type: "integer", nullable: false),
                    ActorIdentifier = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_audit_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_translation_audit_events_translation_jobs_TranslationJobId",
                        column: x => x.TranslationJobId,
                        principalSchema: "platform",
                        principalTable: "translation_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "translation_files",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TranslationJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelativePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SanitizedPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    HeaderKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetLanguage = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OriginalSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OriginalSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SegmentCount = table.Column<int>(type: "integer", nullable: false),
                    CharacterCount = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    WarningJson = table.Column<string>(type: "jsonb", nullable: false),
                    OriginalContent = table.Column<string>(type: "text", nullable: false),
                    TranslatedContent = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_translation_files_translation_jobs_TranslationJobId",
                        column: x => x.TranslationJobId,
                        principalSchema: "platform",
                        principalTable: "translation_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "translation_job_items",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TranslationJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchNumber = table.Column<int>(type: "integer", nullable: false),
                    ProviderCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    SegmentIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CharacterCount = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    RequestPayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ResponsePayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    StartedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_job_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_translation_job_items_translation_jobs_TranslationJobId",
                        column: x => x.TranslationJobId,
                        principalSchema: "platform",
                        principalTable: "translation_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "translation_segments",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TranslationFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    LocalizationKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Prefix = table.Column<string>(type: "text", nullable: false),
                    Suffix = table.Column<string>(type: "text", nullable: false),
                    SourceText = table.Column<string>(type: "text", nullable: false),
                    ProtectedSourceText = table.Column<string>(type: "text", nullable: false),
                    ProtectedTranslationText = table.Column<string>(type: "text", nullable: false),
                    FinalText = table.Column<string>(type: "text", nullable: false),
                    PlaceholderMapJson = table.Column<string>(type: "jsonb", nullable: false),
                    CharacterCount = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ValidationMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_segments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_translation_segments_translation_files_TranslationFileId",
                        column: x => x.TranslationFileId,
                        principalSchema: "platform",
                        principalTable: "translation_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mod_analysis_snapshots_DeviceId",
                schema: "platform",
                table: "mod_analysis_snapshots",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_mod_analysis_snapshots_LicenseId_DeviceId_PayloadSha256",
                schema: "platform",
                table: "mod_analysis_snapshots",
                columns: new[] { "LicenseId", "DeviceId", "PayloadSha256" });

            migrationBuilder.CreateIndex(
                name: "IX_submod_build_artifacts_TranslationJobId_IsPrimary",
                schema: "platform",
                table: "submod_build_artifacts",
                columns: new[] { "TranslationJobId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_translation_audit_events_TranslationJobId_OccurredUtc",
                schema: "platform",
                table: "translation_audit_events",
                columns: new[] { "TranslationJobId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_translation_files_TranslationJobId_SanitizedPath",
                schema: "platform",
                table: "translation_files",
                columns: new[] { "TranslationJobId", "SanitizedPath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_translation_glossaries_LicenseId",
                schema: "platform",
                table: "translation_glossaries",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_translation_glossaries_Scope_LicenseId_SourceLanguage_Targe~",
                schema: "platform",
                table: "translation_glossaries",
                columns: new[] { "Scope", "LicenseId", "SourceLanguage", "TargetLanguage", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_translation_job_items_TranslationJobId_BatchNumber",
                schema: "platform",
                table: "translation_job_items",
                columns: new[] { "TranslationJobId", "BatchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_translation_jobs_AnalysisSnapshotId",
                schema: "platform",
                table: "translation_jobs",
                column: "AnalysisSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_translation_jobs_DeviceId",
                schema: "platform",
                table: "translation_jobs",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_translation_jobs_LicenseId_DeviceId_IdempotencyKey",
                schema: "platform",
                table: "translation_jobs",
                columns: new[] { "LicenseId", "DeviceId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_translation_jobs_State_RequestedUtc",
                schema: "platform",
                table: "translation_jobs",
                columns: new[] { "State", "RequestedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_translation_provider_settings_ProviderCode",
                schema: "platform",
                table: "translation_provider_settings",
                column: "ProviderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_translation_quotas_LicenseId",
                schema: "platform",
                table: "translation_quotas",
                column: "LicenseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_translation_segments_TranslationFileId_Sequence",
                schema: "platform",
                table: "translation_segments",
                columns: new[] { "TranslationFileId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_translation_usage_DeviceId",
                schema: "platform",
                table: "translation_usage",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_translation_usage_LicenseId_DeviceId_UsageDate",
                schema: "platform",
                table: "translation_usage",
                columns: new[] { "LicenseId", "DeviceId", "UsageDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submod_build_artifacts",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "translation_audit_events",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "translation_glossaries",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "translation_job_items",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "translation_provider_settings",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "translation_quotas",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "translation_segments",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "translation_usage",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "translation_files",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "translation_jobs",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "mod_analysis_snapshots",
                schema: "platform");
        }
    }
}
