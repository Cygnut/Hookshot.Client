using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;

namespace Hookshot.Client
{
    using Util;
    using Api;

    [Activity(Label = "FileBrowser", Icon = "@drawable/icon")]
    public class FileBrowserActivity : Activity
    {
        enum EntryType
        {
            Up = 0,
            Drive = 1,
            Folder = 2,
            File = 3,
            
            Unknown = 999,  // Should have the greatest value to be sorted to the end of the ListView.
        }
        
        class Entry
        {
            public EntryType Type { get; set; }
            public string Path { get; set; }
        }

        static readonly string TAG = "FileBrowserActivity";

        public int Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }

        ListView FileListView;
        ActivityListAdapter Adapter => (ActivityListAdapter)FileListView.Adapter;

        ApiClient Api;
        CancellationTokenSource Canceller = new CancellationTokenSource();

        Comparison<ActivityListItem> Comparer = (ActivityListItem x, ActivityListItem y) =>
        {
            var xs = (Entry)x.Tag;
            var ys = (Entry)y.Tag;

            // First sort by section if these are different:
            if (xs.Type != ys.Type)
                return xs.Type > ys.Type ? 1 : -1;

            // If not, sort by text:
            return string.Compare(x.Text, y.Text);
        };

        bool CanClick(EntryType type)
        {
            switch (type)
            {
                case EntryType.Up: return true;
                case EntryType.Drive: return true;
                case EntryType.Folder: return true;
            }
            return false;
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Id = Intent.Extras.GetInt("id");
            Name = Intent.Extras.GetString("name");
            Address = Intent.Extras.GetString("address");

            Title = $"{Name} ({Address})";

            SetContentView(Resource.Layout.FileBrowser);

            FileListView = FindViewById<ListView>(Resource.Id.FileBrowserListView);
            FileListView.Adapter = new ActivityListAdapter(this);

            Api = new ApiClient(Address);

            // Register long press
            FileListView.ItemClick += (object sender, AdapterView.ItemClickEventArgs e) => {
                try
                {
                    var item = Adapter.GetItem(e.Position) as ActivityListItem;
                    if (item == null) return;

                    var entry = (Entry)item.Tag;
                    if (CanClick(entry.Type))
                        RenderDirectory(entry.Path);
                }
                catch (Exception er)
                {
                    Toast.MakeText(this, $"Failed to enter directory.", ToastLength.Short).Show();
                    Log.Error(TAG, $"Failed to enter directory with error {er}.");
                }
            };

            // Context menu setup.
            RegisterForContextMenu(FileListView);

            ListViewMenuItems.Add(new ListViewMenuItem
            {
                Title = "Run",
                ValidTypes = new EntryType[] { EntryType.File },
                Action = async i => 
                {
                    var entry = (Entry)i.Tag;
                    await Api.Call<Api.Responses.RunFile>(new Api.Requests.RunFile(entry.Path, null), Canceller.Token);
                } 
            });

            // Option menu setup.
            Options.AddItem("Refresh", () =>
            {
                RenderLastSuccessfulPath();
            });
            Options.AddItem("Drives", () =>
            {
                RenderDrives();
            });

            try
            {
                RenderLastSuccessfulPath();
            }
            catch (Exception e)
            {
                Toast.MakeText(this, $"Failed to browse files on {Title}.", ToastLength.Short).Show();
                Log.Error(TAG, $"Failed to start activity with error {e}.");
                Finish();
            }
        }
        
        protected override void OnDestroy()
        {
            base.OnDestroy();

            Canceller.Cancel();
        }

