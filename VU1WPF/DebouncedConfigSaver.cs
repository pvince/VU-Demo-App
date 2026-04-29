using System;
using System.Threading;
using System.Threading.Tasks;

namespace KR_VU1_ConfigurationManager
{
    public sealed class DebouncedConfigSaver
    {
        private readonly Func<Task> _saveAction;
        private readonly TimeSpan _debounceDelay;
        private readonly Action<Exception>? _onError;
        private readonly object _sync = new object();

        private CancellationTokenSource? _saveCts;
        private Task _lastScheduledSave = Task.CompletedTask;

        public DebouncedConfigSaver(Func<Task> saveAction, TimeSpan debounceDelay, Action<Exception>? onError = null)
        {
            _saveAction = saveAction ?? throw new ArgumentNullException(nameof(saveAction));
            _debounceDelay = debounceDelay;
            _onError = onError;
        }

        public void RequestSave()
        {
            lock (_sync)
            {
                _saveCts?.Cancel();
                _saveCts?.Dispose();

                _saveCts = new CancellationTokenSource();
                CancellationToken token = _saveCts.Token;
                _lastScheduledSave = RunSaveAsync(token);
            }
        }

        public Task FlushAsync()
        {
            lock (_sync)
            {
                return _lastScheduledSave;
            }
        }

        private async Task RunSaveAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(_debounceDelay, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                await _saveAction().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // A newer save request superseded this one.
            }
            catch (Exception ex)
            {
                _onError?.Invoke(ex);
            }
        }
    }
}
