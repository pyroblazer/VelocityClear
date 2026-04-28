using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FinancialPlatform.ComplianceService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOjkComplianceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AmlAlerts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RuleTriggered = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmlAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApprovalType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResourceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResourceType = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditRetentionPolicies",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetentionYears = table.Column<int>(type: "int", nullable: false),
                    Regulation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRetentionPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplaintNotes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ComplaintId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AuthorId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplaintNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplaintTickets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SlaDeadline = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SlaBreach = table.Column<bool>(type: "bit", nullable: false),
                    RelatedTransactionId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplaintTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConsentType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LegalBasis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataClassifications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    MaskingRule = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetentionRequired = table.Column<bool>(type: "bit", nullable: false),
                    RetentionYears = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataClassifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataResidencyChecks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Region = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCompliant = table.Column<bool>(type: "bit", nullable: false),
                    NonComplianceReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Regulation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataResidencyChecks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrpBcpStatuses",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RtoMinutes = table.Column<int>(type: "int", nullable: false),
                    RpoMinutes = table.Column<int>(type: "int", nullable: false),
                    LastTestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextTestScheduled = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTestPassed = table.Column<bool>(type: "bit", nullable: false),
                    TestNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrpBcpStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KycProfiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LivenessChecked = table.Column<bool>(type: "bit", nullable: false),
                    LivenessConfidence = table.Column<double>(type: "float", nullable: false),
                    WatchlistScreened = table.Column<bool>(type: "bit", nullable: false),
                    WatchlistHit = table.Column<bool>(type: "bit", nullable: false),
                    WatchlistMatchedCategory = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KycProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OjkReports",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReportType = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedTo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetentionYears = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OjkReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleAssignments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ApprovalRequestId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityIncidents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RunbookReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AffectedSystems = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContainmentActions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RootCause = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ContainedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityIncidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignedDocuments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DocumentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SignerId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Signature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VendorReferenceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SigningMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignedDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SuspiciousActivityReports",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TransactionId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Narrative = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OjkReferenceNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FiledBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FiledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuspiciousAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SuspiciousBasis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuspiciousActivityReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorAuditEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ServiceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UptimePercent = table.Column<double>(type: "float", nullable: false),
                    SlaTargetPercent = table.Column<double>(type: "float", nullable: false),
                    SlaMet = table.Column<bool>(type: "bit", nullable: false),
                    IncidentCount = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WatchlistEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Aliases = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Nationality = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistEntries", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AuditRetentionPolicies",
                columns: new[] { "Id", "CreatedAt", "EventType", "IsActive", "Regulation", "RetentionYears" },
                values: new object[,]
                {
                    { "rp-001", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "TransactionCreatedEvent", true, "POJK No.6/POJK.07/2022", 10 },
                    { "rp-002", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "PaymentAuthorizedEvent", true, "POJK No.6/POJK.07/2022", 10 },
                    { "rp-003", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "KycStatusChangedEvent", true, "POJK No.12/POJK.01/2017", 5 }
                });

            migrationBuilder.InsertData(
                table: "DataClassifications",
                columns: new[] { "Id", "CreatedAt", "EntityName", "FieldName", "Level", "MaskingRule", "RetentionRequired", "RetentionYears" },
                values: new object[,]
                {
                    { "dc-001", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "User", "IdNumber", 5, "Partial", true, 10 },
                    { "dc-002", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "User", "FullName", 4, "Partial", true, 10 },
                    { "dc-003", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Transaction", "Amount", 2, "Full", true, 7 },
                    { "dc-004", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "User", "Email", 4, "Email", true, 10 },
                    { "dc-005", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "User", "PhoneNumber", 4, "Phone", true, 10 }
                });

            migrationBuilder.InsertData(
                table: "WatchlistEntries",
                columns: new[] { "Id", "AddedAt", "Aliases", "Category", "FullName", "IdNumber", "IsActive", "Nationality", "RemovedAt", "Source" },
                values: new object[,]
                {
                    { "wl-001", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 1, "Test Sanctioned Person", null, true, null, null, "OFAC-Seed" },
                    { "wl-002", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, 0, "Test PEP Individual", null, true, null, null, "Local-Seed" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AmlAlerts_Status",
                table: "AmlAlerts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AmlAlerts_TransactionId",
                table: "AmlAlerts",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_RequestedBy",
                table: "ApprovalRequests",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_Status",
                table: "ApprovalRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintNotes_ComplaintId",
                table: "ComplaintNotes",
                column: "ComplaintId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintTickets_Status",
                table: "ComplaintTickets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ComplaintTickets_UserId",
                table: "ComplaintTickets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_UserId_ConsentType",
                table: "ConsentRecords",
                columns: new[] { "UserId", "ConsentType" });

            migrationBuilder.CreateIndex(
                name: "IX_DataClassifications_EntityName_FieldName",
                table: "DataClassifications",
                columns: new[] { "EntityName", "FieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrpBcpStatuses_PlanName",
                table: "DrpBcpStatuses",
                column: "PlanName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KycProfiles_Status",
                table: "KycProfiles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_KycProfiles_UserId",
                table: "KycProfiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OjkReports_ReportType",
                table: "OjkReports",
                column: "ReportType");

            migrationBuilder.CreateIndex(
                name: "IX_OjkReports_Status",
                table: "OjkReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_UserId_IsActive",
                table: "RoleAssignments",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityIncidents_Severity",
                table: "SecurityIncidents",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityIncidents_Status",
                table: "SecurityIncidents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SignedDocuments_DocumentId",
                table: "SignedDocuments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SuspiciousActivityReports_Status",
                table: "SuspiciousActivityReports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SuspiciousActivityReports_TransactionId",
                table: "SuspiciousActivityReports",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistEntries_Category",
                table: "WatchlistEntries",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistEntries_IsActive",
                table: "WatchlistEntries",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AmlAlerts");

            migrationBuilder.DropTable(
                name: "ApprovalRequests");

            migrationBuilder.DropTable(
                name: "AuditRetentionPolicies");

            migrationBuilder.DropTable(
                name: "ComplaintNotes");

            migrationBuilder.DropTable(
                name: "ComplaintTickets");

            migrationBuilder.DropTable(
                name: "ConsentRecords");

            migrationBuilder.DropTable(
                name: "DataClassifications");

            migrationBuilder.DropTable(
                name: "DataResidencyChecks");

            migrationBuilder.DropTable(
                name: "DrpBcpStatuses");

            migrationBuilder.DropTable(
                name: "KycProfiles");

            migrationBuilder.DropTable(
                name: "OjkReports");

            migrationBuilder.DropTable(
                name: "RoleAssignments");

            migrationBuilder.DropTable(
                name: "SecurityIncidents");

            migrationBuilder.DropTable(
                name: "SignedDocuments");

            migrationBuilder.DropTable(
                name: "SuspiciousActivityReports");

            migrationBuilder.DropTable(
                name: "VendorAuditEntries");

            migrationBuilder.DropTable(
                name: "WatchlistEntries");
        }
    }
}
