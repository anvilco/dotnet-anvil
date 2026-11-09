#if NETFRAMEWORK
using System;
using System.Threading.Tasks;

namespace AnvilTests
{
    // .NET Framework has no Task.WaitAsync; the tests use it to fail fast
    // instead of hanging when a request or the rate limiter never completes.
    internal static class NetFrameworkShims
    {
        public static async Task WaitAsync(this Task task, TimeSpan timeout)
        {
            if (await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false) != task)
            {
                throw new TimeoutException($"The task didn't complete within {timeout}.");
            }

            await task.ConfigureAwait(false);
        }

        public static async Task<T> WaitAsync<T>(this Task<T> task, TimeSpan timeout)
        {
            await ((Task)task).WaitAsync(timeout).ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }
    }
}
#endif
