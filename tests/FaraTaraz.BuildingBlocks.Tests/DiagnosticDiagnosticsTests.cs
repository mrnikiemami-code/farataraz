namespace FaraTaraz.BuildingBlocks.Tests;

using System;
using System.Collections.Generic;
using FaraTaraz.BuildingBlocks.Diagnostics;
using FaraTaraz.BuildingBlocks.Errors;
using FaraTaraz.BuildingBlocks.Identifiers;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Evidence for the diagnostics foundation: <see cref="DiagnosticContext"/> enforces its
/// required fields, and the <c>ILogger</c> extension emits the expected structured fields,
/// error code, and severity-derived log level — without leaking exception details into the
/// message template.
/// </summary>
public class DiagnosticDiagnosticsTests
{
    [Fact]
    public void DiagnosticContext_rejects_missing_required_values()
    {
        // CorrelationId, Module, Operation are all required and must be nonblank.
        Assert.Throws<ArgumentException>(() => new DiagnosticContext("", "Module", "Operation"));
        Assert.Throws<ArgumentException>(() => new DiagnosticContext("   ", "Module", "Operation"));
        Assert.Throws<ArgumentException>(() => new DiagnosticContext("corr-1", "", "Operation"));
        Assert.Throws<ArgumentException>(() => new DiagnosticContext("corr-1", "  ", "Operation"));
        Assert.Throws<ArgumentException>(() => new DiagnosticContext("corr-1", "Module", ""));
        Assert.Throws<ArgumentException>(() => new DiagnosticContext("corr-1", "Module", "   "));
    }

    [Fact]
    public void Captured_log_event_carries_structured_fields_and_severity()
    {
        var provider = new CaptureLoggerProvider();
        var logger = provider.CreateLogger("FaraTaraz.Sync.Customers");

        var context = new DiagnosticContext(
            correlationId: "corr-123",
            module: "Sync.Customers",
            operation: "Fetch",
            tenantId: new TenantId("tenant-1"),
            accountingSourceId: new AccountingSourceId("source-9"));

        var descriptor = new ErrorDescriptor(
            new ErrorCode("FT-ING-SYNC-003"),
            ErrorCategory.ProviderTransient,
            ErrorSeverity.Critical,
            isRetryable: true,
            safeMessageKey: "sync.source_record.unreadable");

        var technical = new InvalidOperationException("boom");
        logger.LogErrorWithDiagnostics(context, descriptor, technical);

        var captured = Assert.Single(provider.Records);
        Assert.Equal(technical, captured.Exception);
        Assert.Equal(LogLevel.Critical, captured.Level);

        var properties = ToProperties(captured.State);
        Assert.Equal("FT-ING-SYNC-003", properties["ErrorCode"]);
        Assert.Equal("ProviderTransient", properties["ErrorCategory"]);
        Assert.Equal("Critical", properties["ErrorSeverity"]);
        Assert.Equal("corr-123", properties["CorrelationId"]);
        Assert.Equal("Sync.Customers", properties["Module"]);
        Assert.Equal("Fetch", properties["Operation"]);
        Assert.Equal("tenant-1", properties["TenantId"]);
        Assert.Equal("source-9", properties["AccountingSourceId"]);

        // The message template is fixed and carries no exception details.
        Assert.DoesNotContain("boom", captured.FormattedMessage);
        Assert.DoesNotContain("InvalidOperationException", captured.FormattedMessage);
    }

    private static Dictionary<string, object?> ToProperties(object? state)
    {
        if (state is IReadOnlyDictionary<string, object?> dictionary)
        {
            return new Dictionary<string, object?>(dictionary);
        }

        return new Dictionary<string, object?>();
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        private readonly List<CapturedLog> _records = new();

        public IReadOnlyList<CapturedLog> Records => _records;

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);

        public void Dispose()
        {
        }

        internal void Record(CapturedLog log) => _records.Add(log);
    }

    private sealed class CaptureLogger : ILogger
    {
        private readonly CaptureLoggerProvider _provider;

        public CaptureLogger(CaptureLoggerProvider provider) => _provider = provider;

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullDisposable.Instance;

        public void Log<TState>(
            LogLevel level,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string>? formatter)
        {
            _provider.Record(
                new CapturedLog(level, eventId, state, exception, formatter?.Invoke(state, exception)));
        }
    }

    private sealed class CapturedLog
    {
        public CapturedLog(
            LogLevel level,
            EventId eventId,
            object? state,
            Exception? exception,
            string? formattedMessage)
        {
            Level = level;
            EventId = eventId;
            State = state;
            Exception = exception;
            FormattedMessage = formattedMessage;
        }

        public LogLevel Level { get; }

        public EventId EventId { get; }

        public object? State { get; }

        public Exception? Exception { get; }

        public string? FormattedMessage { get; }
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
