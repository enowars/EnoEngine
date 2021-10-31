namespace EnoDatabase
{
    using System;
    using System.Threading.Channels;
    using EnoCore;
    using EnoCore.Models.Database;

    public record FlagSubmissionRequest(
        byte[] FlagString,
        Flag Flag,
        long AttackerTeamId,
        ChannelWriter<(byte[] Flag, FlagSubmissionResult Result)> Writer)
        : IComparable
    {
        public int CompareTo(object? obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException();
            }

            if (obj is FlagSubmissionRequest request)
            {
                if (this.Flag.OwnerId < request.Flag.OwnerId)
                {
                    return -1;
                }
                else if (this.Flag.OwnerId > request.Flag.OwnerId)
                {
                    return 1;
                }
                else if (this.Flag.ServiceId < request.Flag.ServiceId)
                {
                    return -1;
                }
                else if (this.Flag.ServiceId > request.Flag.ServiceId)
                {
                    return 1;
                }
                else if (this.Flag.RoundOffset < request.Flag.RoundOffset)
                {
                    return -1;
                }
                else if (this.Flag.RoundOffset > request.Flag.RoundOffset)
                {
                    return 1;
                }
                else if (this.Flag.RoundId < request.Flag.RoundId)
                {
                    return -1;
                }
                else if (this.Flag.RoundId > request.Flag.RoundId)
                {
                    return 1;
                }
                else if (this.AttackerTeamId < request.AttackerTeamId)
                {
                    return -1;
                }
                else if (this.AttackerTeamId > request.AttackerTeamId)
                {
                    return 1;
                }

                return 0;
            }
            else
            {
                throw new ArgumentException();
            }
        }
    }
}
