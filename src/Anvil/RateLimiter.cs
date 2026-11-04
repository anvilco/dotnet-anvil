using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Anvil
{
    /// <summary>The rate limit an <see cref="AnvilClient"/> is pacing requests to.</summary>
    public sealed class RateLimitInfo
    {
        internal RateLimitInfo(int limit, int intervalMs, bool learnedFromResponse)
        {
            Limit = limit;
            IntervalMs = intervalMs;
            LearnedFromResponse = learnedFromResponse;
        }

        /// <summary>Requests allowed per <see cref="IntervalMs"/>.</summary>
        public int Limit { get; }

        /// <summary>The interval of <see cref="Limit"/>, in milliseconds.</summary>
        public int IntervalMs { get; }

        /// <summary>
        /// Whether the limit came from the API's <c>X-RateLimit-Limit</c> and
        /// <c>X-RateLimit-Interval-Ms</c> headers, rather than the starting
        /// <see cref="AnvilClientOptions.RequestLimit"/>.
        /// </summary>
        public bool LearnedFromResponse { get; }
    }

    /// <summary>Raised when the API answered <c>429</c> and the request is about to be retried.</summary>
    public sealed class RateLimitedEventArgs : EventArgs
    {
        internal RateLimitedEventArgs(TimeSpan retryAfter)
        {
            RetryAfter = retryAfter;
        }

        /// <summary>How long the client waits before sending the request again.</summary>
        public TimeSpan RetryAfter { get; }
    }

    /// <summary>
    /// Paces requests so the API never has to answer <c>429</c>.
    ///
    /// Counts requests over a sliding window, like the API does: a request counts
    /// from when it is sent until an interval after its response arrives, so the
    /// API, which counts from when the request reached it, never sees more in its
    /// window than the client allowed. The first request goes alone, and the rest
    /// wait for its response, which carries the API's actual limit.
    /// </summary>
    internal sealed class RateLimiter
    {
        public const string LimitHeader = "X-RateLimit-Limit";
        public const string IntervalHeader = "X-RateLimit-Interval-Ms";

        private readonly object _lock = new object();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Queue<long> _finished = new Queue<long>();
        private int _inFlight;
        private bool _setupStarted;
        private bool _setupDone;
        private readonly TaskCompletionSource<bool> _setupGate = NewSignal();
        private TaskCompletionSource<bool> _changed = NewSignal();

        public RateLimiter(int limit, int intervalMs)
        {
            if (limit <= 0 || intervalMs <= 0)
            {
                limit = Constants.DefaultRequestLimit;
                intervalMs = Constants.DefaultRequestLimitMs;
            }

            Limit = limit;
            IntervalMs = intervalMs;
        }

        public int Limit { get; private set; }

        public int IntervalMs { get; private set; }

        public bool HasSetFromResponse { get; private set; }

        public RateLimitInfo Info
        {
            get
            {
                lock (_lock)
                {
                    return new RateLimitInfo(Limit, IntervalMs, HasSetFromResponse);
                }
            }
        }

        private long Window => IntervalMs + Constants.RateLimitBufferMs;

        /// <summary>Waits until a request may be sent, then counts it as in flight.</summary>
        public async Task AcquireAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                Task wait;
                int delayMs;
                lock (_lock)
                {
                    if (!_setupDone)
                    {
                        if (!_setupStarted)
                        {
                            // The first request goes alone, so its response can set the limit
                            _setupStarted = true;
                            _inFlight++;
                            return;
                        }

                        wait = _setupGate.Task;
                        delayMs = Timeout.Infinite;
                    }
                    else
                    {
                        var now = _clock.ElapsedMilliseconds;
                        Prune(now);
                        if (_inFlight + _finished.Count < Limit)
                        {
                            _inFlight++;
                            return;
                        }

                        // Until the oldest finished request leaves the window, or a
                        // response arrives (which may change the limit)
                        wait = _changed.Task;
                        delayMs = _finished.Count > 0
                            ? (int)Math.Max(1, _finished.Peek() + Window - now)
                            : Timeout.Infinite;
                    }
                }

                await WaitAsync(wait, delayMs, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Records that a request got its response, and learns the API's limit from
        /// the first response that carries one.
        /// </summary>
        public void Release(int? limit, int? intervalMs)
        {
            lock (_lock)
            {
                Finish();
                if (!HasSetFromResponse && limit > 0 && intervalMs > 0)
                {
                    Limit = limit.Value;
                    IntervalMs = intervalMs.Value;
                    HasSetFromResponse = true;
                }

                Signal();
            }
        }

        /// <summary>Records that a request failed without a response.</summary>
        public void ReleaseFailed()
        {
            lock (_lock)
            {
                Finish();
                Signal();
            }
        }

        private void Finish()
        {
            _inFlight = Math.Max(0, _inFlight - 1);
            _finished.Enqueue(_clock.ElapsedMilliseconds);
        }

        private void Signal()
        {
            if (!_setupDone)
            {
                _setupDone = true;
                _setupGate.TrySetResult(true);
            }

            var changed = _changed;
            _changed = NewSignal();
            changed.TrySetResult(true);
        }

        private void Prune(long now)
        {
            while (_finished.Count > 0 && _finished.Peek() + Window <= now)
            {
                _finished.Dequeue();
            }
        }

        private static async Task WaitAsync(Task signal, int delayMs, CancellationToken cancellationToken)
        {
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                var tasks = new List<Task> { signal, cancelled.Task };
                if (delayMs != Timeout.Infinite)
                {
                    tasks.Add(Task.Delay(delayMs, cancellationToken));
                }

                try
                {
                    await Task.WhenAny(tasks).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private static TaskCompletionSource<bool> NewSignal()
            => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Every client in the process with the same base URL and credentials shares
        /// one limiter, since the API's limit is per API key. The starting limit only
        /// applies to the client that creates it.
        /// </summary>
        public static RateLimiter Shared(string baseUrl, string authorizationHeader, int limit, int intervalMs)
        {
            var key = baseUrl + "\n" + Sha256(authorizationHeader);
            lock (SharedLimiters)
            {
                if (SharedLimiters.TryGetValue(key, out var reference) && reference.TryGetTarget(out var existing))
                {
                    return existing;
                }

                var limiter = new RateLimiter(limit, intervalMs);
                SharedLimiters[key] = new WeakReference<RateLimiter>(limiter);
                return limiter;
            }
        }

        private static readonly Dictionary<string, WeakReference<RateLimiter>> SharedLimiters
            = new Dictionary<string, WeakReference<RateLimiter>>(StringComparer.Ordinal);

        private static string Sha256(string value)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
            }
        }
    }
}
