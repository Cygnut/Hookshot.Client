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

    [Activity(Label = "Server Information", Icon = "@drawable/icon")]
    public class ServerInfoActivity : Activity
    {
        static readonly string TAG = "ServerInfoActivity";
        static readonly int REFRESH_PERIOD = 3000;
        
        ListView ServerInfoListView;
        TwoLineListAdapter Adapter => (TwoLineListAdapter)ServerInfoListView.Adapter;
        
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
            string source = Intent.Extras.GetString("source");

            Title = $"{Name} ({Address})";

            SetContentView(Resource.Layout.ServerInfo);

            ServerInfoListView = FindViewById<ListView>(Resource.Id.ServerInfoListView);
            ServerInfoListView.Adapter = new TwoLineListAdapter(this);

            Api = new ApiClient(Address);
                        
            try
            {
                UpdateTask = TaskUtils.RunForever(c => 
                {
                    GetAsync(ItemsProvider.Create(source, Address)).Wait();
                }, 
                Canceller.Token,
                REFRESH_PERIOD);
            }
            catch (Exception e)
            {
                Toast.MakeText(this, $"Failed to connect to {Title}", ToastLength.Short).Show();
                Log.Error(TAG, $"Failed to start activity with error {e}.");
                Finish();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            Canceller.Cancel();
        }

        Task GetAsync(ItemsProvider provider)
        {
            return provider
                .GetItemsAsync(Canceller.Token)
                .ContinueWith(t => 
                {
                    RunOnUiThread(() =>
                    {
                        if (t.IsFaulted)
                        {
                            Log.Error(TAG, $"Failed to fetch information with error {t.Exception.Flatten().InnerException}.");
                            Toast.MakeText(this, $"Failed to fetch information.", ToastLength.Short).Show();
                            return;
                        }

                        Populate(t.Result);
                    });
                });
        }

        void Populate(TwoLineListItem[] items)
        {
            try
            {
                foreach (var i in items)
                {
                    var li = Adapter.Items.FirstOrDefault(l => l.Line1 == i.Line1);
                    if (li == null)
                        Adapter.Items.Add(i);
                    else
                        li.Line2 = i.Line2;
                }
                Adapter.Items.Sort((x, y) => string.Compare(x.Line1, y.Line1));
                Adapter.NotifyDataSetChanged();
            }
            catch (Exception) { }
        }

        abstract class ItemsProvider
        {
            public string Name { get; private set; }

            protected ApiClient Api;

            public ItemsProvider(string name, string address)
            {
                Name = name;
                Api = new ApiClient(address);
            }

            public abstract Task<TwoLineListItem[]> GetItemsAsync(CancellationToken cancel);

            static bool Is<T>(string typeName)
            {
                return typeName.Equals(typeof(T).Name, StringComparison.OrdinalIgnoreCase);
            }

            public static ItemsProvider Create(string type, string address)
            {
                if (Is<Api.Responses.Os>(type))
                    return new OsModelProvider(address);
                else if (Is<Api.Responses.Service>(type))
                    return new ServiceModelProvider(address);
                else return null;
            }

            abstract class ModelProvider<TModel> : ItemsProvider
            {
                public ModelProvider(string address) : base(typeof(TModel).Name, address) { }

                protected abstract Task<TModel> GetModelAsync(CancellationToken cancel);
                protected abstract TwoLineListItem[] Transform(TModel model);

                public override Task<TwoLineListItem[]> GetItemsAsync(CancellationToken cancel)
                {
                    return GetModelAsync(cancel)
                        .ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                                throw t.Exception;

                            return t.IsCompleted ? Transform(t.Result) : new TwoLineListItem[0];
                        });
                }
            }


            class OsModelProvider : ModelProvider<Api.Responses.Os>
            {
                public OsModelProvider(string address) : base(address) { }

                protected override Task<Api.Responses.Os> GetModelAsync(CancellationToken cancel)
                {
                    return Api.Call<Api.Responses.Os>(new Api.Requests.Os(), cancel);
                }

                protected override TwoLineListItem[] Transform(Api.Responses.Os model)
                {
                    return new TwoLineListItem[]
                    {
                        new TwoLineListItem { Line1 = "Uptime", Line2 = model.uptime },
                        new TwoLineListItem { Line1 = "Architecture", Line2 = model.arch },
                        new TwoLineListItem { Line1 = "Cpu(s)", Line2 = string.Join(System.Environment.NewLine, model.cpus.Select(cpu => cpu.model).GroupBy(s => s).Select(g => $"{g.Count()} x {g.First()}")) },
                        new TwoLineListItem { Line1 = "Free Memory", Line2 = UIConversion.FromBytes(model.freemem) },
                        new TwoLineListItem { Line1 = "Used Memory", Line2 = UIConversion.FromBytes(model.usedmem) },
                        new TwoLineListItem { Line1 = "Total Memory", Line2 = UIConversion.FromBytes(model.totalmem) },
                    };
                }
            }

            class ServiceModelProvider : ModelProvider<Api.Responses.Service>
            {
                public ServiceModelProvider(string address) : base(address) { }

                protected override Task<Api.Responses.Service> GetModelAsync(CancellationToken cancel)
                {
                    return Api.Call<Api.Responses.Service>(new Api.Requests.Service(), cancel);
                }

                protected override TwoLineListItem[] Transform(Api.Responses.Service model)
                {
                    return new TwoLineListItem[]
                    {
                        new TwoLineListItem { Line1 = "Version", Line2 = model.version },
                        new TwoLineListItem { Line1 = "Runtime", Line2 = string.Join(System.Environment.NewLine, model.versions.Select(m => $"{m.Name}@{m.Version}")) },
                        new TwoLineListItem { Line1 = "Resident Set Size", Line2 = UIConversion.FromBytes(model.memory.residentSetSize) },
                        new TwoLineListItem { Line1 = "Heap Used", Line2 = UIConversion.FromBytes(model.memory.heapUsed) },
                        new TwoLineListItem { Line1 = "Heap Total", Line2 = UIConversion.FromBytes(model.memory.heapTotal) },
                    };
                }
            }
        }
    }
}