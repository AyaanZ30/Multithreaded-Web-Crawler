using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

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

        private async Task ProcessUrlAsync(string url, CancellationToken token)
        {
            System.Console.WriteLine($"Worker {_workerId} processing URL : {url}");

            // Simulation network + parsing delay
            await Task.Delay(500, token);

            // Simulation of discovering a new url
            if(url.Length < 40)
            {
                string childUrl1 = url + "/a";
                string childUrl2 = url + "/b";

                _frontier.AddURL(childUrl1);
                _frontier.AddURL(childUrl2);
            }
        }
    }
}