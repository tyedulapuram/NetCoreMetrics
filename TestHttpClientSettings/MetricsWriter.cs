using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestHttpClientSettings
{
    public class MetricsWriter : IMetricsWriter
    {
        public void Initialize()
        {
            // No op
        }
        public void WriteMetrics(string message)
        {
            Console.WriteLine(message);
        }
    }
}
