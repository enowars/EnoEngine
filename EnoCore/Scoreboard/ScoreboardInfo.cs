namespace EnoCore.Scoreboard
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;

    public record ScoreboardInfo(
        string Title,
        ScoreboardInfoTeam[] Teams);

    public record ScoreboardInfoTeam(
        long Id,
        string Name,
        string? LogoUrl,
        string? FlagUrl,
        bool Active);
}
