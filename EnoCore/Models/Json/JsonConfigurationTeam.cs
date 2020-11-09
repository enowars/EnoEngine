using EnoCore.Models.Database;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnoCore.Models.Json
{
    public class JsonConfigurationTeamValidationException : Exception
    {
        public JsonConfigurationTeamValidationException(string message) : base(message) { }
    }

    public class JsonConfigurationTeam
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? TeamSubnet { get; set; }
        public string? LogoUrl { get; set; }
        public string? FlagUrl { get; set; }
        public bool Active { get; set; }

        internal ConfigurationTeam Validate()
        {
            if (Id == 0)
                throw new JsonConfigurationTeamValidationException("Team id must not be 0.");

            if (Name is null)
                throw new JsonConfigurationTeamValidationException($"Team name must not be null (team {Id}).");

            if (TeamSubnet is null)
                throw new JsonConfigurationTeamValidationException($"Team subnet must not be null (team {Id}).");

            return new(Id,
                Name,
                Address,
                TeamSubnet,
                LogoUrl,
                FlagUrl,
                Active);
        }
    }
}
