using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Net.Http;
using HtmlAgilityPack;
using System.Reflection.Metadata;

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
        private readonly Action _onStart; 
        private readonly Action _onStop; 

        // HTTP client constructoe to init client instance
        private readonly HttpClient _httpClient = new HttpClient();

        public CrawlerWorker(int workerId, URLFrontier urlFrontier, Action onStart, Action onStop, int maxVisited)
        {
            _workerId = workerId;
            _frontier = urlFrontier;
            _maxVisited = maxVisited;
            _onStart = onStart;
            _onStop = onStop;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"Worker {_workerId} started");
            _onStart?.Invoke(); 

            while (!cancellationToken.IsCancellationRequested)
            {
                if(_frontier.VisitedCount >= _maxVisited)
                {
                    break;
                }
                // try to assign url to current worker 
                if(!_frontier.GetURL(out var item, cancellationToken))
                {
                    break;  
                }   

                string currentUrl = item.childUrl;
                int depth = item.Depth;

                // Worker is active (+1) to process URL asynchronously
                try
                {
                    await ProcessUrlAsync(currentUrl, depth, cancellationToken);
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Worker {_workerId} error on {currentUrl}: {ex.Message}");    
                }
            }
            _onStop?.Invoke();
            Console.WriteLine($"Worker {_workerId} exiting");
        }

        // Each worker calls the below method independently (parentURL : PARENT)
        private async Task ProcessUrlAsync(string currentURL, int depth, CancellationToken token)
        {
            Console.WriteLine($"[START] Worker {_workerId} -> [{currentURL}]");

            var htmlString = await FetchAsync(currentURL, token);
            if (string.IsNullOrEmpty(htmlString)) return;

            Uri baseUri = new Uri(currentURL);

            var extractedLinks = ExtractLinks(htmlString, baseUri).ToList();
            Console.WriteLine($"[PARSED] Worker {_workerId} -> {currentURL} (links = {extractedLinks.Count})");

            EnqueueLinks(extractedLinks, currentURL, depth);

            Console.WriteLine($"[DONE] Worker {_workerId} -> [{currentURL}]");
        }

        private void EnqueueLinks(IEnumerable<string> links, string parentUrl, int parentDepth)
        {
            foreach(var link in links)
            {
                var currentUrl = link;
                _frontier.AddURL(currentUrl, parentUrl, parentDepth);
            }
        }

        private async Task<string> FetchAsync(string url, CancellationToken token)
        {
            try
            {
                string fetchResult = await _httpClient.GetStringAsync(url, token);
                return fetchResult;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error in fetching data : {ex.Message}");
                return "";
            }
        }

        private IEnumerable<string> ExtractLinks(string htmlContent, Uri baseUri)
        {
            var document = new HtmlDocument();
            document.LoadHtml(htmlContent);

            var links = document.DocumentNode.SelectNodes("//a[@href]");
            if(links == null) yield break; 

            // Concurrent Oppurtunistic Traversal (neither BFS nor DFS)
            foreach(var link in links)
            {
                var href = link.GetAttributeValue("href", null);
                
                if(string.IsNullOrWhiteSpace(href)) continue;
                if(!TryNormalize(baseUri, href, out var absoluteUrl)) continue;
                if(baseUri.Host != new Uri(absoluteUrl).Host) continue;

                yield return absoluteUrl;
            } 
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