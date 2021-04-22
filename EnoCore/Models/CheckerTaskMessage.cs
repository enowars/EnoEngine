namespace EnoCore.Models
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    public class CheckerTaskMessage
    {
        public CheckerTaskMessage(
            long? taskId,
            CheckerTaskMethod? method,
            string? address,
            long? teamId,
            string? teamName,
            long? currentRoundId,
            long? relatedRoundId,
            string? flag,
            long? variantId,
            long? timeout,
            long? roundLength,
            string? taskChainId)
        {
            this.TaskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
            this.Method = method ?? throw new ArgumentNullException(nameof(method));
            this.Address = address ?? throw new ArgumentNullException(nameof(address));
            this.TeamId = teamId ?? throw new ArgumentNullException(nameof(teamId));
            this.TeamName = teamName ?? throw new ArgumentNullException(nameof(teamName));
            this.CurrentRoundId = currentRoundId ?? throw new ArgumentNullException(nameof(currentRoundId));
            this.RelatedRoundId = relatedRoundId ?? throw new ArgumentNullException(nameof(relatedRoundId));
            this.Flag = flag ?? throw new ArgumentNullException(nameof(flag));
            this.VariantId = variantId ?? throw new ArgumentNullException(nameof(variantId));
            this.Timeout = timeout ?? throw new ArgumentNullException(nameof(timeout));
            this.RoundLength = roundLength ?? throw new ArgumentNullException(nameof(roundLength));
            this.TaskChainId = taskChainId ?? throw new ArgumentNullException(nameof(taskChainId));
        }

        [NotNull]
        public long? TaskId { get; }

        [NotNull]
        public CheckerTaskMethod? Method { get; }

        [NotNull]
        public string? Address { get; }

        [NotNull]
        public long? TeamId { get; }

        [NotNull]
        public string? TeamName { get; }

        [NotNull]
        public long? CurrentRoundId { get; }

        [NotNull]
        public long? RelatedRoundId { get; }

        [NotNull]
        public string? Flag { get; }

        [NotNull]
        public long? VariantId { get; }

        [NotNull]
        public long? Timeout { get; }

        [NotNull]
        public long? RoundLength { get; }

        [NotNull]
        public string? TaskChainId { get; }
    }
}
