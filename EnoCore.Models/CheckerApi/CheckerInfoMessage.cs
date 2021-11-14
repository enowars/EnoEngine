namespace EnoCore.Models.CheckerApi;

public sealed record CheckerInfoMessage(
    string ServiceName,
    int FlagVariants,
    int NoiseVariants,
    int HavocVariants);
