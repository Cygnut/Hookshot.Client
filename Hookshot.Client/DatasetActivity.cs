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

using BarChart;

namespace Hookshot.Client
{
    using Util;
    using Api;

    [Activity(Label = "Dataset", Icon = "@drawable/icon")]
    public class DatasetActivity : Activity
    {
        static readonly string TAG = "DatasetActivity";

        class DataPoint
        {
            public long Timestamp { get; set; }
            public float Value { get; set; }
        }        

        string Name;
        string Address;
        string DatasetName;
        string DatasetAlias;
        long TimestampOffset;
        UnitConverter Converter;

        ApiClient Api;
        CancellationTokenSource Canceller = new CancellationTokenSource();

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Name = Intent.Extras.GetString("name");
            Address = Intent.Extras.GetString("address");
            DatasetName = Intent.Extras.GetString("datasetName");
            DatasetAlias = Intent.Extras.GetString("datasetAlias");
            TimestampOffset = Intent.Extras.GetLong("timestampOffset");
            var sourceUnit = Intent.Extras.GetString("datasetSourceUnit");
            var targetUnit = Intent.Extras.GetString("datasetTargetUnit");

            Title = $"{Name} ({Address})";

            try
            {
                Converter = UnitConverter.Create(sourceUnit, targetUnit);

                Api = new ApiClient(Address);

                Api.Call<Api.Responses.Dataset>(new Api.Requests.Dataset(DatasetName, null, null), Canceller.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsCanceled) return;

                        if (t.IsFaulted)
                        {
                            Log.Error(TAG, $"Failed to fetch dataset {DatasetName} with error {t.Exception.Flatten().InnerException}.");
                            Toast.MakeText(this, "Failed to get performance information.", ToastLength.Short).Show();
                            return;
                        }

                        RunOnUiThread(() =>
                        {
                            // Change units.
                            var dataset = t
                                .Result
                                .dataset
                                .Select(d => new DataPoint
                                    {
                                        Timestamp = d.Timestamp - TimestampOffset,
                                        Value = Converter.ChangeUnits(d.Value),
                                    })
                                .ToArray();

                            var minValue = dataset.Min(d => d.Value);
                            var min = dataset.First(d => d.Value == minValue);
                            var maxValue = dataset.Max(d => d.Value);
                            var max = dataset.First(d => d.Value == maxValue);
                            var width = maxValue - minValue;

                            var datasetMap = dataset.ToDictionary(d => new BarModel
                            {
                                Value = d.Value,
                                ValueCaptionHidden = true,
                            });

                            var chart = new BarChartView(this)
                            {
                                ItemsSource = datasetMap.Keys,
                                BarWidth = 3,
                                BarOffset = 1,
                                MinimumValue = min.Value - width / 2,
                                MaximumValue = max.Value + width / 2,
                            };

                            chart.AutoLevelsEnabled = false;
                            chart.AddLevelIndicator(min.Value, StringizeValue(min.Value));
                            chart.AddLevelIndicator(max.Value, StringizeValue(max.Value));

                            chart.BarClick += (sender, args) => {
                                DataPoint data = null;
                                if (!datasetMap.TryGetValue(args.Bar, out data)) return;
                                
                                new AlertDialog.Builder(this)
                                    .SetCancelable(false)
                                    .SetTitle(UIConversion.FromMilliseconds(data.Timestamp).ToString())
                                    .SetMessage(StringizeValue(data.Value))
                                    .SetPositiveButton("OK", (s, e) => { })
                                    .Create()
                                    .Show();
                            };

                            SetContentView(chart, new ViewGroup.LayoutParams(
                              ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent)
                              );
                        });
                    });
            }
            catch (Exception e)
            {
                Toast.MakeText(this, $"Failed to get performance information for {DatasetName} for {Title}", ToastLength.Short).Show();
                Log.Error(TAG, $"Failed to start activity for dataset {DatasetName} with error {e}.");
                Finish();
            }
        }

        string StringizeValue(float value) => $"{value.ToString("F3")} {Converter.TargetUnit}";

        protected override void OnDestroy()
        {
            base.OnDestroy();

            Canceller.Cancel();
        }


        // TODO: Improve this if possible.
        abstract class UnitConverter
        {
            public string SourceUnit { get; set; }
            public string TargetUnit { get; set; }

            protected abstract void Initialise();
            public abstract bool SupportsUnits(string sourceUnits);
            public abstract float ChangeUnits(float value);

            static List<Func<UnitConverter>> Factories = new List<Func<UnitConverter>>()
            {
                () => new ByteUnitConverter(),
            };
            
            public static UnitConverter Create(string sourceUnit, string targetUnit)
            {
                var converter = Factories.Select(f => f()).FirstOrDefault(c => c.SupportsUnits(sourceUnit)) ?? new DefaultConverter();
                converter.SourceUnit = sourceUnit;
                converter.TargetUnit = targetUnit;
                converter.Initialise();
                return converter;
            }

            class DefaultConverter : UnitConverter
            {
                protected override void Initialise() { }
                public override bool SupportsUnits(string units) => false;
                public override float ChangeUnits(float value) => value;
            }

            class ByteUnitConverter : UnitConverter
            {
                static readonly List<string> Units = new List<string> { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

                int Magnitude;
                protected override void Initialise()
                    => Magnitude = Units.FindIndex(s => s.Equals(TargetUnit, StringComparison.OrdinalIgnoreCase));

                // TODO: For now we only support B -> ?B.
                public override bool SupportsUnits(string sourceUnits)
                    => sourceUnits.Equals("B", StringComparison.OrdinalIgnoreCase);

                public override float ChangeUnits(float value)
                     => value / (float)Math.Pow(10, 3 * Magnitude);
            }
        }
    }
}