using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnoCore.Models.Json
{
    public class EnoEngineScoreboardInfo
    {
        private readonly JsonConfiguration Config;
        public string Title { get => Config.Title; }
        public List<EnoEngineScoreboardTeam> Teams { get => Config.Teams.Select(t => new EnoEngineScoreboardTeam(t)).ToList(); }
        public EnoEngineScoreboardInfo(JsonConfiguration config)
        {
            Config = config;
        }
    }
    public class EnoEngineScoreboardTeam
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string LogoUrl { get; set; }
        public string FlagUrl { get; set; }
        public bool Active { get; set; }
        public EnoEngineScoreboardTeam(JsonConfigurationTeam t)
        {
            Id = t.Id;
            Name = t.Name;
            LogoUrl = t.LogoUrl;
            FlagUrl = t.FlagUrl;
            Active = t.Active;
        }
    }
}
