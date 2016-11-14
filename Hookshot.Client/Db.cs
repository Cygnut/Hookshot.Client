using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Util;

using SQLite;

namespace Hookshot.Client
{
    using Orm;

    class Db
    {
        static readonly string TAG = "Db";

        static readonly string Path = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal),
                "hookshot.db3");

        static Db()
        {
            // Ensure all required tables exist on first reference.
            Execute(db =>
            {
                CreateTable<Orm.Server>(db);
                CreateTable<Orm.BrowsedFilepath>(db);
            });
        }

        static void CreateTable<T>(SQLiteConnection db, CreateFlags createFlags = CreateFlags.None)
        {
            try
            {
                db.CreateTable<T>(createFlags);
                Log.Info(TAG, $"Created db table {typeof(T).Name}.");
            }
            catch (Exception e)
            {
                Log.Error(TAG, $"Failed to create table {typeof(T).Name} with error {e}.");
            }
        }

        static T Execute<T>(Func<SQLiteConnection, T> method)
        {
            try
            {
                using (var db = new SQLiteConnection(Path))
                {
                    return method(db);
                }
            }
            catch (Exception e)
            {
                Log.Error(TAG, $"Db operation failed with error {e}.");
                throw;
            }
        }

        static void Execute(Action<SQLiteConnection> method)
        {
            Execute<int>(db => {
                method(db);
                return 0;
            });
        }

        public int InsertServer(Server server)
        {
            return Execute(db =>
            {
                return server.Id = db.Insert(server);
            });
        }

        public void UpdateServer(Server server)
        {
            Execute(db =>
            {
                db.Update(server);
            });
        }

        public Server[] GetServers()
        {
            return Execute(db =>
            {
                return db.Table<Server>().ToArray();
            });
        }

        public void DeleteServer(Server server)
        {
            Execute(db =>
            {
                db.Delete(server);
            });
        }
        
        public void UpdateBrowsedFilepath(int serverId, string filepath)
        {
            Execute(db => 
            {
                var table = db.Table<BrowsedFilepath>();
                var f = table.FirstOrDefault(r => r.ServerId == serverId);
                if (f == null)
                {
                    // Then we need to create the element and add it.
                    db.Insert(new BrowsedFilepath
                    {
                        ServerId = serverId,
                        Filepath = filepath,
                    });
                }
                else
                {
                    f.Filepath = filepath;
                    db.Update(f);
                }
            });
        }

        public BrowsedFilepath GetBrowsedFilepath(int serverId)
        {
            return Execute(db =>
            {
                return db.Table<BrowsedFilepath>().FirstOrDefault(f => f.ServerId == serverId);
            });
        }
    }
}