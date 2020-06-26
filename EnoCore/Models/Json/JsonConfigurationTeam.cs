using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Json
{
    public class JsonConfigurationTeam
    {
#pragma warning disable CS8618
        public long Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string TeamSubnet { get; set; }
        public string LogoUrl { get; set; }
        public string FlagUrl { get; set; }
        public bool Active { get; set; }
#pragma warning restore CS8618
    }
}
