using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestHttpClientSettings
{
    public interface IMetricsWriter
    {
        /// <summary>
        /// Initializes the metrics writer.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Writes a set of metrics to the specified metrics account.
        /// </summary>
        /// <param name="metricsAccount">The metrics account.</param>
        /// <param name="metricData">the set of metrics.</param>
        void WriteMetrics(string metrics);
    }
}
