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

namespace Hookshot.Client.Util
{
    public abstract class GenericBaseAdapter<TItem> : BaseAdapter<TItem>
    {
        public List<TItem> Items { get; set; }
        public Activity Context { get; set; }

        protected GenericBaseAdapter(Activity context, List<TItem> items)
        {
            Context = context;
            Items = items ?? new List<TItem>();
        }

        public override long GetItemId(int position)
        {
            return position;
        }

        public override int Count
        {
            get { return Items.Count; }
        }

        public override TItem this[int position]
        {
            get { return Items[position]; }
        }

        protected abstract View GetView(TItem item, View convertView, ViewGroup parent);

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            return GetView(Items[position], convertView, parent);
        }
    }
}