        #region Options Menu
        OptionsMenu Options = new OptionsMenu();

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            return Options.OnCreateOptionsMenu(menu);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            return Options.OnOptionsItemSelected(item);
        }
        #endregion

        #region Context Menu
        class ListViewMenuItem
        {
            public string Title { get; set; }
            public EntryType[] ValidTypes { get; set; }
            public Action<ActivityListItem> Action;
        }

        List<ListViewMenuItem> ListViewMenuItems = new List<ListViewMenuItem>();

        ActivityListItem GetListViewItem(IContextMenuContextMenuInfo menuInfo)
        {
            var info = menuInfo as AdapterView.AdapterContextMenuInfo;
            if (info == null) return null;

            return Adapter.GetItem(info.Position) as ActivityListItem;
        }

        public override void OnCreateContextMenu(IContextMenu menu, View v, IContextMenuContextMenuInfo menuInfo)
        {
            var item = GetListViewItem(menuInfo);
            if (item == null) return;

            var entry = (Entry)item.Tag;

            menu.SetHeaderTitle(item.Text);            
            for (int i = 0; i < ListViewMenuItems.Where(m => m.ValidTypes.Contains(entry.Type)).Count(); ++i)
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
        #endregion

        EntryType ParseFileType(string type)
        {
            if (type.Equals("file", StringComparison.OrdinalIgnoreCase))
                return EntryType.File;
            else if (type.Equals("dir", StringComparison.OrdinalIgnoreCase))
                return EntryType.Folder;

            return EntryType.Unknown;
        }
        
        int? GetImage(EntryType t)
        {
            switch (t)
            {
                case EntryType.Up: return Resource.Drawable.Folder;
                case EntryType.Drive: return Resource.Drawable.Drive;
                case EntryType.Folder: return Resource.Drawable.Folder;
                case EntryType.File: return Resource.Drawable.File;
                case EntryType.Unknown: return null;
            }
            return null;
        }

        ActivityListItem CreateItem(string text, EntryType type, string path)
        {
            return new ActivityListItem
            {
                Text = text,
                Image = GetImage(type),
                Tag = new Entry
                {
                    Path = path,
                    Type = type,
                },
            };
        }

        void Populate(string containingDir, Task<List<ActivityListItem>> t)
        {
            RunOnUiThread(() =>
            {
                if (t.IsFaulted)
                {
                    Log.Error(TAG, $"Failed to fetch files with error {t.Exception.Flatten().InnerException}.");
                    Toast.MakeText(this, $"Failed to fetch files.", ToastLength.Short).Show();
                    return;
                }

                Adapter.Items = t.Result.ToList();
                // If we've been supplied with the path for the directory that contains the items
                if (containingDir != null)
                {
                    var up = CreateItem("..", EntryType.Up, containingDir + "/..");
                    Adapter.Items.Add(up);
                }
                Adapter.Items.Sort(Comparer);
                Adapter.NotifyDataSetChanged();
            });
        }

        void RenderDrives()
        {
            Api
                .Call<Api.Responses.Drives>(new Api.Requests.Drives(), Canceller.Token)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        throw t.Exception;

                    if (t.IsCanceled)
                        return new List<ActivityListItem>();

                    return t.Result.drives.Select(d =>
                    {
                        var text = d.name;
                        if (!string.IsNullOrEmpty(d.volumeName))
                            text += $" ({d.volumeName})";
                        return CreateItem(text, EntryType.Drive, d.name + "/");
                    }).ToList();
                })
                .ContinueWith(t => Populate(null, t));
        }

        void RenderDirectory(string path)
        {
            Api
                .Call<Api.Responses.Files>(new Api.Requests.Files(path), Canceller.Token)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        throw t.Exception;

                    if (t.IsCanceled)
                        return new List<ActivityListItem>();

                    // We successfully navigated to this directory. Save this path as the last correctly
                    // rendered directory.
                    // Do it on the Ui thread so that all Db access is serialized and safer.
                    RunOnUiThread(() =>
                    {
                        new Db().UpdateBrowsedFilepath(Id, path);
                    });

                    return t.Result.files
                        // Eliminate directory entities of unknown type.
                        .Where(f => !string.IsNullOrWhiteSpace(f.type))
                        .Select(f => 
                            CreateItem(f.name, ParseFileType(f.type), f.path)
                        )
                        .ToList();
                })
                .ContinueWith(t => Populate(path, t));
        }

        void RenderLastSuccessfulPath()
        {
            // First of all see if there is a stored filepath that we browsed to previously.
            var bf = new Db().GetBrowsedFilepath(Id);
            if (bf != null)
                // If there is one, navigate to it.
                RenderDirectory(bf.Filepath);
            else
                // Just go to drives if we never did it before.
                RenderDrives();
        }
    }
}