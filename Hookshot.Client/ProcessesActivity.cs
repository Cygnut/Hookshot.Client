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
using Android.Util;

using System.Threading;
using System.Threading.Tasks;

namespace Hookshot.Client
{
    using Util;
    using Api;

    [Activity(Label = "Processes", Icon = "@drawable/icon")]
    public class ProcessesActivity : Activity
    {
        static readonly string TAG = "ProcessesActivity";
        static readonly int REFRESH_PERIOD = 5 * 1000;
        
        ListView ProcessesListView;
        TwoLineListAdapter Adapter => (TwoLineListAdapter)ProcessesListView.Adapter;

        string Name;
        string Address;

        ApiClient Api;
        CancellationTokenSource Canceller = new CancellationTokenSource();
        Task UpdateTask;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Name = Intent.Extras.GetString("name");
            Address = Intent.Extras.GetString("address");

            Title = $"{Name} ({Address})";

            SetContentView(Resource.Layout.Processes);

            ProcessesListView = FindViewById<ListView>(Resource.Id.ProcessesListView);
            ProcessesListView.Adapter = new TwoLineListAdapter(this);

            Api = new ApiClient(Address);
            
            UpdateTask = CreateUpdateTask();

            // Context menu setup.
            RegisterForContextMenu(ProcessesListView);

            ListViewMenuItems.AddRange(new ListViewMenuItem[]
            {
                new ListViewMenuItem
                {
                    Title = "Close",
                    Action = item => KillProcess(item)
                },
                new ListViewMenuItem
                {
                    Title = "Volume",
                    Action = item => Volume(item)
                }
            });
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            Canceller.Cancel();
        }

        Task CreateUpdateTask()
        {
            try
            {
                return TaskUtils.RunForever(c =>
                {
                    Api
                        .Call<Api.Responses.Processes>(new Api.Requests.Processes(), Canceller.Token)
                        .ContinueWith(t =>
                        {
                            if (t.IsCanceled)
                                return new TwoLineListItem[0];

                            if (t.IsFaulted)
                                throw t.Exception;

                            return t.Result.processes
                                .Select(p => new TwoLineListItem
                                {
                                    Line1 = $"{p.imageName} ({p.pid})",
                                    Line2 = $"Memory: {UIConversion.FromBytes(p.memUsage)}, CPU: {TimeSpan.FromMilliseconds(p.cpuTime)}",
                                    Tag = p
                                })
                                .OrderBy(i => i.Line1)
                                .ToArray();
                        })
                        .ContinueWith(t =>
                        {
                            RunOnUiThread(() =>
                            {
                                if (t.IsFaulted)
                                {
                                    Log.Error(TAG, $"Failed to fetch processes with error {t.Exception.Flatten().InnerException}.");
                                    Toast.MakeText(this, $"Failed to fetch processes.", ToastLength.Short).Show();
                                    return;
                                }

                                Populate(t.Result);
                            });
                        });
                },
                Canceller.Token,
                REFRESH_PERIOD);
            }
            catch (Exception e)
            {
                Toast.MakeText(this, $"Failed to connect to {Title}", ToastLength.Short).Show();
                Log.Error(TAG, $"Failed to start activity with error {e}.");
                Finish();
                return null;
            }
        }
        
        void Populate(TwoLineListItem[] items)
        {
            try
            {
                // We have been give a full snapshot. All items given should be placed into the view.
                // Instead of trying to messily located existing items and update them, remove items not in
                // the snapshot dot dot dot, we just set the collection to our snapshot. This seems to
                // be pretty performant.
                Adapter.Items = items.ToList();
                Adapter.Items.Sort((x, y) => string.Compare(x.Line1, y.Line1));
                Adapter.NotifyDataSetChanged();
            }
            catch (Exception) { }
        }

