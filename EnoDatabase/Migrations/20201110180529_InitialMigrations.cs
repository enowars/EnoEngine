using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace EnoDatabase.Migrations
{
    public partial class InitialMigrations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckerTasks",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CheckerUrl = table.Column<string>(nullable: false),
                    Method = table.Column<int>(nullable: false),
                    Address = table.Column<string>(nullable: false),
                    ServiceId = table.Column<long>(nullable: false),
                    ServiceName = table.Column<string>(nullable: false),
                    TeamId = table.Column<long>(nullable: false),
                    TeamName = table.Column<string>(nullable: false),
                    RelatedRoundId = table.Column<long>(nullable: false),
                    CurrentRoundId = table.Column<long>(nullable: false),
                    Payload = table.Column<string>(nullable: true),
                    StartTime = table.Column<DateTime>(nullable: false),
                    MaxRunningTime = table.Column<int>(nullable: false),
                    RoundLength = table.Column<long>(nullable: false),
                    TaskIndex = table.Column<long>(nullable: false),
                    CheckerResult = table.Column<int>(nullable: false),
                    ErrorMessage = table.Column<string>(nullable: true),
                    CheckerTaskLaunchStatus = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckerTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rounds",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Begin = table.Column<DateTime>(nullable: false),
                    Quarter2 = table.Column<DateTime>(nullable: false),
                    Quarter3 = table.Column<DateTime>(nullable: false),
                    Quarter4 = table.Column<DateTime>(nullable: false),
                    End = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rounds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoundTeamServiceStatus",
                columns: table => new
                {
                    TeamId = table.Column<long>(nullable: false),
                    ServiceId = table.Column<long>(nullable: false),
                    GameRoundId = table.Column<long>(nullable: false),
                    Status = table.Column<int>(nullable: false),
                    ErrorMessage = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundTeamServiceStatus", x => new { x.ServiceId, x.TeamId, x.GameRoundId });
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(nullable: false),
                    FlagsPerRound = table.Column<long>(nullable: false),
                    NoisesPerRound = table.Column<long>(nullable: false),
                    HavocsPerRound = table.Column<long>(nullable: false),
                    FlagStores = table.Column<long>(nullable: false),
                    Active = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubmittedFlags",
                columns: table => new
                {
                    FlagServiceId = table.Column<long>(nullable: false),
                    FlagOwnerId = table.Column<long>(nullable: false),
                    FlagRoundId = table.Column<long>(nullable: false),
                    FlagRoundOffset = table.Column<int>(nullable: false),
                    AttackerTeamId = table.Column<long>(nullable: false),
                    RoundId = table.Column<long>(nullable: false),
                    SubmissionsCount = table.Column<long>(nullable: false),
                    Timestamp = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmittedFlags", x => new { x.FlagServiceId, x.FlagRoundId, x.FlagOwnerId, x.FlagRoundOffset, x.AttackerTeamId });
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(nullable: false),
                    TeamSubnet = table.Column<string>(nullable: false),
                    TotalPoints = table.Column<double>(nullable: false),
                    AttackPoints = table.Column<double>(nullable: false),
                    DefensePoints = table.Column<double>(nullable: false),
                    ServiceLevelAgreementPoints = table.Column<double>(nullable: false),
                    Address = table.Column<string>(nullable: true),
                    ServiceStatsId = table.Column<long>(nullable: false),
                    Active = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamServicePointsSnapshot",
                columns: table => new
                {
                    TeamId = table.Column<long>(nullable: false),
                    ServiceId = table.Column<long>(nullable: false),
                    RoundId = table.Column<long>(nullable: false),
                    AttackPoints = table.Column<double>(nullable: false),
                    LostDefensePoints = table.Column<double>(nullable: false),
                    ServiceLevelAgreementPoints = table.Column<double>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamServicePointsSnapshot", x => new { x.ServiceId, x.RoundId, x.TeamId });
                });

            migrationBuilder.CreateTable(
                name: "TeamServicePoints",
                columns: table => new
                {
                    TeamId = table.Column<long>(nullable: false),
                    ServiceId = table.Column<long>(nullable: false),
                    AttackPoints = table.Column<double>(nullable: false),
                    DefensePoints = table.Column<double>(nullable: false),
                    ServiceLevelAgreementPoints = table.Column<double>(nullable: false),
                    Status = table.Column<int>(nullable: false),
                    ErrorMessage = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamServicePoints", x => new { x.TeamId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_TeamServicePoints_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckerTasks_CheckerTaskLaunchStatus_StartTime",
                table: "CheckerTasks",
                columns: new[] { "CheckerTaskLaunchStatus", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_CheckerTasks_CurrentRoundId_RelatedRoundId_CheckerResult",
                table: "CheckerTasks",
                columns: new[] { "CurrentRoundId", "RelatedRoundId", "CheckerResult" });

            migrationBuilder.CreateIndex(
                name: "IX_SubmittedFlags_FlagServiceId_FlagRoundOffset_Timestamp",
                table: "SubmittedFlags",
                columns: new[] { "FlagServiceId", "FlagRoundOffset", "Timestamp" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckerTasks");

            migrationBuilder.DropTable(
                name: "Rounds");

            migrationBuilder.DropTable(
                name: "RoundTeamServiceStatus");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "SubmittedFlags");

            migrationBuilder.DropTable(
                name: "TeamServicePoints");

            migrationBuilder.DropTable(
                name: "TeamServicePointsSnapshot");

            migrationBuilder.DropTable(
                name: "Teams");
        }
    }
}
