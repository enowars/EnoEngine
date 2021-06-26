namespace EnoCore.Models.Scoreboard
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using EnoCore.Models;

    public class Scoreboard
    {
        public Scoreboard(long currentRound, string? startTimestamp, string? endTimestamp, string? dnsSuffix, ScoreboardService[] services, ScoreboardTeam[] teams)
        {
            this.CurrentRound = currentRound;
            this.StartTimestamp = startTimestamp;
            this.EndTimestamp = endTimestamp;
            this.DnsSuffix = dnsSuffix;
            this.Services = services;
            this.Teams = teams;
        }

        /// <summary>
        /// The ID of the current round.
        /// </summary>
        [Required]
        public long CurrentRound { get; init; }

        /// <summary>
        ///  Start timestamp of the current round according to ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ") in UTC.
        /// </summary>
        public string? StartTimestamp { get; init; }

        /// <summary>
        /// End timestamp of the current round according to ISO-86-01 ("yyyy-MM-ddTHH:mm:ss.fffZ") in UTC.
        /// </summary>
        public string? EndTimestamp { get; init; }

        /// <summary>
        /// The DNS suffix (including the leading dot), if DNS is used. Example: ".bambi.ovh".
        /// </summary>
        public string? DnsSuffix { get; init; }

        [Required]
        public ScoreboardService[] Services { get; init; }

        [Required]
        public ScoreboardTeam[] Teams { get; init; }
    }
}
