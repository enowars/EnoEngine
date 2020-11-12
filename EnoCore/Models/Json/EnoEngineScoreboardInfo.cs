namespace EnoCore.Models.Json
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using EnoCore.Models.Database;

    public class EnoEngineScoreboardInfo
    {
        private readonly Configuration config;

        public EnoEngineScoreboardInfo(Configuration config)
        {
            this.config = config;
        }

        public string Title { get => this.config.Title; }
        public List<EnoEngineScoreboardTeam> Teams { get => this.config.Teams.Select(t => EnoEngineScoreboardTeam.FromConfigurationTeam(t)).ToList(); }
    }

#pragma warning disable SA1201 // Elements should appear in the correct order
    public record EnoEngineScoreboardTeam(
#pragma warning restore SA1201 // Elements should appear in the correct order
        long Id,
        string Name,
        string? LogoUrl,
        string? FlagUrl,
        bool Active)
    {
        public static EnoEngineScoreboardTeam FromConfigurationTeam(ConfigurationTeam t)
        {
            return new(
                t.Id,
                t.Name,
                t.LogoUrl,
                t.FlagUrl,
                t.Active);
        }
    }
}
