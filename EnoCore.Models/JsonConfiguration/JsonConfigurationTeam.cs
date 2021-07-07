namespace EnoCore.Models.JsonConfiguration
{
    using System;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Net;

    public class JsonConfigurationTeam
    {
        public JsonConfigurationTeam(long id, string name, bool? active, IPAddress address, string? countryCode, string? logoUrl, string teamSubnet)
        {
            this.Id = id;
            this.Name = name;
            this.Active = active ?? true;
            this.Address = address;
            this.CountryCode = countryCode;
            this.LogoUrl = logoUrl;
            this.TeamSubnet = teamSubnet;
        }

        [Required]
        [Description("The id of the team.")]
        [Range(minimum: 0, long.MaxValue)]
        public long Id { get; init; }

        [Required]
        [Description("The name of the team.")]
        public string Name { get; init; }

        [Description("Whether the team is active or not.")]
        public bool Active { get; init; }

        [Required]
        [Description("The IP address of the teams vulnbox.")]
        public IPAddress Address { get; init; }

        [Description("The URL to the country flag of the team")]
        public string? CountryCode { get; init; }

        [Description("The URL to the logo of the team.")]
        [UrlAttribute]
        public string? LogoUrl { get; init; }

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
