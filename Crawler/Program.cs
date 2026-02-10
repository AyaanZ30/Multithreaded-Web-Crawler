using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Crawler.Basics.Threading;
using Crawler.Core;


/*
In a crawler => Every WORKER : PRODUCER (by discovering new links) + CONSUMER (shrinks the size of shared queue after extracting relevant info from URL)

WORKER flow : take URL (shared queue) -> fetch page -> parse HTML -> extract data -> extract links (deeper URLs)

URL Frontier (Shared Queue) : THREAD-SAFE (to avoid duplication in working operations or crashing[INFINITE LOOPING])

BlockingCollection<T> or ConcurrentQueue<T>
ConcurrentDictionary<URL(string), True/False(boolean)> (to keep track of added URLs)
ConcurrentDictionary<string, data> [data : scraped results]
Interlocked / CancellationToken / volatile flags (TERMINATE CRAWL)

Worker local variables → NOT shared (to avoid race conditions)

Worker loop:
    while Q not empty and not stopped
        URL <- URL.next
        process(URL)

STOP CRAWL if:
    totalPagesCrawled >= MAX_PAGES
    OR TimeElapsed >= MAX_TIME
    OR frontier is EMPTY AND [w] in workers IDLE (all workers are idle)


Worker lifecycle:

START
  ↓
Try to take URL from frontier
  ↓
If no URL available:
    - wait (don’t spin!)
  ↓
Process URL
  ↓
Possibly add new URLs
  ↓
Check termination condition
  ↓
Repeat or EXIT



Shallower URLs almost always matter more (depth 2 URLs > depth 5 URLs) [HEURISTIC BASED]
-> priority += (maxCrawlDepth - currentDepth) * 10
-> same domain boost (+ 30 as Internal links > external links)
-> Keyword boost (+20 per matched keyword (blog, docs, research, careers, etc))
*/
namespace Crawler
{
    
    class Program
    {
        static int activeWorkers = 0;
        static async Task Main(string[] args)
        {
            int maxVisitedUrls = 100;
            int maxCrawlDepth = 5;

            URLFrontier queue = new URLFrontier(maxDepth : maxCrawlDepth);
            queue.AddSeed(url : "https://www.anthropic.com/");
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            CancellationToken token = cts.Token;

            int n_workers = 5;
            var workers = new List<Task>();

            for(int i = 1 ; i <= n_workers ; i++)
            {
                int workerId = i;
                var worker = new CrawlerWorker(
                    workerId, 
                    queue,
                    () => Interlocked.Increment(ref activeWorkers),
                    () => Interlocked.Decrement(ref activeWorkers),
                    maxVisitedUrls
                );
    
                workers.Add(Task.Run(() => worker.RunAsync(token)));  
            }

            await Task.WhenAll(workers);

            System.Console.WriteLine("=== Crawler Stopped ===");

            System.Console.WriteLine($"VISITED URLs : {queue.VisitedCount}\n");
            System.Console.WriteLine($"PENDING URLs : {queue.PendingCount}\n");
        }
    }
}