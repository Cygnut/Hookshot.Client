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
    using Api;

    [Activity(Label = "Performance", Icon = "@drawable/icon")]
    public class PerformanceActivity : Activity
    {
        static readonly string TAG = "PerformanceActivity";

        ListView PerformanceListView;
        ArrayAdapter<ListViewItem> Adapter => (ArrayAdapter<ListViewItem>)PerformanceListView.Adapter;

        CancellationTokenSource Canceller = new CancellationTokenSource();
        ApiClient Api;

        string Name;
        string Address;

        class ListViewItem
        {
            public string Title { get; set; }
            public Api.Responses.DatasetsSchema.DatasetSchema Schema { get; set; }
                        
            public ListViewItem(string title, Api.Responses.DatasetsSchema.DatasetSchema schema)
            {
                Title = title;
                Schema = schema;
            }

            public override string ToString()
            {
                return Title;
            }
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Name = Intent.Extras.GetString("name");
            Address = Intent.Extras.GetString("address");

            Title = $"{Name} ({Address})";

            SetContentView(Resource.Layout.Performance);

            try
            {
                Api = new ApiClient(Address);

                PerformanceListView = FindViewById<ListView>(Resource.Id.PerformanceListView);
                PerformanceListView.Adapter = new ArrayAdapter<ListViewItem>(this, Android.Resource.Layout.SimpleListItem1, new List<ListViewItem>());

                Api
                    .Call<Api.Responses.DatasetsSchema>(new Api.Requests.DatasetsSchema(), Canceller.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsCanceled) return;

                        if (t.IsFaulted)
                        {
                            Log.Error(TAG, $"Failed to fetch dataset schema with error {t.Exception.Flatten().InnerException}.");
                            Toast.MakeText(this, "Failed to get performance information.", ToastLength.Short).Show();
                            return;
                        }
                        
                        RunOnUiThread(() => 
                        {
                            foreach (var dataset in t.Result.datasets)
                                AddListViewItem(dataset);
                        });
                    });
                                
                PerformanceListView.ItemClick += (object sender, AdapterView.ItemClickEventArgs e) =>
                {
                    var item = Adapter.GetItem(e.Position);

                    try
                    {
                        var d = GetDataset(item.Schema.name);

                        var intent = new Intent(this, typeof(DatasetActivity))
                            .PutExtra("name", Name)
                            .PutExtra("address", Address)
                            .PutExtra("datasetName", d.Name)
                            .PutExtra("datasetAlias", d.Alias)
                            .PutExtra("timestampOffset", item.Schema.timestampOffset)
                            .PutExtra("datasetSourceUnit", d.SourceUnit)
                            .PutExtra("datasetTargetUnit", d.TargetUnit);
                        StartActivity(intent);
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

        protected override void OnDestroy()
        {
            base.OnDestroy();

            Canceller.Cancel();
        }

        class Dataset
        {
            public string Name { get; set; }
            public string Alias { get; set; }
            public string SourceUnit { get; set; }
            public string TargetUnit { get; set; }

            public Dataset(string name, string alias, string sourceUnit, string targetUnit)
            {
                Name = name;
                Alias = alias;
                SourceUnit = sourceUnit;
                TargetUnit = targetUnit;
            }
        }

        // Optional aliases for datasets.
        static readonly Dataset[] Datasets = new Dataset[]
        {
            new Dataset("os.freemem", "Free Memory", "B", "GB"),
            new Dataset("os.usedmem", "Used Memory", "B", "GB"),
            new Dataset("service.residentSetSize", "Service Resident Set Size", "B", "MB"),
            new Dataset("service.heapTotal", "Service Heap Total", "B", "MB"),
            new Dataset("service.heapUsed", "Service Heap Used", "B", "MB"),
        };

        Dataset GetDataset(string datasetName)
        {
            var d = Datasets.FirstOrDefault(a => a.Name.Equals(datasetName, StringComparison.OrdinalIgnoreCase));
            return d ?? new Dataset(datasetName, datasetName, string.Empty, string.Empty);
        }

        void AddListViewItem(Api.Responses.DatasetsSchema.DatasetSchema d)
        {
            Adapter.Add(new ListViewItem(GetDataset(d.name).Alias, d));
        }
    }
}