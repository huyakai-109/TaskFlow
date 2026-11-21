using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TaskFlow.Tests.Fixtures
{
    // f09-reports.md R17: a minimal in-test ILoggerFactory/ILogger pair that
    // captures formatted log lines, so a test can assert the reports/export
    // log entry's shape without a new logging/test package. Swapped in for
    // one test's host only (WebApplicationFactory.WithWebHostBuilder,
    // replacing the production Serilog registration) - every other test
    // keeps the real pipeline untouched.
    public class CapturingLoggerFactory : ILoggerFactory
    {
        public ConcurrentBag<string> Messages { get; } = new();

        public void AddProvider(ILoggerProvider provider)
        {
            // Not needed - this factory is the whole pipeline for its test.
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private class CapturingLogger(CapturingLoggerFactory owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                owner.Messages.Add(formatter(state, exception));
            }
        }
    }
}
