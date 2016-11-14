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
    public class ActivityListItem : Java.Lang.Object
    {
        public string Text { get; set; }
        public int? Image { get; set; }
        public object Tag { get; set; }
    }
    
    public class ActivityListAdapter : GenericBaseAdapter<ActivityListItem>
    {
        public ActivityListAdapter(Activity context, List<ActivityListItem> items = null)
            : base(context, items)
        { }

        protected override View GetView(ActivityListItem item, View convertView, ViewGroup parent)
        {
            View view = convertView ?? Context.LayoutInflater.Inflate(Android.Resource.Layout.ActivityListItem, null);
            view.FindViewById<TextView>(Android.Resource.Id.Text1).Text = item.Text;
            try
            {
                view.FindViewById<ImageView>(Android.Resource.Id.Icon).SetImageResource(item.Image ?? 0);
            }
            catch (Exception) { }
            return view;
        }
    }
}