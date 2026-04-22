using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PZAutoBackUpAndRestore
{
    public class TimerService
    {
        private readonly TimeSpan _interval;
        private CancellationTokenSource _cts;
        private Action _saveGame;
        public TimerService(double minutes, Action saveGame)
        {
            _interval = TimeSpan.FromMinutes(minutes);
            _saveGame = saveGame;
        }
        //starts the background work
        public void Start()
        {
            _cts = new CancellationTokenSource();
            // Task.Run on a different thread
            Task.Run(() => RunTimer(_cts.Token));
        }

        //stops the background work
        public void Stop()
        {
            _cts?.Cancel();
        }

        private async Task RunTimer(CancellationToken token)
        {
            using var timer = new PeriodicTimer(_interval);

            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    Console.WriteLine($"Autosave completed at {DateTime.Now:T}");
                    _saveGame?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                //happens when Stop()
                Console.WriteLine("Autosave service stopped.");
            }
        }
    }
}
