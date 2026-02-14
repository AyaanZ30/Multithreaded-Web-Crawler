using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
/*

PURPOSE : Thread-safe URL frontier (shared queue) for URL crawler (stores URLs)

RESPONSIBILITIES : 

1) Store URLs waiting to be crawled
2) Ensure each URL is processed atmost 1 time
3) Block workers when no URLs are available
4) Clean shutdown

*/
namespace Crawler.Core
{
    public sealed class FrontierItem
    {
        public string childUrl {get;}          // current Url to crawl
        public string parentUrl {get;}         // where childUrl was found
        public int Depth {get;}
        public FrontierItem(string Url, string ParentUrl, int depth)
        {
            childUrl = Url;
            parentUrl = ParentUrl;
            Depth = depth;
        }
    }
    public class URLFrontier
    {
        private readonly PriorityQueue<FrontierItem, int> _queue;
        private readonly SemaphoreSlim _itemsAvailable;              // limits number of threads that can access a resource pool concurrently
        private readonly object _lock = new();
        private readonly ConcurrentDictionary<string, bool> _visited; // holds URLs that have already been seen
        private readonly int _maxDepth;

        public URLFrontier(int maxDepth)
        {
            _maxDepth = maxDepth;
            _queue = new PriorityQueue<FrontierItem, int>();
            _itemsAvailable = new SemaphoreSlim(initialCount : 0);
            _visited = new ConcurrentDictionary<string, bool>();
        }   

        public void AddSeed(string url)
        {
            _visited.TryAdd(url, true);
            var item = new FrontierItem(url, null, 0);
            var priority = Priority(item);

            lock (_lock)
            {
                _queue.Enqueue(item, priority);
            }
            _itemsAvailable.Release();
        }

        public bool AddURL(string childUrl, string parentUrl, int parentDepth)
        {
            if(string.IsNullOrWhiteSpace(childUrl)) return false;

            int childDepth = parentDepth + 1;
            if(_maxDepth < childDepth) return false;

            // TryAdd guarantees atomic check + insert (add URL to queue if not already seen (_visited))
            if (!_visited.TryAdd(childUrl, true))     // DUPLICATE CHECK (only proceed if we successfully mark it as visited first.)
            {
                return false;
            }

            try
            {
                FrontierItem item = new FrontierItem(childUrl, parentUrl, childDepth);
                int priority = Priority(item);

                lock (_lock)
                {
                    _queue.Enqueue(item, priority);
                }
                _itemsAvailable.Release();
                return true;
            }catch(Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                return false;
            }
        }

        public bool GetURL(out FrontierItem item, CancellationToken cancellationToken = default)
        {
            item = null;

            try
            {
                _itemsAvailable.Wait(cancellationToken);

                lock (_lock)
                {
                    if(_queue.Count == 0) return false;
                    item = _queue.Dequeue();
                    return true;
                }
                
            }catch(Exception e)
            {
                System.Console.WriteLine(e.Message);
                return false;
            }
        }

        private int Priority(FrontierItem item)
        {
            int score = 0;

            score += (_maxDepth - item.Depth)*10;

            string[] keywords = {"blog", "research", "careers", "docs", "api"};

            string path = item.childUrl.ToLowerInvariant();
            foreach(var k in keywords)
            {
                if (path.Contains(k))
                {
                    score += 20;
                }
            }

            if(path.EndsWith(".pdf")) score -= 20;
            else if(path.EndsWith(".zip")) score -= 50;
            else score += 10;

            return (score * (-1));
        }

        public int PendingCount => _queue.Count;
        public int VisitedCount => _visited.Count;
    }
}