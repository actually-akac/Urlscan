using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;

namespace Urlscan
{
    /// <summary>
    /// A secondary API client specifically for the /json/live endpoint. This polls the API every few seconds and delivers you newly created scans through events.
    /// </summary>
    public class LiveClient
    {
        /// <summary>
        /// The base API url.
        /// </summary>
        public const string URL = "https://urlscan.io";

        private readonly HttpClientHandler HttpHandler = new()
        {
            AutomaticDecompression = DecompressionMethods.All
        };

        private readonly HttpClient Client;

        private EventHandler<LiveScan> Handler;
        public event EventHandler<LiveScan> UrlScanned
        {
            add
            {
                Handler += value;
                if (Handler.GetInvocationList().Length == 1) Start();
            }
            remove
            {
                Handler -= value;
                if (Handler is null || Handler.GetInvocationList().Length == 0) Stop();
            }
        }

        private static int Interval;
        private static int Size;

        /// <summary>
        /// Create a new instance of the client for polling live results.
        /// </summary>
        /// <param name="pollInterval">How often newly created scans should be retrieved, in milliseconds.</param>
        /// <param name="pollSize"></param>
        public LiveClient(int interval = 30000, int size = 100)
        {
            if (interval < 3000) throw new ArgumentOutOfRangeException(nameof(interval), "Poll interval has to be at least 3000 ms.");
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), "Poll size has to be at least 1.");

            Interval = interval;
            Size = size;

            Seen = new(Size);
            Client = new(HttpHandler)
            {
                DefaultRequestVersion = new Version(2, 0),
            };

            Client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
            Client.DefaultRequestHeaders.UserAgent.ParseAdd("Urlscan C# Live Client - actually-akac/Urlscan");
            Client.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            Timer = new()
            {
                Interval = Interval
            };
            Timer.Elapsed += async (o, e) => await Poll();
        }

        private readonly Timer Timer;

        /// <summary>
        /// Forbicly start polling Urlscan's live endpoint. 
        /// </summary>
        public void Start()
        {
            _ = Poll();
            Timer.Start();
        }
        /// <summary>
        /// Forcibly stop polling Urlscan's live endpoint. Polling is automatically stopped when all event listeners are removed.
        /// </summary>
        public void Stop() => Timer.Stop();

        /// <summary>
        /// A cache for already seen scans to prevent duplicates.
        /// </summary>
        private readonly List<string> Seen;

        /// <summary>
        /// A counter for failed requests in a row to stop polling in case Urlscan is having an incident.
        /// </summary>
        private int FailedRequests = 0;

        /// <summary>
        /// Poll for the next chunk of recently finished submissions. 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task Poll()
        {
            HttpResponseMessage res = await Client.Request($"{URL}/json/live?size={Size}", HttpMethod.Get);
            if (res.StatusCode != HttpStatusCode.OK)
            {
                FailedRequests++;
                if (FailedRequests >= 5)
                {
                    Stop();
                    throw new Exception($"Urlscan is likely having some issues right now, disabling Live polling.");
                }
            }
            else FailedRequests = 0;

            LiveContainer cont = await res.Deseralize<LiveContainer>();

            LiveScan[] unseen = cont.Results.Where(x => !Seen.Contains(x.Task.UUID)).ToArray();

            foreach (LiveScan scan in unseen)
            {
                string uuid = scan.Task.UUID;
                Seen.Add(uuid);

                OnScan(scan);
            }

            List<string> toRemove = new();

            foreach (string uuid in Seen)
            {
                if (cont.Results.All(x => x.Task.UUID != uuid)) toRemove.Add(uuid);
            };

            foreach (string uuid in toRemove) Seen.Remove(uuid);
        }

        public void OnScan(LiveScan scan)
        {
            Handler.Invoke(this, scan);
        }
    }
}
