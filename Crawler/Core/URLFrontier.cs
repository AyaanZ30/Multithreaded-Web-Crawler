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
    public class URLFrontier
    {
        private readonly BlockingCollection<string> _queue;      // holds URLs to be processed
        private readonly ConcurrentDictionary<string, bool> _visited; // holds URLs that have already been seen
        private readonly ConcurrentDictionary<string, bool> _visitedParents;

        public URLFrontier(int? FrontierCapacity = null)
        {
            _queue = (FrontierCapacity.HasValue) ? new BlockingCollection<string>(FrontierCapacity.Value) : new BlockingCollection<string>();
            _visited = new ConcurrentDictionary<string, bool>();
            _visitedParents = new ConcurrentDictionary<string, bool>();
        }   

        public bool TryAddParent(Uri parentUri)
        {
            return _visitedParents.TryAdd(parentUri.AbsoluteUri, true);
        }
        public bool AddURL(string url)
        {
            if(_queue.IsAddingCompleted) return false;               // Additional (tight) completion check
            if(string.IsNullOrWhiteSpace(url)) return false;

            // TryAdd guarantees atomic check + insert (add URL to queue if not already seen (_visited))
            if (!_visited.TryAdd(url, true))     // DUPLICATE CHECK (only proceed if we successfully mark it as visited first.)
            {
                return false;
            }

            try
            {
                bool addedOrNot = _queue.TryAdd(url);
                return addedOrNot;
            }catch(Exception ex)
            {
                System.Console.WriteLine(ex.Message.ToString());
                return false;
            }
        }

        public bool GetURL(out string url, CancellationToken cancellationToken = default)
        {
            url = null;

            try
            {
                return _queue.TryTake(out url, Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }catch (InvalidOperationException)
            {
                return false;
            }
        }

        public void Complete()
        {
            _queue.CompleteAdding();
        }

        public IReadOnlyCollection<string> GetVisitedUrlSnapshot()
        {
            return _visited.Keys.ToList();      // <string, bool> => <key : value>
        }
        public IReadOnlyCollection<string> GetPendingUrlSnapshot()
        {
            return _queue.ToArray();            
        }

        public int PendingCount => _queue.Count;
        public int VisitedCount => _visited.Count;
        public int parentCount => _visitedParents.Count;
    }
}