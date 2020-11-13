namespace EnoCore.Checker
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using EnoCore.Models;

    public interface IChecker
    {
        int FlagsPerRound { get; }

        int NoisesPerRound { get; }

        int HavocsPerRound { get; }

        Task HandlePutFlag(CheckerTaskMessage task, CancellationToken token);

        Task HandleGetFlag(CheckerTaskMessage task, CancellationToken token);

        Task HandlePutNoise(CheckerTaskMessage task, CancellationToken token);

        Task HandleGetNoise(CheckerTaskMessage task, CancellationToken token);

        Task HandleHavoc(CheckerTaskMessage task, CancellationToken token);
    }
}
