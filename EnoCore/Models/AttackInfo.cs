namespace EnoCore.AttackInfo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using EnoCore.Models;

    public record AttackInfo(
        string[] AvailableTeams,
        Dictionary<string, AttackInfoService> Services);

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1502 // Element should not be on a single line
    public class AttackInfoService : Dictionary<string, AttackInfoServiceTeam>
    { }

    public class AttackInfoServiceTeam : Dictionary<long, AttackInfoServiceTeamRound>
    { }

    public class AttackInfoServiceTeamRound : Dictionary<long, string[]>
    { }
#pragma warning restore SA1502
#pragma warning restore SA1402
}
