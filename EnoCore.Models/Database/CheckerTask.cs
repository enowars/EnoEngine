namespace EnoCore.Models.Database;

public enum CheckerTaskMethod
{
#pragma warning disable SA1300 // Element should begin with upper-case letter
    putflag,
    getflag,
    putnoise,
    getnoise,
    havoc,
#pragma warning restore SA1300 // Element should begin with upper-case letter
}

public enum CheckerResult
{
    INTERNAL_ERROR,
    OFFLINE,
    MUMBLE,
    OK,
}

public enum CheckerTaskLaunchStatus
{
    New,
    Launched,
    Done,
}

#pragma warning disable SA1201 // Elements should appear in the correct order
public sealed record CheckerTask(
#pragma warning restore SA1201 // Elements should appear in the correct order
    long Id,
    string CheckerUrl,
    CheckerTaskMethod Method,
    string Address,
    long ServiceId,
    string ServiceName,
    long TeamId,
    string TeamName,
    long RelatedRoundId,
    long CurrentRoundId,
    string? Payload,
    DateTimeOffset StartTime,
    int MaxRunningTime,
    long RoundLength,
    long UniqueVariantId,
    long VariantId,
    CheckerResult CheckerResult,
    string? ErrorMessage,
    string? AttackInfo,
    CheckerTaskLaunchStatus CheckerTaskLaunchStatus)
{
    public string GetTaskChainId()
    {
        return this.Method switch
        {
            CheckerTaskMethod.putflag => $"flag_s{this.ServiceId}_r{this.RelatedRoundId}_t{this.TeamId}_i{this.UniqueVariantId}",
            CheckerTaskMethod.getflag => $"flag_s{this.ServiceId}_r{this.RelatedRoundId}_t{this.TeamId}_i{this.UniqueVariantId}",
            CheckerTaskMethod.putnoise => $"noise_s{this.ServiceId}_r{this.RelatedRoundId}_t{this.TeamId}_i{this.UniqueVariantId}",
            CheckerTaskMethod.getnoise => $"noise_s{this.ServiceId}_r{this.RelatedRoundId}_t{this.TeamId}_i{this.UniqueVariantId}",
            CheckerTaskMethod.havoc => $"havoc_s{this.ServiceId}_r{this.RelatedRoundId}_t{this.TeamId}_i{this.UniqueVariantId}",
            _ => throw new NotImplementedException(),
        };
    }
}
