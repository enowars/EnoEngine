namespace EnoCore.Models.CheckerApi
{
    using System;

    public sealed record CheckerInfoMessage(
        string ServiceName,
        int FlagVariants,
        int NoiseVariants,
        int HavocVariants);
}
