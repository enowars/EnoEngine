namespace EnoCore.Models.Database;

public record EnoLogMessage(
    string? Tool,
    string Severity,
    long SeverityLevel,
    string? Timestamp,
    string? Module,
    string? Function,
    string? Flag,
    long? VariantId,
    string? TaskChainId,
    long? TaskId,
    long? CurrentRoundId,
    long? RelatedRoundId,
    string Message,
    string? TeamName,
    long? TeamId,
    string? ServiceName,
    string? Method,
    string? Type = "infrastructure");
