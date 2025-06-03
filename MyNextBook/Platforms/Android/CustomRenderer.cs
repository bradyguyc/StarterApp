using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace MyNextBook.Platforms.Android
{
    public class CustomShellRenderer : ShellRenderer
    {
        protected override IShellToolbarTracker CreateTrackerForToolbar(AndroidX.AppCompat.Widget.Toolbar toolbar)
        {
            toolbar.ContentInsetStartWithNavigation = 0;
            toolbar.SetContentInsetsAbsolute(0, 0);
            return base.CreateTrackerForToolbar(toolbar);
        }
    }

}
