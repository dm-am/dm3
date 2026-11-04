using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace DM.Testing;

/// <summary>Collects one tag of every measurement of one counter, while the listener lives.</summary>
public static class CounterTags
{
    public static MeterListener Listen(string instrument, string tag, ICollection<string?> collected)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (published, self) =>
            {
                if (published.Name == instrument)
                {
                    self.EnableMeasurementEvents(published);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var measured in tags)
            {
                if (measured.Key == tag)
                {
                    collected.Add(measured.Value?.ToString());
                }
            }
        });

        listener.Start();
        return listener;
    }
}
