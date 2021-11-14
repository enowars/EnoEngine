namespace EnoCore;

public static class CheckerResultExtensions
{
    public static ServiceStatus AsServiceStatus(this CheckerResult checkerResult)
    {
        return checkerResult switch
        {
            CheckerResult.OK => ServiceStatus.OK,
            CheckerResult.MUMBLE => ServiceStatus.MUMBLE,
            CheckerResult.OFFLINE => ServiceStatus.OFFLINE,
            _ => ServiceStatus.INTERNAL_ERROR,
        };
    }
}
