using Telegram.Bot.Types.Enums;
namespace FoolMeGame.Shared.Telegram;

public static class TelegramKeepStatus
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(1);

    public static TelegramKeepStatusScope KeepStatus(this TelegramHelper telegram, ChatAction status)
    {
        return new TelegramKeepStatusScope(telegram, status);
    }

    public class TelegramKeepStatusScope : IDisposable
    {
        private readonly TelegramHelper _telegram;
        private readonly ChatAction _status;
        private readonly CancellationTokenSource _cts;

        public TelegramKeepStatusScope(TelegramHelper telegram, ChatAction status)
        {
            _telegram = telegram ?? throw new ArgumentNullException(nameof(telegram));
            _status = status;
            _cts = new CancellationTokenSource(MaxDuration);

            _ = RunAsync();
        }

        public void Dispose() => _cts.Cancel();

        private async Task RunAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await _telegram.SendStatusAsync(_status, _cts.Token);
                    await Task.Delay(RetryInterval, _cts.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    // it's ok
                }
                catch
                {
                    await _cts.CancelAsync();
                }
            }
        }
    }
}
