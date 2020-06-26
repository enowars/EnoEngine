using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Json
{
    class EnoEngineScoreboardInfo
    {
        private JsonConfiguration Config;
        public string Title { get => Config.Title; }
        public List<JsonConfigurationTeam> Teams { get => Config.Teams; }
        public EnoEngineScoreboardInfo(JsonConfiguration config)
        {
            Config = config;
        }

    }
}
