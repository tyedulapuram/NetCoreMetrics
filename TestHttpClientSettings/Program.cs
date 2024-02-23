using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using System.Runtime.InteropServices;
using System.IO.Hashing;
using System.Net;
using System.Runtime.InteropServices;


namespace TestHttpClientSettings
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var eventListener = new PlatformCountersListener(new MetricsWriter());

            int numThreads = 500; // Number of threads to create
            List<Task> tasks = new List<Task>();

            // Set the default connection limit for all ServicePoints
            ServicePointManager.DefaultConnectionLimit = 10;

            for (int i = 0; i < numThreads; i++)
            {
                string url = "https://jsonplaceholder.typicode.com/posts/1";
                ServicePoint servicePoint = ServicePointManager.FindServicePoint(new Uri(url));
                servicePoint.ConnectionLimit = 15;
                int threadId = i;
                Task task = Task.Run(async () =>
                {
                    await MakeHttpRequestAsync(threadId);
                });
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
            Console.WriteLine("All requests completed.");
        }

        static async Task MakeHttpRequestAsync(int threadId)
        {
            // Create a custom HttpClientHandler with its own connection limit
            HttpClientHandler handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = 5 // Set the connection limit for this handler
            };

            using (HttpClient httpClient = new HttpClient(handler))
            {
                string url = "https://jsonplaceholder.typicode.com/posts/1"; // Example API endpoint
                try
                {
                    HttpResponseMessage response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    if(response.StatusCode == HttpStatusCode.OK)
                    {
                        for(int i=0;i<60;i++)
                        {
                            //Console.WriteLine(i);
                        }
                    }
                    string content = await response.Content.ReadAsStringAsync();
                    //Console.WriteLine($"Thread {threadId}: Request succeeded. Response: {content}");
                    //Thread.Sleep(30000);
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"Thread {threadId}: Request failed. Exception: {ex.Message}");
                }
            }
        }
        private static uint GetHashCodeOfGuid(Guid guid)
        {
            Span<byte> hashedBytes = stackalloc byte[4];
            if (!XxHash32.TryHash(guid.ToByteArray(), hashedBytes, out _))
            {
                throw new InvalidOperationException("Cannot compute xxHash32");
            }

            return MemoryMarshal.Cast<byte, uint>(hashedBytes)[0];
        }

        public static int GetHashCode(byte[] b)
        {
            int _a;
            short _b;
            short _c;
            byte _d;
            byte _e;
            byte _f;
            byte _g;
            byte _h;
            byte _i;
            byte _j;
            byte _k;

            _a = (b[3] << 24) | (b[2] << 16) | (b[1] << 8) | b[0];
            _b = (short)((b[5] << 8) | b[4]);
            _c = (short)((b[7] << 8) | b[6]);
            _d = b[8];
            _e = b[9];
            _f = b[10];
            _g = b[11];
            _h = b[12];
            _i = b[13];
            _j = b[14];
            _k = b[15];
            return _a ^ ((_b << 16) | (ushort)_c) ^ ((_f << 24) | _k);
        }
    }
}