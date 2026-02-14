using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crawler.Utils;
using Crawler.Core;
using System.Collections.Concurrent;


/*
The Rate Limiter is initialized inside a shared service : accessible by all workers

Each domain [google.com, microsoft.com, wikipedia.ord, ..] <- respected individually
(each domain has its own limiter)
*/
namespace Crawler.Core
{
    public class DomainRateLimiter
    {
        private readonly ConcurrentDictionary<string, RateLimiter> _limiters = new();
        private readonly int _requestsPerWindow;
        private readonly TimeSpan _timeWindow;

        public DomainRateLimiter(int requestsPerWindow, TimeSpan timeWindow)
        {
            _requestsPerWindow = requestsPerWindow;
            _timeWindow = timeWindow;
        }

        public void WaitForDomain(string url)
        {
            var domain = new Uri(url).Host;

            var limiter = _limiters.GetOrAdd(domain, _ => new RateLimiter(
                occurences : _requestsPerWindow, 
                timeUnit : _timeWindow
            ));

            limiter.WaitToProceed();
        }
    }
}