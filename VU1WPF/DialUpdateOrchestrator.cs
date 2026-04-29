using System;
using System.Threading;
using System.Threading.Tasks;

namespace VU1WPF
{
    public sealed class DialUpdateOrchestrator
    {
        private int _isRunning;

        public bool TryRun(Func<Task> updateOperation)
        {
            if (updateOperation == null)
            {
                throw new ArgumentNullException(nameof(updateOperation));
            }

            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            {
                return false;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await updateOperation().ConfigureAwait(false);
                }
                finally
                {
                    Volatile.Write(ref _isRunning, 0);
                }
            });

            return true;
        }
    }
}