using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using System.Threading;
using System.Threading.Tasks;

namespace Hookshot.Client.Util
{
    public static class TaskUtils
    {
        // From http://stackoverflow.com/questions/13695499/proper-way-to-implement-a-never-ending-task-timers-vs-task
        public static Task RunForever(Action<CancellationToken> action, CancellationToken cancel, int period)
        {
            return Task.Run(async () =>  // <- marked async
            {
                while (true)
                {
                    action(cancel);
                    await Task.Delay(period, cancel); // <- await with cancellation
                }
            }, cancel);
        }
    }
}