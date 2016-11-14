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
using Android.Graphics;
using Android.Util;

using System.Threading;
using System.Threading.Tasks;

namespace Hookshot.Client
{
    using Util;
    using Api;

    [Activity(Label = "Server", Icon = "@drawable/icon")]
    public class ServerActivity : Activity
    {
        static readonly string TAG = "ServerActivity";

        ListView ServerListView;
        ArrayAdapter<ListViewItem> Adapter => (ArrayAdapter<ListViewItem>)ServerListView.Adapter;

        CancellationTokenSource Canceller = new CancellationTokenSource();
        ApiClient Client;

        int Id;
        string Name;
        string Address;

        class ListViewItem
        {
            public string Title { get; set; }
            public Action Action { get; set; }

            public ListViewItem(string title, Action action)
            {
                Title = title;
                Action = action;
            }

            public override string ToString()
            {
                return Title;
            }
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Id = Intent.Extras.GetInt("id");
            Name = Intent.Extras.GetString("name");
            Address = Intent.Extras.GetString("address");
            
            Title = $"{Name} ({Address})";

            SetContentView(Resource.Layout.Server);

            try
            {
                Client = new ApiClient(Address);

                ServerListView = FindViewById<ListView>(Resource.Id.ServerListView);
                ServerListView.Adapter = new ArrayAdapter<ListViewItem>(this, Android.Resource.Layout.SimpleListItem1, new List<ListViewItem>());
                AddListViewItem("Screen", () => StartActivity(BaseIntent<ServerScreenActivity>()));
                AddListViewItem("OS", () => {
                    StartActivity(
                        BaseIntent<ServerInfoActivity>()
                            .PutExtra("source", typeof(Api.Responses.Os).Name)
                            );
                });
                AddListViewItem("Service", () =>
                {
                    var intent = BaseIntent<ServerInfoActivity>()
                        .PutExtra("source", typeof(Api.Responses.Service).Name);
                    StartActivity(intent);
                });
                AddListViewItem("Processes", () => StartActivity(BaseIntent<ProcessesActivity>()));
                AddListViewItem("File Browser", () => StartActivity(BaseIntent<FileBrowserActivity>()));
                AddListViewItem("Performance", () => StartActivity(BaseIntent<PerformanceActivity>()));
                AddListViewItem("System Volume", () => SystemVolume());
                AddListViewItem("Beep", () => {
                    HandleAsyncTask(Client.Call<Api.Responses.Beep>(new Api.Requests.Beep(null, null), Canceller.Token), "Beep", null, $"Failed to beep on {Name}");
                });
                AddListViewItem("Speak", () => Speak());
                AddListViewItem("CD Drive", () => CdDrive());
                AddListViewItem("Monitor", () => Monitor());

                AddListViewItem("Hibernate", () => {
                    ShowAlert($"Hibernate {Name}?", () =>
                        HandleAsyncTask(Client.Call<Api.Responses.Sleep>(new Api.Requests.Sleep(), Canceller.Token), "Hibernate", $"Hibernated {Name}.", $"Failed to hibernate {Name}.")
                        );
                });
                AddListViewItem("Turn Off", () => {
                    ShowAlert($"Turn {Name} off?", () => 
                        HandleAsyncTask(Client.Call<Api.Responses.PowerOff>(new Api.Requests.PowerOff(), Canceller.Token), "Turn Off", $"Turned off {Name}.", $"Failed to turn off {Name}.")
                        );
                });
                
                ServerListView.ItemClick += (object sender, AdapterView.ItemClickEventArgs e) =>
                {
                    var item = Adapter.GetItem(e.Position);

                    try
                    {
                        item.Action();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(TAG, $"Failed to handle action of name {item.Title} with error {ex}.");
                    }
                };                
            }
            catch (Exception e)
            {
                Toast.MakeText(this, $"Failed to connect to {Title}", ToastLength.Short).Show();
                Log.Error(TAG, $"Failed to start activity with error {e}.");
                Finish();
            }
        }

        Intent BaseIntent<TActivity>()
            => new Intent(this, typeof(TActivity))
                .PutExtra("id", Id)
                .PutExtra("name", Name)
                .PutExtra("address", Address);

