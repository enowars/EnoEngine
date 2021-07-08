namespace EnoCore.Models.JsonConfiguration
{
    using System;
    using System.ComponentModel;
    using System.Net;
    using Json.Schema.Generation;

    public class JsonConfigurationTeam
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        [Required]
        [Description("The id of the team.")]
        [Minimum(1)]
        [Maximum(uint.MaxValue)]
        public long Id { get; init; }

        [Required]
        [Description("The name of the team.")]
        public string Name { get; init; }

        [Description("Whether the team is active or not.")]
        [Nullable(true)]
        public bool? Active { get; init; }

        [Required]
        [Description("The IP address of the teams vulnbox.")]
        public string Address { get; init; }

        [Description("The URL to the country flag of the team")]

        public string? CountryCode { get; init; }

        [Description("The URL to the logo of the team.")]
        public Uri? LogoUrl { get; init; }

        [Required]
        [Description("The Teams Subnet.")]
        public string TeamSubnet { get; init; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    }
}
