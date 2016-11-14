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

using SQLite;
using SQLite.Extensions;

namespace Hookshot.Client.Orm
{
    [Table("BrowsedFilepath")]
    class BrowsedFilepath
    {
        // There's no way to specify ForeignKeys for SQLite-Net. So we'll have to enforce it manually :(
        [PrimaryKey, AutoIncrement, Column("Id")]
        public int Id { get; set; }
        public int ServerId { get; set; }
        public string Filepath { get; set; }
    }
}