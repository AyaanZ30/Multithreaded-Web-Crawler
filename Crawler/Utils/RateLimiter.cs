using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

/*
The goal is to ensure that a specific action only happens N times within a time window T (to avoid overwhelming the target server) 
(example : 5 requests per second)
*/
namespace Crawler.Utils
{
    public interface IRateLimiter { void WaitToProceed(); }

    public class RateLimiter : IRateLimiter, IDisposable
    {
        private readonly SemaphoreSlim _semaphore;       // counts & limits no of occurences per unit time (1 sec)
        private readonly ConcurrentQueue<int> _exitTimes;    // CurrentTime [when a thread successfully passes the semaphore] + TimeUnit (in milliseconds)
        private readonly Timer _exitTimer;                  // Timer used to trigger exiting the semaphore

        private bool _isDisposed;                        
        public int Occurences {get; private set;}
        public int TimeUnitMilliseconds {get; private set;}

        public RateLimiter(int occurences, TimeSpan timeUnit)
        {
            if(occurences <= 0)
                throw new ArgumentOutOfRangeException("occurences", "Number of occurrences must be a positive integer");
            if(timeUnit > TimeSpan.FromMilliseconds(UInt32.MaxValue))
                throw new ArgumentOutOfRangeException("timeUnit", "Time unit must be less than 2^32 milliseconds [int-32 max]");
            if(timeUnit != timeUnit.Duration())
                throw new ArgumentOutOfRangeException("timeUnit", "Time unit must be a positive span of time");

            Occurences = occurences;
            TimeUnitMilliseconds = (int)timeUnit.TotalMilliseconds;

            // initially, semaphore starts full (max count : occurences, initial count : occurences)
            _semaphore = new SemaphoreSlim(Occurences, Occurences);
            _exitTimes = new ConcurrentQueue<int>();

            _exitTimer = new Timer(
                callback : ExitTimerCallback, 
                state : null, 
                dueTime : TimeUnitMilliseconds,
                period : -1
            );
        }   

        private void ExitTimerCallback(object state)
        {
            /*
            1] peeks at the first item in _exitTimes (the oldest request).
            2] compares the time to CurrentTime [Environment.TickCount]
            3] release the semaphore if the time has passed
            */
            int exitTime;
            var exitTimeValid = _exitTimes.TryPeek(out exitTime);

            while (exitTimeValid)
            {
                if(unchecked(exitTime - Environment.TickCount) > 0)
                {
                    break;
                }
                _semaphore.Release();
                _exitTimes.TryDequeue(out exitTime);

                exitTimeValid = _exitTimes.TryPeek(out exitTime);
            }

            var timeUntilNextCheck = exitTimeValid ? Math.Min(TimeUnitMilliseconds, Math.Max(0, (exitTime - Environment.TickCount))) : TimeUnitMilliseconds;
            _exitTimer.Change(dueTime : timeUntilNextCheck, period : -1);
        }

        public bool WaitToProceed(int millisecondsTimeout)
        {
            if(millisecondsTimeout < -1) throw new ArgumentOutOfRangeException("millisecondsTimeout");

            CheckDisposed();

            // block until we can enter the semaphore (def) or until the timeout expires
            var entered = _semaphore.Wait(millisecondsTimeout);

            if (entered)
            {
                var timeToExit = unchecked(Environment.TickCount + TimeUnitMilliseconds);
                _exitTimes.Enqueue(timeToExit);
            }

            return entered;
        }

        public bool WaitToProceed(TimeSpan timeout)
        {
            return WaitToProceed((int)timeout.TotalMilliseconds);
        }
        public void WaitToProceed()      // implementing IRateLimiter's method [WaitToProceed()]
        {
            WaitToProceed(Timeout.Infinite);
        }


        protected virtual void Dispose(bool isDisposing)
        {
            if (!_isDisposed)
            {
                if (isDisposing)
                {
                    _semaphore.Dispose();
                    _exitTimer.Dispose();

                    _isDisposed = true;         // indicate that we are disposing the operation             
                }
            }
        }

        public void Dispose()                   // implementing IDisposable's method [Dispose()]
        {
            Dispose(isDisposing : true);
            GC.SuppressFinalize(this);
        }   

        private void CheckDisposed()
        {
            if(_isDisposed) throw new ObjectDisposedException("Object is already disposed");
        }
    }
}