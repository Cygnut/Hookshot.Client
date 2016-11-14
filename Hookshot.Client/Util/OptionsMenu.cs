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
    class OptionsMenu
    {
        class Option
        {
            public string Title { get; set; }
            public Action Action;
        }

        List<Option> Options = new List<Option>();

        public void AddItem(string title, Action action)
        {
            Options.Add(new Option
            {
                Title = title,
                Action = action,
            });
        }

        public bool OnCreateOptionsMenu(IMenu menu)
        {
            for (int i = 0; i < Options.Count(); ++i)
                menu.Add(Menu.None, i, i, Options[i].Title);
            return true;
        }

        public bool OnOptionsItemSelected(IMenuItem item)
        {
            var menuItem = Options.ElementAtOrDefault(item.ItemId);
            if (menuItem == null) return false;

            menuItem.Action();
            return true;
        }
    }
}