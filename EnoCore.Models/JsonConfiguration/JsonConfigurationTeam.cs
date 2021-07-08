namespace EnoCore.Models.JsonConfiguration
{
    using System;
    using System.ComponentModel;
    using Json.Schema.Generation;

    public class JsonConfigurationTeam
    {

        [Required]
        [Description("The id of the team.")]
        [Minimum(0)]
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

        //public JsonConfigurationTeam Validate(int subnetBytesLength)
        //{
        //    IPAddress ip;
        //    try
        //    {
        //        ip = IPAddress.Parse(this.TeamSubnet);
        //    }
        //    catch (Exception e)
        //    {
        //        throw new JsonConfigurationTeamValidationException($"Team subnet is no valid IP address (team {this.Id}).", e);
        //    }

        //    byte[] teamSubnet = new byte[subnetBytesLength];
        //    Array.Copy(ip.GetAddressBytes(), teamSubnet, subnetBytesLength);

        //    return new(this.Id,
        //        this.Name,
        //        this.Active,
        //        this.Address,
        //        this.CountryCode,
        //        this.LogoUrl,
        //        teamSubnet.ToString());
        //}
    }
}
