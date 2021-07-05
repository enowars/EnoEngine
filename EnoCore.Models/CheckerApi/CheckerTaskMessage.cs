namespace EnoCore.Models.CheckerApi
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using EnoCore.Models.Database;

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
            this.Flag = flag;
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

        public string? Flag { get; }

        [NotNull]
        public long? VariantId { get; }

        [NotNull]
        public long? Timeout { get; }

        [NotNull]
        public long? RoundLength { get; }

        [NotNull]
        public string? TaskChainId { get; }

        public override string ToString()
        {
            return $"CheckerTaskMessage(TaskId={this.TaskId}, Method={this.Method}, Address={this.Address}, TeamId={this.TeamId}, TeamName={this.TeamName}, CurrentRoundId={this.CurrentRoundId}, RelatedRoundId={this.RelatedRoundId}, Flag={this.Flag}, VariantId={this.VariantId}, Timeout={this.Timeout}, RoundLength={this.RoundLength}, TaskChainId={this.TaskChainId})";
        }
    }
}