        void AddListViewItem(string title, Action action)
        {
            Adapter.Add(new ListViewItem(title, action));
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
        
        int Legalise(int value, int min, int max)
        {
            return Math.Min(Math.Max(value, min), max);
        }

        int Rebase(int value, int oldMin, int oldMax, int newMin, int newMax)
        {
            var prop = (value - oldMin) / (float)(oldMax - oldMin);
            var newValue = (int)(newMin + prop * (newMax - newMin));
            // Finally, ensure it is in the new range.
            return Legalise(newValue, newMin, newMax);
        }

        void Speak()
        {
            var view = LayoutInflater
                .From(this)
                .Inflate(Resource.Layout.SpeakDialog, null);
            var builder = new AlertDialog.Builder(this)
                .SetView(view);

            var textEdit = view.FindViewById<EditText>(Resource.Id.SpeakEditText);
            var rateSeek = view.FindViewById<SeekBar>(Resource.Id.SpeakRateSeek);
            var volumeSeek = view.FindViewById<SeekBar>(Resource.Id.SpeakVolumeSeek);

            rateSeek.Max = volumeSeek.Max = 100;
            rateSeek.Progress = volumeSeek.Progress = 50;
            
            builder
                .SetCancelable(true)
                .SetPositiveButton("OK", (sender, e) =>
                {
                    try
                    {
                        var text = textEdit.Text;
                        var rate = Rebase(rateSeek.Progress, 0, 100, -10, 10);   // to -10 to 10
                        var volume = Rebase(volumeSeek.Progress, 0, 100, 0, 100);   // to 0 to 100

                        HandleAsyncTask(
                            Client.Call<Api.Responses.Speak>(new Api.Requests.Speak(text, rate, volume), Canceller.Token),
                            "Speak",
                            null,
                            $"Failed to beep on {Name}"
                            );
                    }
                    catch (Exception er)
                    {
                        Log.Error(TAG, $"Failed to Speak on {Name} with error {er}");
                        Toast.MakeText(this, "$Failed to speak on {Name}.", ToastLength.Short).Show();
                    }
                })
                .SetNegativeButton("Cancel", (sender, e) => { })
                .Create().
                Show();
        }

        void Monitor()
        {
            var choices = new string[] { "Off", "On" };
            var choice = choices[0];
            new AlertDialog
                .Builder(this)
                .SetSingleChoiceItems(choices, 0, (sender, e) => 
                {
                    choice = choices[e.Which];
                })
                .SetPositiveButton("OK", (sender, e) => 
                {
                    bool on = choice == choices[0];
                    string op = on ? choices[0] : choices[1];
                    HandleAsyncTask(
                        Client.Call<Api.Responses.Monitor>(new Api.Requests.Monitor(on), Canceller.Token),
                        $"Turn Monitor {op}", 
                        null, 
                        $"Failed to turn monitor {op} for {Name}");
                })
                .Create()
                .Show();
        }

        void CdDrive()
        {
            var choices = new string[] { "Open", "Close" };
            var choice = choices[0];
            new AlertDialog
                .Builder(this)
                .SetSingleChoiceItems(choices, 0, (sender, e) =>
                {
                    choice = choices[e.Which];
                })
                .SetPositiveButton("OK", (sender, e) =>
                {
                    bool open = choice == choices[0];
                    string op = open ? choices[0] : choices[1];
                    HandleAsyncTask(
                        Client.Call<Api.Responses.CdDrive>(new Api.Requests.CdDrive(open), Canceller.Token),
                        $"{op} CD Drive",
                        null,
                        $"Failed to {op} CD Drive for {Name}");
                })
                .Create()
                .Show();
        }

        void SystemVolume()
        {
            var view = LayoutInflater
                .From(this)
                .Inflate(Resource.Layout.VolumeDialog, null);
            var builder = new AlertDialog.Builder(this)
                .SetView(view);
            
            var volumeSeek = view.FindViewById<SeekBar>(Resource.Id.VolumeSeek);

            volumeSeek.Max = Api.Requests.SetSystemVolume.VOLUME_MAX;
            volumeSeek.Progress = Api.Requests.SetSystemVolume.VOLUME_MAX / 2;

            builder
                .SetCancelable(true)
                .SetPositiveButton("OK", (sender, e) =>
                {
                    try
                    {
                        var volume = Legalise(
                            volumeSeek.Progress, 
                            Api.Requests.SetSystemVolume.VOLUME_MIN,
                            Api.Requests.SetSystemVolume.VOLUME_MAX);

                        HandleAsyncTask(
                            Client.Call<Api.Responses.SetSystemVolume>(new Api.Requests.SetSystemVolume(volume, null, null), Canceller.Token),
                            "System Volume",
                            null,
                            $"Failed to set system volume on {Name}"
                            );
                    }
                    catch (Exception er)
                    {
                        Log.Error(TAG, $"Failed to set system volume on {Name} with error {er}");
                        Toast.MakeText(this, "$Failed to set system volume on {Name}.", ToastLength.Short).Show();
                    }
                })
                .SetNegativeButton("Cancel", (sender, e) => { })
                .Create().
                Show();
        }
    }
}