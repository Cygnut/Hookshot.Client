using Android.App;
using Android.Widget;
using Android.OS;
using Android.Content;
using Android.Util;
using Android.Views;

using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace Hookshot.Client
{
    using Util;
    using Api;

#warning need about page.

    [Activity(Label = "Hookshot", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        static readonly string TAG = "MainActivity";

        static readonly int DISCOVERY_PORT = 2999;
        static readonly string DISCOVERY_APP_NAME = "Hookshot";
        DiscoveryClient Client = new DiscoveryClient(DISCOVERY_PORT, DISCOVERY_APP_NAME);
        CancellationTokenSource Canceller = new CancellationTokenSource();

        Comparison<ActivityListItem> Comparer = (ActivityListItem x, ActivityListItem y) =>
        {
            var xs = (Server)x.Tag;
            var ys = (Server)y.Tag;

            // If they're both in the same section, compare alphabetically.
            if (xs.IsSaved == ys.IsSaved)
                return string.Compare(xs.ToString(), ys.ToString());

            // Not in the same section, put Saved items up top.
            return xs.IsSaved ? 1 : -1;
        };

        class Server
        {
            public string Name { get; set; }
            public string Address { get; set; }
            // Only set if this object is in the local db.
            public int? DbId { get; set; }

            // Derived
            public bool IsSaved => DbId.HasValue;
            public int? Image => IsSaved ? Resource.Drawable.Saved : (int?)null;

            public override string ToString() => $"{Name} ({Address})";
            
            public Server Clone()
            {
                return new Server
                {
                    DbId = DbId,
                    Name = Name,
                    Address = Address,                    
                };
            }

            public static Server FromServer(Orm.Server server)
            {
                return new Server
                {
                    DbId = server.Id,
                    Name = server.Name,
                    Address = server.Address,
                };
            }

            public Orm.Server ToServer()
            {
                return new Orm.Server
                {
                    Id = DbId.GetValueOrDefault(),
                    Name = Name,
                    Address = Address,
                };
            }
        }

        ListView MainListView;
        ActivityListAdapter Adapter => (ActivityListAdapter)MainListView.Adapter;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            SetContentView(Resource.Layout.Main);

            // Initialise the ListView content.
            MainListView = FindViewById<ListView>(Resource.Id.MainListView);
            MainListView.Adapter = new ActivityListAdapter(this);

            MainListView.ItemClick += (object sender, AdapterView.ItemClickEventArgs e) => {
                var item = Adapter.GetItem(e.Position) as ActivityListItem;
                if (item == null) return;

                var server = (Server)item.Tag;

                // Add the ListViewItem to the Saved list.
                SaveServer(server);
                // Update ListView in case that an image has changed.
                UpdateListItem(item, server);
                Adapter.NotifyDataSetChanged();

                // Send a ping for diagnostic purposes.
                new ApiClient(server.Address).Call<Api.Responses.Ping>(new Api.Requests.Ping($"HookshotMagic@{DateTime.Now}"), Canceller.Token)
                    .ContinueWith(t => 
                    {
                        if (t.IsCompleted)
                            Log.Info(TAG, t.Result.msg);
                        else
                            Log.Warn(TAG, $"Failed to ping {server.Address}");
                    });

                // Start the ServerActivity to display it.
                StartActivity(new Intent(this, typeof(ServerActivity))
                    .PutExtra("id", server.DbId.Value)
                    .PutExtra("address", server.Address)
                    .PutExtra("name", server.Name));
            };

            // Initialise item discovery.
            Client.Discovered += (DiscoveryClient.DiscoveredArgs args) => {
                RunOnUiThread(() => OnDiscovered(args));
            };
            Client.Run();

            // Add in saved servers to ListView.
            foreach (var i in LoadSavedServers())
                AddToListView(i);

            // Context menu setup.
            RegisterForContextMenu(MainListView);

            ListViewMenuItems.AddRange(new ListViewMenuItem[]
            {
                new ListViewMenuItem
                {
                    Title = "Edit",
                    Action = item => {
                        ShowServerEditor((Server)item.Tag);
                    }
                },
                new ListViewMenuItem
                {
                    Title = "Delete",
                    Action = item => {
                        var server = (Server)item.Tag;
                        new Db().DeleteServer(server.ToServer());
                        Adapter.Items.Remove(item);
                        Adapter.Items.Sort(Comparer);
                        Adapter.NotifyDataSetChanged();
                    }
                },
            });

            // Option menu setup.
            Options.AddItem("Add", () =>
            {
                ShowServerEditor(null);
            });
        }
        
        class ListViewMenuItem
        {
            public string Title { get; set; }
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

            var server = (Server)item.Tag;
            // Can only modify 'Saved' entities.
            if (!server.IsSaved) return;

            menu.SetHeaderTitle(server.ToString());
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
        
        OptionsMenu Options = new OptionsMenu();

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            return Options.OnCreateOptionsMenu(menu);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            return Options.OnOptionsItemSelected(item);
        }

        ActivityListItem UpdateListItem(ActivityListItem i, Server s)
        {
            i.Text = s.ToString();
            i.Image = s.Image;
            i.Tag = s;
            return i;
        }

        ActivityListItem FromServer(Server s)
        {
            return UpdateListItem(new ActivityListItem(), s);
        }

        IEnumerable<ActivityListItem> LoadSavedServers()
        {
            try
            {
                return new Db().GetServers().Select(s => FromServer(Server.FromServer(s)));
            }
            catch (Exception)
            {
                return new ActivityListItem[0];
            }
        }

        void SaveServer(Server server)
        {
            try
            {
                var db = new Db();

                if (server.DbId.HasValue)
                {
                    // If the item came from the db, update its fields.
                    db.UpdateServer(server.ToServer());
                }
                else
                {
                    // There is no matching item in the database - so insert it.
                    server.DbId = db.InsertServer(server.ToServer());
                }
            }
            catch (Exception e)
            {
                Log.Error(TAG, $"Failed to save server to database with error {e}.");
            }
        }

        void OnDiscovered(DiscoveryClient.DiscoveredArgs args)
        {
            var item = FromServer(new Server
            {
                Name = args.Hostname,
                Address = $"{args.Host}:{args.Port}",
            });

            AddToListView(item);
        }

        void AddItemToAdapter(ActivityListItem item)
        {
            Adapter.Items.Add(item);
            Adapter.Items.Sort(Comparer);
            Adapter.NotifyDataSetChanged();
        }

        void AddToListView(ActivityListItem item)
        {
            var server = (Server)item.Tag;
            var items = Adapter.Items;
            if (server.IsSaved)
            {
                // If it's from the db, check by db id.
                var it = items.FirstOrDefault(i => ((Server)i.Tag).DbId == server.DbId);

                if (it != null)
                    // An item exists that represents this database element - update it.
                    UpdateListItem(it, server);
                else
                    // No item exists to update this database element - add it.
                    AddItemToAdapter(item);
            }
            else
            {
                // If not, check by field equality.
                if (!items.Any(i =>
                    string.Equals(server.Name, ((Server)i.Tag).Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(server.Address, ((Server)i.Tag).Address, StringComparison.OrdinalIgnoreCase)))
                    AddItemToAdapter(item);
            }
        }

        void ShowServerEditor(Server item)
        {
            var view = LayoutInflater
                .From(this)
                .Inflate(Resource.Layout.ServerEditor, null);
            var builder = new AlertDialog.Builder(this)
                .SetView(view);

            var nameEdit = view.FindViewById<EditText>(Resource.Id.NameEdit);
            var addressEdit = view.FindViewById<EditText>(Resource.Id.AddressEdit);
            nameEdit.Text = item?.Name ?? string.Empty;
            addressEdit.Text = item?.Address ?? string.Empty;

            builder
                .SetCancelable(true)
                .SetPositiveButton("OK", (sender, e) =>
                {
                    // If we're creating a new item, then create a ListViewItem to represent it.                    
                    if (item == null)
                        item = new Server();

                    // Set/update fields
                    item.Name = nameEdit.Text;
                    item.Address = addressEdit.Text;

                    // Insert/update to db.
                    SaveServer(item);
                    // Ensure it's in the ListView.
                    AddToListView(FromServer(item));
                    // Ensure the ListView is up to date.
                    Adapter.NotifyDataSetChanged();
                })
                .SetNegativeButton("Cancel", (sender, e) => { })
                .Create()
                .Show();
        }
    }
}

