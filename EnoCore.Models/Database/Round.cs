namespace EnoCore.Models.Database;

public enum RoundStatus
{
    Prepared,
    Running,
    Finished,
    Scored,
}

public sealed record Round
{
    public Round(long id, DateTimeOffset? begin, DateTimeOffset? end, RoundStatus status)
    {
        this.Id = id;
        this.Begin = begin;
        this.End = end;
        this.Status = status;
    }

    public long Id {  get; set; }

    public DateTimeOffset? Begin { get; set; }

    public DateTimeOffset? End { get; set; }

    public RoundStatus Status { get; set; }
}
