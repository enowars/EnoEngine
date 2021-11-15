namespace EnoFlagSink;

public class TeamFlagSubmissionStatistic
{
#pragma warning disable SA1401 // Fields should be private
    public long OkFlags;
    public long DuplicateFlags;
    public long OldFlags;
    public long InvalidFlags;
    public long OwnFlags;
#pragma warning restore SA1401 // Fields should be private

    internal TeamFlagSubmissionStatistic()
    {
        this.OkFlags = 0;
        this.DuplicateFlags = 0;
        this.OldFlags = 0;
        this.InvalidFlags = 0;
        this.OwnFlags = 0;
    }
}
