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
using Android.Graphics;
using Android.Graphics.Drawables;

using System.Threading;
using System.Threading.Tasks;

namespace Hookshot.Client
{
    using Util;
    using Api;

    [Activity(Label = "Screen", Icon = "@drawable/icon")]
    public class ServerScreenActivity : Activity
    {
        static readonly string TAG = "ServerScreenActivity";
                
        string Name;
        string Address;

        ApiClient Api;
        Task ScreenTask;
        CancellationTokenSource Canceller = new CancellationTokenSource();

        ZoomableImageView ScreenImageView;
                
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Name = Intent.Extras.GetString("name");
            Address = Intent.Extras.GetString("address");

            Title = $"{Name} ({Address})";

            ScreenImageView = new ZoomableImageView(this, () => BitmapFactory.DecodeResource(Resources, Resource.Drawable.Preparing));
            SetContentView(ScreenImageView);

            Api = new ApiClient(Address);

            try
            {
                RefreshAsync();
            }
            catch (Exception e)
            {
                Log.Error(TAG, $"Failed to start activity with error {e}.");
                Toast.MakeText(this, $"Failed to connect to {Title}", ToastLength.Short).Show();
                Finish();
            }

            // Option menu setup.
            Options.AddItem("Refresh", () =>
            {
                try
                {
                    RefreshAsync();
                }
                catch (Exception e)
                {
                    Log.Error(TAG, $"Failed to refresh with error {e}.");
                    Toast.MakeText(this, $"Failed to refresh.", ToastLength.Short).Show();
                }
            });
        }

        void RefreshAsync()
        {
            // Check that the task isn't already running.
            if (ScreenTask != null)
            {
                Toast.MakeText(this, "Refresh in progress.", ToastLength.Short).Show();
                return;
            }

            ScreenTask = Api
                .Call<Api.Responses.Screen>(new Api.Requests.Screen(), Canceller.Token)
                .ContinueWith(t =>
                {
                    Canceller.Token.ThrowIfCancellationRequested();

                    RunOnUiThread(() =>
                    {
                        // Flag that the task has been run.
                        ScreenTask = null;

                        if (t.IsCanceled) return;

                        if (t.IsFaulted)
                        {
                            Log.Error(TAG, $"Failed to fetch screen image with error {t.Exception.Flatten().InnerException}.");
                            Toast.MakeText(this, "Failed to fetch screen image.", ToastLength.Short).Show();
                            return;
                        }

                        var data = t.Result.Image ?? new byte[0];
                        if (data.Length == 0) return;
                        var bitmap = BitmapFactory.DecodeByteArray(data, 0, data.Length);
                        ScreenImageView.SetBitmap(bitmap);
                    });
                });
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            Canceller.Cancel();

            ScreenImageView.OnDestroy();
        }

        OptionsMenu Options = new OptionsMenu();

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            return Options.OnCreateOptionsMenu(menu);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            return Options.OnOptionsItemSelected(item);
        }
    }
}