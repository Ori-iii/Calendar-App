using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace calender
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static EventsPage? CurrentEventsPage { get; set; }
        public static GroupsPage? CurrentGroupsPage { get; set; }
        public static Window? _window;
        public static User? user;
        public static string host = "127.0.0.1";
        public static int port = 8443;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }


        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new loginWindow();
            _window.Activate();
        }


        public static void initializeEventGroup(User user)
        {
            foreach (var group in user.groups)
            {
                group.eventPointers.Clear();
            }
            var GroupDict = user.groups.ToDictionary(g => g.ID, g => g);
            foreach (var ev in user.events)
            {
                if (ev.group_id != null && GroupDict.TryGetValue(ev.group_id, out var group))
                {
                    ev.groupPointer = group;
                    group.eventPointers.Add(ev);
                }
                else
                {
                    ev.groupPointer = null;
                }
            }
        }
    }
}
