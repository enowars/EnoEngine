namespace EnoCore.Models.Json
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Reflection.Metadata.Ecma335;
    using System.Text;
    using System.Text.Json.Serialization;
    using EnoCore.Models.Database;

    public record EnoLogMessage(
        string? Tool,
        string Severity,
        long SeverityLevel,
        string? Timestamp,
        string? Module,
        string? Function,
        string? Flag,
        long? FlagIndex,
        long? RunId,
        long? RoundId,
        long? RelatedRoundId,
        string Message,
        string? TeamName,
        long TeamId, // TODO switch to optional long?
        string? ServiceName,
        string? Method,
        string? Type = "infrastructure")
    {
        public static EnoLogMessage FromCheckerTask(CheckerTask task, string tool, string severity, long severityLevel, string? module, string? function, string message)
        {
            return new(
                tool,
                severity,
                severityLevel,
                EnoCoreUtil.GetCurrentTimestamp(),
                module,
                function,
                task.Payload,
                task.TaskIndex,
                task.Id,
                task.CurrentRoundId,
                task.RelatedRoundId,
                message,
                task.TeamName,
                task.TeamId,
                task.ServiceName,
                Enum.GetName(typeof(CheckerTaskMethod), task.Method));
        }

        public static EnoLogMessage FromCheckerTaskMessage(CheckerTaskMessage taskMessage, string tool, string severity, long severityLevel, string? module, string? function, string message)
        {
            return new(
                tool,
                severity,
                severityLevel,
                EnoCoreUtil.GetCurrentTimestamp(),
                module,
                function,
                taskMessage.Flag,
                taskMessage.FlagIndex,
                taskMessage.RunId,
                taskMessage.RoundId,
                taskMessage.RelatedRoundId,
                message,
                taskMessage.TeamName,
                taskMessage.TeamId,
                taskMessage.ServiceName,
                Enum.GetName(typeof(CheckerTaskMethod), taskMessage.Method));
        }
    }
}
