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
                    Id = table.Column<long>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    FlagsPerRound = table.Column<int>(nullable: false),
                    NoisesPerRound = table.Column<int>(nullable: false),
                    HavocsPerRound = table.Column<int>(nullable: false),
                    ServiceStatsId = table.Column<long>(nullable: false),
                    Active = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    TeamSubnet = table.Column<string>(nullable: true),
                    TotalPoints = table.Column<double>(nullable: false),
                    AttackPoints = table.Column<double>(nullable: false),
                    LostDefensePoints = table.Column<double>(nullable: false),
                    ServiceLevelAgreementPoints = table.Column<double>(nullable: false),
                    ServiceStatsId = table.Column<long>(nullable: false),
                    Active = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CheckerTasks",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    CheckerUrl = table.Column<string>(nullable: true),
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
                    RoundLength = table.Column<long>(nullable: false),
                    TaskIndex = table.Column<long>(nullable: false),
                    CheckerResult = table.Column<int>(nullable: false),
                    CheckerTaskLaunchStatus = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckerTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckerTasks_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Havocs",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    OwnerId = table.Column<long>(nullable: false),
                    ServiceId = table.Column<long>(nullable: false),
                    GameRoundId = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Havocs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Havocs_Rounds_GameRoundId",
                        column: x => x.GameRoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Havocs_Teams_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Havocs_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Noises",
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
                    table.PrimaryKey("PK_Noises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Noises_Rounds_GameRoundId",
                        column: x => x.GameRoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Noises_Teams_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Noises_Services_ServiceId",
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
                name: "ServiceStatsSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn),
                    TeamId = table.Column<long>(nullable: false),
                    ServiceId = table.Column<long>(nullable: false),
                    AttackPoints = table.Column<double>(nullable: false),
                    LostDefensePoints = table.Column<double>(nullable: false),
                    ServiceLevelAgreementPoints = table.Column<double>(nullable: false),
                    RoundId = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceStatsSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceStatsSnapshots_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceStatsSnapshots_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceStatsSnapshots_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
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
                    GameRoundId = table.Column<long>(nullable: false),
                    PutTaskId = table.Column<long>(nullable: true)
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
                        name: "FK_Flags_CheckerTasks_PutTaskId",
                        column: x => x.PutTaskId,
                        principalTable: "CheckerTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Flags_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
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
                name: "IX_CheckerTasks_CurrentRoundId",
                table: "CheckerTasks",
                column: "CurrentRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckerTasks_RelatedRoundId",
                table: "CheckerTasks",
                column: "RelatedRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckerTasks_StartTime",
                table: "CheckerTasks",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_CheckerTasks_TeamId",
                table: "CheckerTasks",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_GameRoundId",
                table: "Flags",
                column: "GameRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_OwnerId",
                table: "Flags",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_PutTaskId",
                table: "Flags",
                column: "PutTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_ServiceId",
                table: "Flags",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Havocs_GameRoundId",
                table: "Havocs",
                column: "GameRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_Havocs_OwnerId",
                table: "Havocs",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Havocs_ServiceId",
                table: "Havocs",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Noises_GameRoundId",
                table: "Noises",
                column: "GameRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_Noises_OwnerId",
                table: "Noises",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Noises_ServiceId",
                table: "Noises",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundTeamServiceStates_GameRoundId",
                table: "RoundTeamServiceStates",
                column: "GameRoundId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundTeamServiceStates_ServiceId",
                table: "RoundTeamServiceStates",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundTeamServiceStates_TeamId",
                table: "RoundTeamServiceStates",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStats_ServiceId",
                table: "ServiceStats",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStats_TeamId",
                table: "ServiceStats",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStatsSnapshots_RoundId",
                table: "ServiceStatsSnapshots",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStatsSnapshots_ServiceId",
                table: "ServiceStatsSnapshots",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStatsSnapshots_TeamId",
                table: "ServiceStatsSnapshots",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmittedFlags_FlagId",
                table: "SubmittedFlags",
                column: "FlagId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmittedFlags_RoundId",
                table: "SubmittedFlags",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_SubmittedFlags_AttackerTeamId_FlagId",
                table: "SubmittedFlags",
                columns: new[] { "AttackerTeamId", "FlagId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Havocs");

            migrationBuilder.DropTable(
                name: "Noises");

            migrationBuilder.DropTable(
                name: "RoundTeamServiceStates");

            migrationBuilder.DropTable(
                name: "ServiceStats");

            migrationBuilder.DropTable(
                name: "ServiceStatsSnapshots");

            migrationBuilder.DropTable(
                name: "SubmittedFlags");

            migrationBuilder.DropTable(
                name: "Flags");

            migrationBuilder.DropTable(
                name: "Rounds");

            migrationBuilder.DropTable(
                name: "CheckerTasks");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "Teams");
        }
    }
}
