using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.Http;
using HtmlAgilityPack;

/*
Represents a single crawler worker
Continuously pulls URLs from the UrlFrontier and processes them.
*/

namespace Crawler.Core
{ 
    public class CrawlerWorker
    {   
        private readonly int _workerId;
        private readonly int _maxVisited;
        private readonly URLFrontier _frontier;
        private readonly Action incrementActive;
        private readonly Action decrementActive;

        // HTTP client constructoe to init client instance
        private readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public CrawlerWorker(int workerId, URLFrontier urlFrontier, Action incrementActive, Action decrementActive, int maxVisited)
        {
            _workerId = workerId;
            _frontier = urlFrontier;
            _maxVisited = maxVisited;
            this.incrementActive = incrementActive;
            this.decrementActive = decrementActive;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            System.Console.WriteLine($"Worker {_workerId} started");

            while (!cancellationToken.IsCancellationRequested)
            {
                if(_frontier.VisitedCount >= _maxVisited)
                {
                    _frontier.Complete();
                    break;
                }
                // try to assign url to current worker 
                if(!_frontier.GetURL(out var url, cancellationToken))
                {
                    break;  
                }   

                // Worker is active (+1) to process URL asynchronously
                incrementActive();
                try
                {
                    await ProcessUrlAsync(url, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }catch(Exception ex)
                {
                    System.Console.WriteLine($"Worker {_workerId} error on {url}: {ex.Message}");    
                }
                finally         // after processing, worker is passive (-1)
                {
                    decrementActive();
                }
            }
            System.Console.WriteLine($"Worker {_workerId} exiting");
        }

        // Each worker calls the below method independently
        private async Task ProcessUrlAsync(string url, CancellationToken token)
        {
            System.Console.WriteLine($"[START] Worker {_workerId} -> [{url}]");

            string htmlString;

            try
            {
                htmlString = await _httpClient.GetStringAsync(url, token);  
                System.Console.WriteLine($"[FETCHED] Worker {_workerId} -> [{url}] ({htmlString.Length} chars)"); 
            }
            catch(InvalidOperationException)
            {
                return;
            }

            var document = new HtmlDocument();
            document.LoadHtml(htmlString);

            var baseUri = new Uri(url);
            System.Console.WriteLine(baseUri.ToString());

            if(baseUri != null) _frontier.TryAddParent(baseUri);
            
            var links = document.DocumentNode.SelectNodes("//a[@href]");
            if(links == null) return;

            int discoveredLinks = links?.Count ?? 0;
            Console.WriteLine($"[PARSED] Worker {_workerId} -> {url} (links={discoveredLinks})");

            foreach(var link in links)
            {
                if(token.IsCancellationRequested) return;

                var href = link.GetAttributeValue("href", null);
                if(String.IsNullOrWhiteSpace(href)) continue;

                if(!TryNormalize(baseUri, href, out var absoluteUrl)) continue;

                _frontier.AddURL(absoluteUrl);
            }

            System.Console.WriteLine($"[DONE] Worker {_workerId} -> {url}");
        }

        private static bool TryNormalize(Uri baseUri, string href, out string absoluteUrl)
        {
            absoluteUrl = null;

            if (href.StartsWith("#") ||
                href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!Uri.TryCreate(baseUri, href, out var resolved))
                return false;

            if (resolved.Scheme != Uri.UriSchemeHttp &&
                resolved.Scheme != Uri.UriSchemeHttps)
                return false;

            var clean = new UriBuilder(resolved)
            {
                Fragment = ""
            };

            absoluteUrl = clean.Uri.AbsoluteUri;
            return true;
        }
    }
}