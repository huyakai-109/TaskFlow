using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace TaskFlow.Api.Logging
{
    // ADR 0004 condition 1: a defense-in-depth enricher alongside the pinned
    // log-level overrides in appsettings.json - redacts an access_token
    // query value from any string property on any log event (path, request
    // URI, exception message), regardless of which logger produced it.
    public class AccessTokenRedactionEnricher : ILogEventEnricher
    {
        private static readonly Regex AccessTokenPattern = new(
            "access_token=[^&\\s\"']+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            foreach (var (key, value) in logEvent.Properties)
            {
                if (value is ScalarValue { Value: string text } && AccessTokenPattern.IsMatch(text))
                {
                    var redacted = AccessTokenPattern.Replace(text, "access_token=REDACTED");
                    logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, redacted));
                }
            }
        }
    }
}
