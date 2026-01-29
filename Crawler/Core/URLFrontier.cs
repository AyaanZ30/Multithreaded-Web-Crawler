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
        public string childUrl {get;}
        public string parentUrl {get;}
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
        private readonly BlockingCollection<FrontierItem> _queue;      // holds URLs to be processed
        private readonly ConcurrentDictionary<string, bool> _visited; // holds URLs that have already been seen
        private readonly ConcurrentDictionary<string, bool> _visitedParents;
        private readonly int _maxDepth;

        public URLFrontier(int maxDepth, int? capacity = null)
        {
            _maxDepth = maxDepth;
            _queue = capacity.HasValue ? new BlockingCollection<FrontierItem>(capacity.Value) : new BlockingCollection<FrontierItem>();
            _visited = new ConcurrentDictionary<string, bool>();
            _visitedParents = new ConcurrentDictionary<string, bool>();
        }   

        public void AddSeed(string url)
        {
            _visited.TryAdd(url, true);
            _queue.TryAdd(new FrontierItem(Url : url, ParentUrl : null, depth : 0));
        }

        public bool TryAddParent(Uri parentUri)
        {
            return _visitedParents.TryAdd(parentUri.AbsoluteUri, true);
        }
        public bool AddURL(string childUrl, string parentUrl, int parentDepth)
        {
            if(_queue.IsAddingCompleted) return false;               // Additional (tight) completion check
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
                return _queue.TryAdd(item);
            }catch(Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                return false;
            }
        }

        public bool GetURL(out FrontierItem item, CancellationToken cancellationToken = default)
        {
            try
            {
                // item : childUrl + parentUrl + depth
                return _queue.TryTake(out item, Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                item = null;
                return false;
            }
        }

        public void Complete()
        {
            _queue.CompleteAdding();
        }

        public int PendingCount => _queue.Count;
        public int VisitedCount => _visited.Count;
        public int parentCount => _visitedParents.Count;
    }
}