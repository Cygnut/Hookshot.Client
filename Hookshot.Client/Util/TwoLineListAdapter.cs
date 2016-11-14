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
    public class TwoLineListItem : Java.Lang.Object
    {
        public string Line1 { get; set; }
        public string Line2 { get; set; }
        public object Tag { get; set; }
    }

    class TwoLineListAdapter : GenericBaseAdapter<TwoLineListItem>
    {        
        public TwoLineListAdapter(Activity context, List<TwoLineListItem> items = null)
            : base(context, items)
        {
        }
        
        protected override View GetView(TwoLineListItem item, View convertView, ViewGroup parent)
        {
            View view = convertView ?? Context.LayoutInflater.Inflate(Android.Resource.Layout.TwoLineListItem, null);
            view.FindViewById<TextView>(Android.Resource.Id.Text1).Text = item.Line1;
            view.FindViewById<TextView>(Android.Resource.Id.Text2).Text = item.Line2;

            return view;
        }
    }
}