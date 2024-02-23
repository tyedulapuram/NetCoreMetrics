using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using static Microsoft.Diagnostics.Tracing.Analysis.TraceLoadedDotNetRuntimeExtensions;

namespace TestHttpClientSettings
{
    public class PlatformCountersListener : EventListener
    {
        /// <summary>
        /// The interval window in seconds to update the counters.
        /// </summary>
        private const int CounterUpdateInterval = 30;

        /// <summary>
        /// The event sources that the class will listen to.
        /// </summary>
        private static readonly HashSet<string> WantedEventCounterSources = new HashSet<string>()
        {
            //"System.Runtime",
            //"System.Net.Http",
            "Microsoft-Windows-DotNETRuntime"

        };

        /// <summary>
        /// The event counters under the above event counter sources that the class will write to metrics.
        /// For a description of each counter, see https://docs.microsoft.com/en-us/dotnet/core/diagnostics/available-counters
        /// To add a new counter, make sure its source is in the WantedEventCounterSources list.
        /// </summary>
        private static readonly HashSet<string> WantedEventCounters = new HashSet<string>()
        {
              "CPU Usage" ,
              "ThreadPool Completed Work Item Count" ,
             "ThreadPool Queue Length" ,
             "ThreadPool Thread Count" ,
             "Number of Active Timers" ,
             "GC Fragmentation" ,
             "GC Committed Bytes" ,
             "GC Heap Size" ,
             "Gen 0 GC Count" ,
             "Gen 1 GC Count" ,
             "Gen 2 GC Count" ,
             "Gen 0 Size" ,
             "Gen 1 Size" ,
             "Gen 2 Size" ,
             "LOH Size" ,
             "POH (Pinned Object Heap) Size" ,
             "Exception Count" ,
             "Monitor Lock Contention Count" ,
             "Allocation Rate" ,
             "% Time in GC since last GC" ,
             // Counters for System.Net.Http
             "Current Requests" ,
             "Requests Started" ,
             "Requests Failed" ,
             "Current Http 1.1 Connections" ,
             "Current Http 2.0 Connections" ,
             "HTTP 1.1 Requests Queue Duration" ,
             "HTTP 2.0 Requests Queue Duration" 
        };

        /// <summary>
        /// The metrics writer
        /// </summary>
        private readonly IMetricsWriter metricWriter;

        public PlatformCountersListener(IMetricsWriter metricWriter)
        {

            this.metricWriter = metricWriter;
        }

        /// <summary>
        /// Initializes the PlatformCountersListener.
        /// </summary>
        public void Initialize()
        {
            // No op
        }
        private EventSource? eventSourceForGCHeapStatsV2;
        protected override void OnEventSourceCreated(EventSource source)
        {
            if (WantedEventCounterSources.Contains(source.Name))
            {
                
                if (source.Name.Equals("Microsoft-Windows-DotNETRuntime", StringComparison.Ordinal) == true)
                {
                    this.EnableEvents(source, EventLevel.Informational, (EventKeywords)1, new Dictionary<string, string>()
                    {
                        ["EventCounterIntervalSec"] = CounterUpdateInterval.ToString()
                    });
                    this.eventSourceForGCHeapStatsV2 = source;
                } 
                else
                {
                    this.EnableEvents(source, EventLevel.Informational, (EventKeywords)1, new Dictionary<string, string>()
                    {
                        ["EventCounterIntervalSec"] = CounterUpdateInterval.ToString()
                    });
                }
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if(!WantedEventCounterSources.Contains(eventData.EventSource.Name))
            {
                return;
            }

            if (eventData.EventName.Equals("EventCounters"))
            {
                foreach (var eventPayload in eventData.Payload)
                {
                    if (eventPayload is IDictionary<string, object> eventPayloadDict)
                    {
                        var metrics = this.GetMetricsFromEventPayload(eventPayloadDict);
                        if (metrics != null)
                        {
                            this.metricWriter.WriteMetrics(metrics);
                        }
                    }
                }
            }
            else if (eventData.EventName.Contains("GCHeapStats_V2"))
            {
                if (eventData.Payload.Count >= 12 && eventData.PayloadNames.Count >= 12)
                {
                    WriteToMetricObjectToLogger(eventData.PayloadNames[1], eventData.Payload[1]); // TotalPromotedSize0
                    WriteToMetricObjectToLogger(eventData.PayloadNames[3], eventData.Payload[3]); // TotalPromotedSize1
                    WriteToMetricObjectToLogger(eventData.PayloadNames[5], eventData.Payload[5]); // TotalPromotedSize2
                    WriteToMetricObjectToLogger(eventData.PayloadNames[8], eventData.Payload[8]); // PinnedObjectCount
                    WriteToMetricObjectToLogger(eventData.PayloadNames[10], eventData.Payload[10]); // FinalizationPromotedSize
                    WriteToMetricObjectToLogger(eventData.PayloadNames[12], eventData.Payload[12]); // GCHandleCount
                }
            }
            else if (eventData != null && eventData.EventName?.Equals("GCEnd_V1", StringComparison.Ordinal) == true)
            {
                if (eventData.Payload is null || eventData.PayloadNames is null)
                {
                    return;
                }

                if (eventData.Payload.Count >= 1 && eventData.Payload[0] is uint numberOfGCs && numberOfGCs > 4 && this.eventSourceForGCHeapStatsV2 != null)
                {
                    DisableEvents(this.eventSourceForGCHeapStatsV2);
                }
            }
        }
        private void WriteToMetricObjectToLogger(string eventMetricName, object? eventMetricValue)
        {
            if (eventMetricValue is ulong)
            {
                try
                {
                    WriteToMetricValueToLogger(eventMetricName, (long)(ulong)eventMetricValue);
                }
                catch (OverflowException ex)
                {
                   //Console.WriteLine(ex.Message, $"Error converting metric value of {eventMetricName} to long");
                }
            }
            else if (eventMetricValue is uint)
            {
                WriteToMetricValueToLogger(eventMetricName, (uint)eventMetricValue);
            }
        }
        private void WriteToMetricValueToLogger(string eventMetricName, long eventMetricValue)
        {
            this.metricWriter.WriteMetrics(eventMetricName + " " + eventMetricValue);
        }

        private string GetMetricsFromEventPayload(IDictionary<string, object> eventPayload)
        {
            if (eventPayload.TryGetValue("DisplayName", out var metricName) && WantedEventCounters.Contains(metricName.ToString()))
            {
                try
                {
                    long? metricValue = null;

                    // There are two types of counter events: Sum and Mean. Mean counter events store the metric value as the "mean" of the
                    // last update interval. Sum counter events store the metric value as the "increment" from the last update interval.
                    if (eventPayload.TryGetValue("Mean", out var value))
                    {
                        metricValue = Convert.ToInt64(value);
                    }
                    else if (eventPayload.TryGetValue("Increment", out value))
                    {
                        // Divide increment from last update interval by interval duration to calculate mean.
                        metricValue = Convert.ToInt64(value) / CounterUpdateInterval;
                    }

                    if (metricValue.HasValue)
                    {
                        return metricName.ToString() + " " + metricValue.Value;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());

                    return null;
                }
            }

            return null;
        }
    }
}
