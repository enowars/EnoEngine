using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace EnoCore.Migrations
{
    public partial class m1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckerTasks",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    TaskType = table.Column<string>(nullable: true),
                    Address = table.Column<string>(nullable: true),
                    ServiceId = table.Column<long>(nullable: false),
                    ServiceName = table.Column<string>(nullable: true),
                    TeamId = table.Column<long>(nullable: false),
                    TeamName = table.Column<string>(nullable: true),
                    RelatedRoundId = table.Column<long>(nullable: false),
                    CurrentRoundId = table.Column<long>(nullable: false),
                    Payload = table.Column<string>(nullable: true),
                    StartTime = table.Column<DateTime>(nullable: false),
                    MaxRunningTime = table.Column<int>(nullable: false),
                    TaskIndex = table.Column<long>(nullable: false),
                    CheckerResult = table.Column<int>(nullable: false),
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
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
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
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    Name = table.Column<string>(nullable: true),
                    FlagsPerRound = table.Column<int>(nullable: false),
                    ServiceStatsId = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    TeamId = table.Column<long>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    VulnboxAddress = table.Column<string>(nullable: true),
                    GatewayAddress = table.Column<string>(nullable: true),
                    TotalPoints = table.Column<double>(nullable: false),
                    AttackPoints = table.Column<double>(nullable: false),
                    LostDefensePoints = table.Column<double>(nullable: false),
                    ServiceLevelAgreementPoints = table.Column<double>(nullable: false),
                    ServiceStatsId = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Logs",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    Message = table.Column<string>(nullable: true),
                    Timestamp = table.Column<DateTime>(nullable: false),
                    Severity = table.Column<int>(nullable: false),
                    RelatedTaskId = table.Column<long>(nullable: false),
                    Origin = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Logs_CheckerTasks_RelatedTaskId",
                        column: x => x.RelatedTaskId,
                        principalTable: "CheckerTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Flags",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    StringRepresentation = table.Column<string>(nullable: true),
                    OwnerId = table.Column<long>(nullable: false),
                    ServiceId = table.Column<long>(nullable: false),
                    RoundOffset = table.Column<int>(nullable: false),
                    GameRoundId = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Flags_Rounds_GameRoundId",
                        column: x => x.GameRoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Flags_Teams_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Flags_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoundTeamServiceStates",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    FlagsLost = table.Column<long>(nullable: false),
                    FlagsCaptured = table.Column<long>(nullable: false),
                    Status = table.Column<int>(nullable: false),
                    TeamId = table.Column<long>(nullable: false),
                    ServiceId = table.Column<long>(nullable: false),
                    GameRoundId = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundTeamServiceStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoundTeamServiceStates_Rounds_GameRoundId",
                        column: x => x.GameRoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoundTeamServiceStates_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoundTeamServiceStates_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceStats",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    TeamId = table.Column<long>(nullable: false),
                    ServiceId = table.Column<long>(nullable: false),
                    AttackPoints = table.Column<double>(nullable: false),
                    LostDefensePoints = table.Column<double>(nullable: false),
                    ServiceLevelAgreementPoints = table.Column<double>(nullable: false),
                    Status = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceStats_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceStats_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubmittedFlags",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    FlagId = table.Column<long>(nullable: false),
                    AttackerTeamId = table.Column<long>(nullable: false),
                    RoundId = table.Column<long>(nullable: false),
                    SubmissionsCount = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubmittedFlags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubmittedFlags_Teams_AttackerTeamId",
                        column: x => x.AttackerTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubmittedFlags_Flags_FlagId",
                        column: x => x.FlagId,
                        principalTable: "Flags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubmittedFlags_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckerTasks_CheckerTaskLaunchStatus",
                table: "CheckerTasks",
                column: "CheckerTaskLaunchStatus");

            migrationBuilder.CreateIndex(
                name: "IX_CheckerTasks_Id",
                table: "CheckerTasks",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_GameRoundId",
                table: "Flags",
                column: "GameRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_Id",
                table: "Flags",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_OwnerId",
                table: "Flags",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_ServiceId",
                table: "Flags",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_Id",
                table: "Logs",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_RelatedTaskId",
                table: "Logs",
                column: "RelatedTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_Timestamp",
                table: "Logs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_Id",
                table: "Rounds",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_RoundTeamServiceStates_GameRoundId",
                table: "RoundTeamServiceStates",
                column: "GameRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundTeamServiceStates_Id",
                table: "RoundTeamServiceStates",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_RoundTeamServiceStates_ServiceId",
                table: "RoundTeamServiceStates",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundTeamServiceStates_TeamId",
                table: "RoundTeamServiceStates",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_Id",
                table: "Services",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStats_Id",
                table: "ServiceStats",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStats_ServiceId",
                table: "ServiceStats",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStats_TeamId",
                table: "ServiceStats",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmittedFlags_FlagId",
                table: "SubmittedFlags",
                column: "FlagId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmittedFlags_Id",
                table: "SubmittedFlags",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_SubmittedFlags_RoundId",
                table: "SubmittedFlags",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmittedFlags_AttackerTeamId_FlagId",
                table: "SubmittedFlags",
                columns: new[] { "AttackerTeamId", "FlagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Id",
                table: "Teams",
                column: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Logs");

            migrationBuilder.DropTable(
                name: "RoundTeamServiceStates");

            migrationBuilder.DropTable(
                name: "ServiceStats");

            migrationBuilder.DropTable(
                name: "SubmittedFlags");

            migrationBuilder.DropTable(
                name: "CheckerTasks");

            migrationBuilder.DropTable(
                name: "Flags");

            migrationBuilder.DropTable(
                name: "Rounds");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Services");
        }
    }
}
