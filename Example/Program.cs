﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Urlscan;

namespace Example
{
    public static class Program
    {
        private static UrlscanClient Client;
        private static LiveClient Live;

        public static async Task Main()
        {
            Console.WriteLine("> Enter your Urlscan API Key:");
            string key = Console.ReadLine();

            Console.WriteLine("\n> Enter your Urlscan SID, or press enter to skip:");
            string sid = Console.ReadLine();
            if (string.IsNullOrEmpty(sid)) sid = null;

            Console.WriteLine();
            Client = new(key, sid);

            User user = await Client.GetCurrentUser();

            Console.WriteLine($"Current user info:");
            Console.WriteLine($"Registered: {(int)(DateTime.Now - user.CreatedAt).TotalDays} days ago");
            Console.WriteLine($"Last Submissions: {(int)(DateTime.Now - user.Stats.LastSubmission).TotalDays} days ago");
            Console.WriteLine($"Username: @{user.Username} - {user.FirstName} {user.LastName}");
            Console.WriteLine($"Email: {user.Email}");
            Console.WriteLine($"Your defualt scan visibility is {user.Preferences.DefaultVisibility}");
            Console.WriteLine(user.IsPro ? "You are a PRO subscription member" : "You are not a PRO subscription member");
            Console.WriteLine();

            Console.WriteLine($"Limits:");
            foreach (KeyValuePair<LimitType, Limit> limit in user.Limits.API)
            {
                Console.WriteLine($"[{limit.Key}] {limit.Value.Minute}/minute, {limit.Value.Hour}/hour, {limit.Value.Day}/day");
            }

            Console.WriteLine();
            Console.WriteLine($"You've made {user.Stats.Total} sumissions, of which {user.Stats.Private + user.Stats.Unlisted} were private");
            Console.WriteLine();

            Stats stats = await Client.GetStats();
            Console.WriteLine($"Currently running scan tasks: {stats.Running}");
            Console.WriteLine($"24h stats: public: {stats.Public}, unlisted: {stats.Unlisted}, private: {stats.Private}");
            Console.WriteLine($"Total: {stats.Total}\n");

            Console.Write($"\n> Enter a URL to scan: ");

            string url = Console.ReadLine();

            Console.WriteLine($"Submitting a scan");

            Submission subm;
            try
            {
                subm = await Client.Scan(new ScanParameters()
                {
                    //URL is the only mandatory argument, the rest is all optional.
                    Url = url,
                    Tags = ["test"],
                    Country = ScanCountry.FI,
                    UserAgent = "My-Custom-Scanner/1.0.0",
                    OverrideSafety = false,
                    Referer = "https://google.com",
                    Visibility = Visibility.Public
                });
            }
            catch (UrlscanException ex)
            {
                Console.WriteLine($"Received an Urlscan exception: {ex.Message}");
                if (ex.Error is not null) Console.WriteLine($"{ex.Error.Description} => {ex.Error.Message}");
                Console.ReadKey();
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Received an unknown exception while scanning: {ex.GetType().Name} => {ex.Message}");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"Submission created: {subm.UUID}\n");

            Console.WriteLine($"Waiting for the scan to finish, this will take about 10 seconds.\n");
            Result res = await Client.Poll(subm);

            Console.WriteLine($"Page was successfully scanned by a submitter from {res.Submitter.Country}.\n");

            Console.WriteLine($"Result URL: {res.Task.ReportUrl}");
            Console.WriteLine($"Screenshot URL: {res.Task.ScreenshotUrl}");
            Console.WriteLine($"DOM view URL: {res.Task.DOMUrl}\n");

            Console.WriteLine($"Final URL: {res.Page.Url} on domain {res.Page.Domain}");
            Console.WriteLine($"This page has the IP address {res.Page.IP} hosted at IP {res.Page.IP} ({res.Page.ASNName}) from {res.Page.Country}");
            Console.WriteLine($"The server software was identified as {res.Page.Server}\n");

            Console.WriteLine($"{res.Lists.Hashes.Length} hashes were generated from the resources");
            Console.WriteLine($"{res.Lists.Urls.Length} links are present on the website");
            Console.WriteLine($"{res.Lists.IPs.Length} IPs are contacted");
            Console.WriteLine($"{res.Lists.ASNs.Length} ASNs are contacted");
            Console.WriteLine($"{res.Lists.Countries.Length} countries are contacted\n");

            Console.WriteLine($"Domains contacted: {string.Join(' ', res.Lists.Domains)}");
            Console.WriteLine($"Technologies used: {string.Join(' ', res.Meta.Processors.Wappa.Data.Select(x => x.App))}\n");

            OverallVerdict over = res.Verdicts.Overall;
            Console.WriteLine($"Anti-phishing verdict: {(over.Malicious ? "Page is phishing" : "Page is safe")}");

            if (over.Malicious)
            {
                UrlscanVerdict uver = res.Verdicts.Urlscan;

                Console.WriteLine($"Urlscan phishing score: {uver.Score}");
                if (uver.Brands.Length > 0) Console.WriteLine($"Impersonated brands: {string.Join(' ', uver.Brands.Select(x => x.Name))}");
                Console.WriteLine($"Malicious: {uver.Malicious}\n");

                CommunityVerdict cver = res.Verdicts.Community;

                Console.WriteLine($"Community phishing score: {cver.Score}, categories: {string.Join(' ', uver.Categories)}");
                if (cver.Brands.Length > 0) Console.WriteLine($"Impersonated brands: {string.Join(' ', cver.Brands.Select(x => x.Name))}");
                Console.WriteLine($"Community Votes: {cver.VotesMalicious}");
                Console.WriteLine($"Malicious: {cver.Malicious}\n");
            }

            SimilarScan[] similar = await Client.GetSimilarScans("bc1ef5f2-eddc-40ae-86c9-fb5894b5d1f2");
            Console.WriteLine($"Found {similar.Length} similar scans");

            Console.WriteLine($"Done analysing URL\n");

            Console.WriteLine($"\n> Downloading screenshot (screenshot.png) and DOM (dom.html) to the current directory.");
            File.WriteAllBytes("screenshot.png", await Client.DownloadScreenshot(res));
            File.WriteAllText("dom.html", await Client.DownloadDOM(res));

            if (Client.UsesAccountSID)
            {
                Console.WriteLine("\n> Press any key to submit a verdict to a known Discord phishing site.");
                Console.ReadKey();

                await Client.AddVerdict(new VerdictParameters()
                {
                    UUID = "8964cc71-ea31-476c-ba8f-863bf4bf6b2f",
                    Comment = "Running a Discord phishing scam with Discord HypeSquad as their target.",
                    Scope = VerdictScope.PageDomain,
                    ScopeValue = "contact-hype-testers.com",
                    ThreatTypes =
                    [
                        ThreatType.Phishing,
                        ThreatType.BrandImpersonation
                    ],
                    Brands =
                    [
                        "Discord"
                    ],
                    Verdict = VerdictType.Malicious
                });

                Console.WriteLine("Successfully verdicted, see it at: https://urlscan.io/result/8964cc71-ea31-476c-ba8f-863bf4bf6b2f/#verdicts");

                Console.WriteLine("\n> Press any key to search for scans that contain hypesquad in them.");
                Console.ReadKey();

                SearchItem[] scans = await Client.Search("page.status:200 AND domain.keyword:*hypesquad*", 10);
                foreach (SearchItem scan in scans)
                {
                    Console.WriteLine(scan.Page.Url[..Math.Min(scan.Page.Url.Length, 50)]);
                }
            }
            
            Console.WriteLine("\n> Press any key to start watching for newly scanned URLs.");
            Console.ReadKey();

            Live = new(1000 * 3, 10);

            Live.UrlScanned += (sender, scan) =>
            {
                Console.WriteLine(scan.Task.Url[..Math.Min(scan.Task.Url.Length, 50)]);
            };

            Console.WriteLine("\n> Demo finished, press any key to exit.");
            Console.ReadKey();
            Live.Stop();
        }
    }
}