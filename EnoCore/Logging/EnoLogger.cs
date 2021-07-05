﻿namespace EnoCore.Logging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using EnoCore.Models;
    using EnoCore.Models.CheckerApi;
    using EnoCore.Models.Database;
    using Microsoft.Extensions.Logging;

    public class EnoLogger : ILogger
    {
        private readonly string categoryName;
        private readonly IEnoLogMessageProvider provider;
        private readonly string tool;
        private readonly string? serviceName;

        public EnoLogger(IEnoLogMessageProvider provider, string categoryName, string tool, string? serviceName = null)
        {
            this.provider = provider;
            this.categoryName = categoryName;
            this.tool = tool;
            this.serviceName = serviceName;
        }

        public static string GetSeverity(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.None => "DEBUG",
                LogLevel.Trace => "DEBUG",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARNING",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "CRITICAL",
                _ => throw new InvalidOperationException(),
            };
        }

        public static long GetSeverityLevel(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.None => 0,
                LogLevel.Trace => 0,
                LogLevel.Debug => 0,
                LogLevel.Information => 1,
                LogLevel.Warning => 2,
                LogLevel.Error => 3,
                LogLevel.Critical => 4,
                _ => throw new InvalidOperationException(),
            };
        }

        public IDisposable? BeginScope<TState>(TState state)
        {
            return this.provider.ScopeProvider?.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (this.IsEnabled(logLevel))
            {
                string message = exception?.Message ?? state?.ToString() ?? string.Empty;
                string module = this.categoryName;
                string tool = this.tool;
                string timestamp = EnoCoreUtil.GetCurrentTimestamp();
                string severity = GetSeverity(logLevel);
                long severityLevel = GetSeverityLevel(logLevel);

                bool hadScope = false;
                if (this.provider.ScopeProvider != null)
                {
                    this.provider.ScopeProvider.ForEachScope(
                        (value, loggingProps) =>
                        {
                            if (value is IEnumerable<KeyValuePair<string, object>> props)
                            {
                                foreach (var pair in props)
                                {
                                    if (pair.Value is CheckerTask task)
                                    {
                                        var enoLogMessage = new EnoLogMessage(
                                            tool,
                                            severity,
                                            severityLevel,
                                            EnoCoreUtil.GetCurrentTimestamp(),
                                            module,
                                            null,
                                            task.Payload,
                                            task.UniqueVariantId,
                                            task.GetTaskChainId(),
                                            task.Id,
                                            task.CurrentRoundId,
                                            task.RelatedRoundId,
                                            message,
                                            task.TeamName,
                                            task.TeamId,
                                            task.ServiceName,
                                            Enum.GetName(typeof(CheckerTaskMethod), task.Method));
                                        this.provider.Log($"##ENOLOGMESSAGE {JsonSerializer.Serialize(enoLogMessage, EnoCoreUtil.CamelCaseEnumConverterOptions)}\n");
                                        hadScope = true;
                                    }
                                    else if (pair.Value is CheckerTaskMessage taskMessage)
                                    {
                                        var enoLogMessage = new EnoLogMessage(
                                            tool,
                                            severity,
                                            severityLevel,
                                            EnoCoreUtil.GetCurrentTimestamp(),
                                            module,
                                            null,
                                            taskMessage.Flag,
                                            taskMessage.VariantId,
                                            taskMessage.TaskChainId,
                                            taskMessage.TaskId,
                                            taskMessage.CurrentRoundId,
                                            taskMessage.RelatedRoundId,
                                            message,
                                            taskMessage.TeamName,
                                            taskMessage.TeamId,
                                            this.serviceName,
                                            Enum.GetName(typeof(CheckerTaskMethod), taskMessage.Method));
                                        this.provider.Log($"##ENOLOGMESSAGE {JsonSerializer.Serialize(enoLogMessage, EnoCoreUtil.CamelCaseEnumConverterOptions)}\n");
                                        hadScope = true;
                                    }
                                }
                            }
                        },
                        state);
                }

                if (!hadScope)
                {
                    var enoLogMessage = new EnoLogMessage(
                        tool,
                        severity,
                        severityLevel,
                        EnoCoreUtil.GetCurrentTimestamp(),
                        module,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        message,
                        null,
                        null,
                        this.serviceName,
                        null);
                    this.provider.Log($"##ENOLOGMESSAGE {JsonSerializer.Serialize(enoLogMessage, EnoCoreUtil.CamelCaseEnumConverterOptions)}\n");
                }
            }
        }
    }
}
