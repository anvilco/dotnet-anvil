using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Anvil;

using Xunit;

namespace AnvilTests
{
    public class RateLimiterTests
    {
        [Fact]
        public void InvalidStartingLimits_FallBackToOnePerSecond()
        {
            var limiter = new RateLimiter(0, -5);

            Assert.Equal(1, limiter.Limit);
            Assert.Equal(1000, limiter.IntervalMs);
            Assert.False(limiter.HasSetFromResponse);
        }

        [Fact]
        public async Task FirstRequest_GoesAtOnce_AndLearnsTheLimit()
        {
            var limiter = new RateLimiter(1, 1000);

            await limiter.AcquireAsync(CancellationToken.None);
            limiter.Release(10, 500);

            Assert.Equal(10, limiter.Limit);
            Assert.Equal(500, limiter.IntervalMs);
            Assert.True(limiter.HasSetFromResponse);
        }

        [Fact]
        public async Task ConcurrentRequests_WaitForTheFirstResponse()
        {
            var limiter = new RateLimiter(5, 1000);
            await limiter.AcquireAsync(CancellationToken.None);

            var second = limiter.AcquireAsync(CancellationToken.None);
            var third = limiter.AcquireAsync(CancellationToken.None);
            await Task.Delay(100);
            Assert.False(second.IsCompleted, "the second request waits for the first response");
            Assert.False(third.IsCompleted);

            limiter.Release(5, 1000);
            await Task.WhenAll(second, third).WaitAsync(TimeSpan.FromSeconds(2));
        }

        [Fact]
        public async Task AFailedFirstRequest_ReleasesTheGate()
        {
            var limiter = new RateLimiter(5, 1000);
            await limiter.AcquireAsync(CancellationToken.None);
            var second = limiter.AcquireAsync(CancellationToken.None);

            limiter.ReleaseFailed();

            await second.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(limiter.HasSetFromResponse);
        }

        [Fact]
        public async Task TheLimit_IsLearnedOnce()
        {
            var limiter = new RateLimiter(1, 1000);
            await limiter.AcquireAsync(CancellationToken.None);
            limiter.Release(10, 500);

            await limiter.AcquireAsync(CancellationToken.None);
            limiter.Release(2, 100);

            Assert.Equal(10, limiter.Limit);
            Assert.Equal(500, limiter.IntervalMs);
        }

        [Fact]
        public async Task MissingHeaders_KeepTheCurrentLimit_AndStillOpenTheGate()
        {
            var limiter = new RateLimiter(3, 1000);
            await limiter.AcquireAsync(CancellationToken.None);
            var second = limiter.AcquireAsync(CancellationToken.None);

            limiter.Release(null, null);

            await second.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(3, limiter.Limit);
            Assert.False(limiter.HasSetFromResponse);

            // A later response with the headers sets it
            limiter.Release(7, 700);
            Assert.Equal(7, limiter.Limit);
            Assert.True(limiter.HasSetFromResponse);
        }

        [Fact]
        public async Task RequestsWithinTheLimit_AreNotDelayed()
        {
            var limiter = new RateLimiter(1, 1000);
            await limiter.AcquireAsync(CancellationToken.None);
            limiter.Release(5, 1000);

            var clock = Stopwatch.StartNew();
            for (var i = 0; i < 4; i++)
            {
                await limiter.AcquireAsync(CancellationToken.None);
            }

            Assert.True(clock.ElapsedMilliseconds < 200, $"took {clock.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task RequestsOverTheLimit_WaitForTheWindow()
        {
            var limiter = new RateLimiter(1, 1000);
            await limiter.AcquireAsync(CancellationToken.None);
            limiter.Release(2, 300);

            // Two in the window (the first, just finished, counts for an interval), so the next waits
            await limiter.AcquireAsync(CancellationToken.None);
            limiter.Release(null, null);
            var clock = Stopwatch.StartNew();
            await limiter.AcquireAsync(CancellationToken.None);

            Assert.True(clock.ElapsedMilliseconds >= 250, $"took {clock.ElapsedMilliseconds}ms, expected about 300ms");
            Assert.True(clock.ElapsedMilliseconds < 1000, $"took {clock.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task TheWindow_IsNeverExceeded()
        {
            var limiter = new RateLimiter(1, 1000);
            await limiter.AcquireAsync(CancellationToken.None);
            limiter.Release(3, 200);

            // 9 requests at 3 per 200ms: at least 2 full windows after the first batch
            var sent = new System.Collections.Concurrent.ConcurrentBag<long>();
            var clock = Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, 9).Select(async _ =>
            {
                await limiter.AcquireAsync(CancellationToken.None);
                sent.Add(clock.ElapsedMilliseconds);
                await Task.Delay(10);
                limiter.Release(null, null);
            });
            await Task.WhenAll(tasks);

            var times = sent.OrderBy(t => t).ToList();
            for (var i = 0; i + 3 < times.Count; i++)
            {
                // Any 4 consecutive sends span more than one interval
                Assert.True(times[i + 3] - times[i] >= 200, $"4 requests within {times[i + 3] - times[i]}ms");
            }
        }

        [Fact]
        public async Task Waiting_CanBeCancelled()
        {
            var limiter = new RateLimiter(5, 1000);
            await limiter.AcquireAsync(CancellationToken.None);
            using var cancel = new CancellationTokenSource(50);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => limiter.AcquireAsync(cancel.Token));
        }

        [Fact]
        public void Shared_ReturnsOneLimiterPerBaseUrlAndCredentials()
        {
            var auth = "Basic " + Guid.NewGuid().ToString("N");

            var a = RateLimiter.Shared("https://app.useanvil.com", auth, 1, 1000);
            var b = RateLimiter.Shared("https://app.useanvil.com", auth, 9, 9000);
            var otherHost = RateLimiter.Shared("https://anvil.example.com", auth, 1, 1000);
            var otherKey = RateLimiter.Shared("https://app.useanvil.com", auth + "x", 1, 1000);

            Assert.Same(a, b);
            Assert.Equal(1, b.Limit);
            Assert.NotSame(a, otherHost);
            Assert.NotSame(a, otherKey);
        }
    }
}
