using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Crawler.Basics.Threading;

public class TaskWorkerDemo
{
    public static async Task RunAsync()
    {
        System.Console.WriteLine("Starting Task Worker Demo\n");

        // multiple workers will pull work from workQueue (shared queue)
        // shared queue --> URL holder (frontier) <-- w1, w2 ....
        BlockingCollection<int> workQueue = new BlockingCollection<int>();

        // PRODUCER (add work to the shared queue)
        for(int i = 1 ; i <= 10 ; i++) workQueue.Add(i);

        // Signal that no more items will be added
        workQueue.CompleteAdding();

        int workerCount = 3;
        List<Task> workers = new List<Task>();

        for(int c = 1 ; c <= workerCount ; c++)
        {
            var workerId = c;    // value of c at the moment captured (current iteration)

            /* CONSUMERS
            A] Each worker runs independently            
            B] Pulls work when available
            C] Stops when shared queue becomes empty
            */
            workers.Add(Task.Run(async() => Worker(workerId, workQueue)));
        }

        await Task.WhenAll(workers);
        System.Console.WriteLine("All work completed.");
    }

    private static async void Worker(int workerId, BlockingCollection<int> workQueue)
    {
        foreach(var item in workQueue.GetConsumingEnumerable())
        {
            System.Console.WriteLine($"Worker {workerId} processing item {item}");
            Thread.Sleep(500);
        }

        System.Console.WriteLine($"Worker {workerId} exiting...");
    }
}