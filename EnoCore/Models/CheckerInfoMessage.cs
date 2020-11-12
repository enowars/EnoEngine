namespace EnoCore.Models
{
    using System;

    public sealed record CheckerInfoMessage(
        string ServiceName,
        int FlagCount,
        int NoiseCount,
        int HavocCount);
}
