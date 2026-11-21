using TaskFlow.Api.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace TaskFlow.Tests.Presence
{
    // ADR 0004 condition 1: a targeted unit test of the enricher itself
    // (pure, no host/log-sink wiring needed) - asserts access_token is
    // never left in a log event's properties after enrichment.
    public class AccessTokenRedactionEnricherTests
    {
        [Fact]
        public void Enrich_RedactsAccessTokenQueryValue_FromAStringProperty()
        {
            var enricher = new AccessTokenRedactionEnricher();
            var propertyFactory = new SimplePropertyFactory();
            var logEvent = BuildLogEvent("Path", "/hubs/presence/negotiate?access_token=eyJhbGciOiJI.secret.value&other=1");

            enricher.Enrich(logEvent, propertyFactory);

            var value = Assert.IsType<ScalarValue>(logEvent.Properties["Path"]);
            var text = Assert.IsType<string>(value.Value);
            Assert.DoesNotContain("eyJhbGciOiJI.secret.value", text);
            Assert.Contains("access_token=REDACTED", text);
        }

        [Fact]
        public void Enrich_LeavesOrdinaryProperties_Untouched()
        {
            var enricher = new AccessTokenRedactionEnricher();
            var propertyFactory = new SimplePropertyFactory();
            var logEvent = BuildLogEvent("Path", "/api/v1/users");

            enricher.Enrich(logEvent, propertyFactory);

            var value = Assert.IsType<ScalarValue>(logEvent.Properties["Path"]);
            Assert.Equal("/api/v1/users", value.Value);
        }

        private static LogEvent BuildLogEvent(string propertyName, string propertyValue)
        {
            var properties = new List<LogEventProperty> { new(propertyName, new ScalarValue(propertyValue)) };
            return new LogEvent(
                DateTimeOffset.UtcNow,
                LogEventLevel.Information,
                null,
                new MessageTemplate(propertyName, []),
                properties);
        }

        private class SimplePropertyFactory : Serilog.Core.ILogEventPropertyFactory
        {
            public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
                new(name, new ScalarValue(value));
        }
    }
}
