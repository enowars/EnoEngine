﻿// <auto-generated />
using System;
using EnoDatabase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EnoDatabase.Migrations
{
    [DbContext(typeof(EnoDbContext))]
    [Migration("20221015193036_m2")]
    partial class m2
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("EnoCore.Models.Database.CheckerTask", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<string>("Address")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("AttackInfo")
                        .HasColumnType("text");

                    b.Property<int>("CheckerResult")
                        .HasColumnType("integer");

                    b.Property<int>("CheckerTaskLaunchStatus")
                        .HasColumnType("integer");

                    b.Property<string>("CheckerUrl")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("CurrentRoundId")
                        .HasColumnType("bigint");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("text");

                    b.Property<int>("MaxRunningTime")
                        .HasColumnType("integer");

                    b.Property<int>("Method")
                        .HasColumnType("integer");

                    b.Property<string>("Payload")
                        .HasColumnType("text");

                    b.Property<long>("RelatedRoundId")
                        .HasColumnType("bigint");

                    b.Property<long>("RoundLength")
                        .HasColumnType("bigint");

                    b.Property<long>("ServiceId")
                        .HasColumnType("bigint");

                    b.Property<string>("ServiceName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTimeOffset>("StartTime")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("TeamId")
                        .HasColumnType("bigint");

                    b.Property<string>("TeamName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("UniqueVariantId")
                        .HasColumnType("bigint");

                    b.Property<long>("VariantId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("CheckerTaskLaunchStatus", "StartTime");

                    b.HasIndex("CurrentRoundId", "RelatedRoundId", "CheckerResult");

                    b.ToTable("CheckerTasks");
                });

            modelBuilder.Entity("EnoCore.Models.Database.Configuration", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<int>("CheckedRoundsPerRound")
                        .HasColumnType("integer");

                    b.Property<string>("DnsSuffix")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Encoding")
                        .HasColumnType("integer");

                    b.Property<byte[]>("FlagSigningKey")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.Property<long>("FlagValidityInRounds")
                        .HasColumnType("bigint");

                    b.Property<int>("RoundLengthInSeconds")
                        .HasColumnType("integer");

                    b.Property<int>("TeamSubnetBytesLength")
                        .HasColumnType("integer");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("Configurations");
                });

            modelBuilder.Entity("EnoCore.Models.Database.Round", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<DateTimeOffset>("Begin")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("End")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("Quarter2")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("Quarter3")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTimeOffset>("Quarter4")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.ToTable("Rounds");
                });

            modelBuilder.Entity("EnoCore.Models.Database.RoundTeamServiceStatus", b =>
                {
                    b.Property<long>("ServiceId")
                        .HasColumnType("bigint");

                    b.Property<long>("TeamId")
                        .HasColumnType("bigint");

                    b.Property<long>("GameRoundId")
                        .HasColumnType("bigint");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("text");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.HasKey("ServiceId", "TeamId", "GameRoundId");

                    b.HasIndex("ServiceId", "GameRoundId", "Status");

                    b.ToTable("RoundTeamServiceStatus");
                });

            modelBuilder.Entity("EnoCore.Models.Database.Service", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<bool>("Active")
                        .HasColumnType("boolean");

                    b.Property<string[]>("Checkers")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<long>("FlagVariants")
                        .HasColumnType("bigint");

                    b.Property<long>("FlagsPerRound")
                        .HasColumnType("bigint");

                    b.Property<long>("HavocVariants")
                        .HasColumnType("bigint");

                    b.Property<long>("HavocsPerRound")
                        .HasColumnType("bigint");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<long>("NoiseVariants")
                        .HasColumnType("bigint");

                    b.Property<long>("NoisesPerRound")
                        .HasColumnType("bigint");

                    b.Property<double>("WeightFactor")
                        .HasColumnType("double precision");

                    b.HasKey("Id");

                    b.ToTable("Services");
                });

            modelBuilder.Entity("EnoCore.Models.Database.SubmittedFlag", b =>
                {
                    b.Property<long>("FlagServiceId")
                        .HasColumnType("bigint");

                    b.Property<long>("FlagRoundId")
                        .HasColumnType("bigint");

                    b.Property<long>("FlagOwnerId")
                        .HasColumnType("bigint");

                    b.Property<int>("FlagRoundOffset")
                        .HasColumnType("integer");

                    b.Property<long>("AttackerTeamId")
                        .HasColumnType("bigint");

                    b.Property<long>("RoundId")
                        .HasColumnType("bigint");

                    b.Property<long>("SubmissionsCount")
                        .HasColumnType("bigint");

                    b.Property<DateTimeOffset>("Timestamp")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("FlagServiceId", "FlagRoundId", "FlagOwnerId", "FlagRoundOffset", "AttackerTeamId");

                    b.HasIndex("FlagServiceId", "AttackerTeamId", "RoundId");

                    b.HasIndex("FlagServiceId", "FlagRoundOffset", "Timestamp");

                    b.HasIndex("FlagServiceId", "FlagOwnerId", "RoundId", "FlagRoundOffset");

                    b.ToTable("SubmittedFlags");
                });

            modelBuilder.Entity("EnoCore.Models.Database.Team", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<long>("Id"));

                    b.Property<bool>("Active")
                        .HasColumnType("boolean");

                    b.Property<string>("Address")
                        .HasColumnType("text");

                    b.Property<double>("AttackPoints")
                        .HasColumnType("double precision");

                    b.Property<string>("CountryCode")
                        .HasColumnType("text");

                    b.Property<double>("DefensePoints")
                        .HasColumnType("double precision");

                    b.Property<string>("LogoUrl")
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<double>("ServiceLevelAgreementPoints")
                        .HasColumnType("double precision");

                    b.Property<long>("ServiceStatsId")
                        .HasColumnType("bigint");

                    b.Property<byte[]>("TeamSubnet")
                        .IsRequired()
                        .HasColumnType("bytea");

                    b.Property<double>("TotalPoints")
                        .HasColumnType("double precision");

                    b.HasKey("Id");

                    b.ToTable("Teams");
                });

            modelBuilder.Entity("EnoCore.Models.Database.TeamServicePoints", b =>
                {
                    b.Property<long>("TeamId")
                        .HasColumnType("bigint");

                    b.Property<long>("ServiceId")
                        .HasColumnType("bigint");

                    b.Property<double>("AttackPoints")
                        .HasColumnType("double precision");

                    b.Property<double>("DefensePoints")
                        .HasColumnType("double precision");

                    b.Property<string>("ErrorMessage")
                        .HasColumnType("text");

                    b.Property<double>("ServiceLevelAgreementPoints")
                        .HasColumnType("double precision");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.HasKey("TeamId", "ServiceId");

                    b.ToTable("TeamServicePoints");
                });

            modelBuilder.Entity("EnoCore.Models.Database.TeamServicePointsSnapshot", b =>
                {
                    b.Property<long>("ServiceId")
                        .HasColumnType("bigint");

                    b.Property<long>("RoundId")
                        .HasColumnType("bigint");

                    b.Property<long>("TeamId")
                        .HasColumnType("bigint");

                    b.Property<double>("AttackPoints")
                        .HasColumnType("double precision");

                    b.Property<double>("LostDefensePoints")
                        .HasColumnType("double precision");

                    b.Property<double>("ServiceLevelAgreementPoints")
                        .HasColumnType("double precision");

                    b.HasKey("ServiceId", "RoundId", "TeamId");

                    b.ToTable("TeamServicePointsSnapshot");
                });

            modelBuilder.Entity("EnoCore.Models.Database.TeamServicePoints", b =>
                {
                    b.HasOne("EnoCore.Models.Database.Team", null)
                        .WithMany("TeamServicePoints")
                        .HasForeignKey("TeamId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EnoCore.Models.Database.Team", b =>
                {
                    b.Navigation("TeamServicePoints");
                });
#pragma warning restore 612, 618
        }
    }
}