        void KillProcess(TwoLineListItem item)
        {
            var p = (Api.Responses.Processes.Process)item.Tag;
            ShowAlert($"Close {p.imageName}?", () => 
            {
                Api
                    .Call<Api.Responses.KillProcess>(new Api.Requests.KillProcess(p.pid), Canceller.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsCanceled) return;

                        bool success = true;
                        
                        if (t.IsFaulted)
                        {
                            success = false;
                            Log.Error(TAG, $"Failed to kill process with pid {p.pid}.");
                        }

                        if (!t.Result.result)
                        {
                            success = false;
                            Log.Error(TAG, $"Failed to kill process with pid {p.pid}, error {t.Result.error}.");
                        }
                        
                        RunOnUiThread(() =>
                        {
                            if (!success)
                                Toast.MakeText(this, $"Failed to close {p.imageName}.", ToastLength.Short).Show();
                        });
                    });
            });
        }

        float Legalise(float value, float min, float max)
        {
            return Math.Min(Math.Max(value, min), max);
        }

        float Rebase(int value, int oldMin, int oldMax, float newMin, float newMax)
        {
            var prop = (value - oldMin) / (float)(oldMax - oldMin);
            var newValue = newMin + prop * (newMax - newMin);
            // Finally, ensure it is in the new range.
            return Legalise(newValue, newMin, newMax);
        }
        
        void HandleAsyncTask(Task task, string taskName, string successText, string failureText)
        {
            task.ContinueWith(t =>
            {
                string toastText = t.IsFaulted ? failureText : successText;
                if (t.IsFaulted)
                    Log.Error(TAG, $"{taskName} failed with error {t.Exception.Flatten().InnerException}");

                RunOnUiThread(() =>
                {
                    if (!string.IsNullOrEmpty(toastText))
                        Toast.MakeText(this, toastText, ToastLength.Short).Show();
                });
            });
        }

        void Volume(TwoLineListItem item)
        {
            var p = (Api.Responses.Processes.Process)item.Tag;
            var view = LayoutInflater
                .From(this)
                .Inflate(Resource.Layout.VolumeDialog, null);
            var builder = new AlertDialog.Builder(this)
                .SetView(view);

            var volumeSeek = view.FindViewById<SeekBar>(Resource.Id.VolumeSeek);

            volumeSeek.Max = 1000;
            volumeSeek.Progress = volumeSeek.Max / 2;

            builder
                .SetCancelable(true)
                .SetPositiveButton("OK", (sender, e) =>
                {
                    try
                    {
                        var volume = Rebase(volumeSeek.Progress, 0, volumeSeek.Max, 0, 1);

                        HandleAsyncTask(
                            Api.Call<Api.Responses.SetAppVolume>(
                                new Api.Requests.SetAppVolume($"/{p.pid}", volume, null), Canceller.Token),
                            "Volume",
                            null,
                            $"Failed to set volume for {p.imageName} on {Name}"
                            );
                    }
                    catch (Exception er)
                    {
                        Log.Error(TAG, $"Failed to set volume for {p.imageName} on {Name} with error {er}");
                        Toast.MakeText(this, $"Failed to set volume for {p.imageName} on {Name}.", ToastLength.Short).Show();
                    }
                })
                .SetNegativeButton("Cancel", (sender, e) => { })
                .Create().
                Show();
        }

        void ShowAlert(string title, Action action)
        {
            new AlertDialog.Builder(this)
                .SetTitle(title)
                .SetCancelable(true)
                .SetPositiveButton("OK", (sender, e) =>
                {
                    action();
                })
                .SetNegativeButton("Cancel", (sender, e) => { })
                .Create()
                .Show();
        }

        class ListViewMenuItem
        {
            public string Title { get; set; }
            public Action<TwoLineListItem> Action;
        }

        List<ListViewMenuItem> ListViewMenuItems = new List<ListViewMenuItem>();

        TwoLineListItem GetListViewItem(IContextMenuContextMenuInfo menuInfo)
        {
            var info = menuInfo as AdapterView.AdapterContextMenuInfo;
            if (info == null) return null;

            return Adapter.GetItem(info.Position) as TwoLineListItem;
        }

        public override void OnCreateContextMenu(IContextMenu menu, View v, IContextMenuContextMenuInfo menuInfo)
        {
            var item = GetListViewItem(menuInfo);
            if (item == null) return;

            menu.SetHeaderTitle(item.Line1);
            for (int i = 0; i < ListViewMenuItems.Count(); ++i)
                menu.Add(Menu.None, i, i, ListViewMenuItems[i].Title);
        }

        public override bool OnContextItemSelected(IMenuItem menu)
        {
            var item = GetListViewItem(menu.MenuInfo);
            if (item == null) return false;

            var menuItem = ListViewMenuItems.ElementAtOrDefault(menu.ItemId);
            if (menuItem == null) return false;

            menuItem.Action(item);
            return true;
        }

    }
}