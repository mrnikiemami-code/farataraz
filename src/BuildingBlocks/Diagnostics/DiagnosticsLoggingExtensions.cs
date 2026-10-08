namespace FaraTaraz.BuildingBlocks.Diagnostics;

using System;
using System.Collections.Generic;
using FaraTaraz.BuildingBlocks.Errors;
using Microsoft.Extensions.Logging;

/// <summary>
/// Small, provider-independent structured-logging extension over <see cref="ILogger"/>.
///
/// It turns a <see cref="DiagnosticContext"/> plus an <see cref="ErrorDescriptor"/> into one
/// structured, searchable log event:
///
/// - named structured properties (<c>ErrorCode</c>, <c>ErrorCategory</c>, <c>ErrorSeverity</c>,
///   <c>CorrelationId</c>, <c>Module</c>, <c>Operation</c> and the available tenant/source
///   identifiers) are emitted as structured state, never baked into the message;
/// - <see cref="ErrorSeverity"/> is mapped to the matching <see cref="LogLevel"/>;
/// - an optional <see cref="Exception"/> is carried as the technical diagnostic, and is
///   deliberately kept out of the message template;
/// - the message template is a fixed, safe constant.
///
/// It logs nothing sensitive: no provider responses, tokens, credentials, customer data, or
/// request payloads. This is an ordinary <c>ILogger</c> extension, not a new logging framework.
/// </summary>
public static class DiagnosticsLoggingExtensions
{
    /// <summary>
    /// Fixed, safe log message template. Actual values are carried as named structured
    /// properties, so this template never changes shape and never leaks data.
    /// </summary>
    public const string ErrorLogTemplate =
        "Diagnostic error: {Module}.{Operation} [{ErrorCode}]";

    /// <summary>
    /// Emits one structured diagnostic event for a failed operation.
    /// </summary>
    /// <param name="logger">Target logger. Required.</param>
    /// <param name="context">Caller-supplied diagnostic context. Required.</param>
    /// <param name="descriptor">Error contract describing the failure. Required.</param>
    /// <param name="exception">
    /// Optional technical exception. Carried as the diagnostic <c>Exception</c> and never
    /// rendered into the message template.
    /// </param>
    public static void LogErrorWithDiagnostics(
        this ILogger logger,
        DiagnosticContext context,
        ErrorDescriptor descriptor,
        Exception? exception = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(descriptor);

        var level = MapSeverity(descriptor.Severity);

        var properties = new Dictionary<string, object?>
        {
            ["ErrorCode"] = descriptor.Code.Value,
            ["ErrorCategory"] = descriptor.Category.ToString(),
            ["ErrorSeverity"] = descriptor.Severity.ToString(),
            [nameof(DiagnosticContext.CorrelationId)] = context.CorrelationId,
            [nameof(DiagnosticContext.Module)] = context.Module,
            [nameof(DiagnosticContext.Operation)] = context.Operation,
        };

        if (context.HasTenant)
        {
            properties[nameof(DiagnosticContext.TenantId)] = context.TenantId!.ToString();
        }

        if (context.HasSource)
        {
            properties[nameof(DiagnosticContext.AccountingSourceId)] =
                context.AccountingSourceId!.ToString();
        }

        // The formatter returns the fixed template; the exception is passed separately so it is
        // captured as technical diagnostic state and never appears in the rendered message.
        logger.Log(level, default(EventId), properties, exception, (_, _) => ErrorLogTemplate);
    }

    private static LogLevel MapSeverity(ErrorSeverity severity) => severity switch
    {
        ErrorSeverity.Warning => LogLevel.Warning,
        ErrorSeverity.Error => LogLevel.Error,
        ErrorSeverity.Critical => LogLevel.Critical,
        _ => LogLevel.Error
    };
}
