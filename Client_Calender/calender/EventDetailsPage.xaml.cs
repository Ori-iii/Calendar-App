using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace calender
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class EventDetailsPage : Page
    {
        private Event? selected = null;

        public EventDetailsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Event selectedEvent)
            {
                this.selected = selectedEvent;
                EventTitle.Text = selected.title;
                EventCreatedSubtext.Text = selected.created_at.ToString("MMMM dd, yyyy");
                EventStartTimeSubtext.Text = selected.start_time.ToString("MMMM dd, yyyy hh:mm tt");
                EventEndTimeSubtext.Text = selected.end_time.ToString("MMMM dd, yyyy hh:mm tt");
                if (selected.groupPointer != null)
                {
                    EventGroupSubtext.Text = selected.groupPointer.group_name;
                }
            }
        }
    }
}
