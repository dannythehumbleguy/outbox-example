using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using System.Text.Json;

internal sealed class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        var activity = Activity.Current;
        if (activity is null) return;
        logEvent.AddPropertyIfAbsent(factory.CreateProperty("TraceId", activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(factory.CreateProperty("SpanId", activity.SpanId.ToString()));
    }
}

internal sealed class LokiFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        var dict = new Dictionary<string, object?>(logEvent.Properties.Count + 2)
        {
            ["Message"] = logEvent.RenderMessage(),
            ["level"] = logEvent.Level.ToString().ToLowerInvariant()
        };

        if (logEvent.Exception is not null)
            dict["Exception"] = logEvent.Exception.ToString();

        foreach (var (key, value) in logEvent.Properties)
            dict[key] = Unwrap(value);

        output.Write(JsonSerializer.Serialize(dict));
        output.WriteLine();
    }

    private static object? Unwrap(LogEventPropertyValue value) => value switch
    {
        ScalarValue { Value: null } => null,
        ScalarValue sv => sv.Value,
        SequenceValue seq => seq.Elements.Select(Unwrap).ToArray(),
        StructureValue str => str.Properties.ToDictionary(p => p.Name, p => Unwrap(p.Value)),
        DictionaryValue dict => dict.Elements.ToDictionary(
            kv => kv.Key.Value?.ToString() ?? string.Empty,
            kv => Unwrap(kv.Value)),
        _ => value.ToString()
    };
}
