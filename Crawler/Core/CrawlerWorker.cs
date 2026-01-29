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
                    _frontier.Complete();
                    break;
                }
                // try to assign url to current worker 
                if(!_frontier.GetURL(out var item, cancellationToken))
                {
                    break;  
                }   

                string parent = item.parentUrl;
                string child = item.childUrl;
                int parentDepth = item.Depth;

                // Worker is active (+1) to process URL asynchronously
                try
                {
                    await ProcessUrlAsync(parent, parentDepth, cancellationToken);
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Worker {_workerId} error on {parent}: {ex.Message}");    
                }
            }
            _onStop?.Invoke();
            Console.WriteLine($"Worker {_workerId} exiting");
        }

        // Each worker calls the below method independently (parentURL : PARENT)
        private async Task ProcessUrlAsync(string parentURL, int parentDepth, CancellationToken token)
        {
            Console.WriteLine($"[START] Worker {_workerId} -> [{parentURL}]");
            string htmlString;

            try
            {
                htmlString = await _httpClient.GetStringAsync(parentURL, token);  
                Console.WriteLine($"[FETCHED] Worker {_workerId} -> [{parentURL}] ({htmlString.Length} chars)"); 
            }
            catch(Exception ex)
            {   
                Console.WriteLine($"Error in fetching data : {ex.Message} (url = {parentURL})");
                return;
            }

            var document = new HtmlDocument();
            document.LoadHtml(htmlString);

            var baseUri = new Uri(parentURL);
            if(baseUri != null) _frontier.TryAddParent(baseUri);
            
            var links = document.DocumentNode.SelectNodes("//a[@href]");
            if(links == null) return;

            int discoveredLinks = links?.Count ?? 0;
            Console.WriteLine($"[PARSED] Worker {_workerId} -> {parentURL} (links = {discoveredLinks})");

            // Concurrent Oppurtunistic Traversal (neither BFS nor DFS)
            foreach(var link in links)
            {
                if(token.IsCancellationRequested) return;

                var href = link.GetAttributeValue("href", null);
                if(String.IsNullOrWhiteSpace(href)) continue;

                if(!TryNormalize(baseUri, href, out var childURL)) continue;
                // every successsfull childURL : Child of parentURL 

                if(baseUri.Host != new Uri(childURL).Host) continue;

                _frontier.AddURL(childURL, parentURL, parentDepth);
            }

            Console.WriteLine($"[DONE] Worker {_workerId} -> [{parentURL}]");
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