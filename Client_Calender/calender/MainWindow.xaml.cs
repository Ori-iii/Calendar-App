using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WinRT.Interop;

namespace calender
{
    public sealed partial class MainWindow : Window
    {
        private DispatcherTimer _pollingTimer = new();
        private DateTime _lastCheckedTime = DateTime.UtcNow;
        public MainWindow()
        {
            ElementSoundPlayer.State = ElementSoundPlayerState.On;

            this.InitializeComponent();

            this.ExtendsContentIntoTitleBar = true; // Extend the content into the title bar and hide the default titlebar
            this.SetTitleBar(titleBar); // Set the custom title bar

            StartPolling();
        }
        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Select the Calendar item and navigate to EventsPage on startup
            navView.SelectedItem = navView.MenuItems[0];
            ContentFrame.Navigate(typeof(EventsPage));
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is NavigationViewItem selectedItem)
            {
                var tag = selectedItem.Tag?.ToString();
                ElementSoundPlayer.State = ElementSoundPlayerState.On;
                ElementSoundPlayer.Play(ElementSoundKind.MoveNext);
                switch (tag)
                {
                    case "EventsPage":
                        ContentFrame.Navigate(typeof(EventsPage));
                        break;
                    case "GroupsPage":
                        ContentFrame.Navigate(typeof(GroupsPage));
                        break;
                    case "AccessibilityPageItem":
                        var hwnd = WindowNative.GetWindowHandle(this);
                        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                        var appWindow = AppWindow.GetFromWindowId(windowId);

                        int width = appWindow.ClientSize.Width;
                        int height = appWindow.ClientSize.Height; ;

                        AccessibilityPopup.HorizontalOffset = 4;                     // left
                        AccessibilityPopup.VerticalOffset = height - 550;      // popup height from bottom

                        AccessibilityFrame.Navigate(typeof(Accessibility), this);
                        AccessibilityPopup.XamlRoot = this.Content.XamlRoot;

                        AccessibilityPopup.HorizontalOffset = 4;            // left padding
                        AccessibilityPopup.VerticalOffset = height - 550;   // bottom position (border height = 500)

                        AccessibilityPopup.IsOpen = true;
                        break;
                }
            }
        }


        private void StartPolling()
        {
            _pollingTimer = new DispatcherTimer();
            _pollingTimer.Interval = TimeSpan.FromSeconds(10);
            _pollingTimer.Tick += PollingTimer_Tick;
            _pollingTimer.Start();
        }

        private async void PollingTimer_Tick(object? sender, object e)
        {
            // Stop the timer temporarily during the network call so it doesn't overlap 
            // if a request takes longer than 5 seconds
            _pollingTimer.Stop();

            try
            {
                await CheckForUpdatesFromServerAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Polling error: {ex.Message}");
            }
            finally
            {
                // Restart the timer for the next 5-second interval
                _pollingTimer.Start();
            }
        }

        private async Task CheckForUpdatesFromServerAsync()
        {
            if (App.user == null)
            {
                Debug.WriteLine("User is null, skipping update check.");
                return;
            }
            string eventJson = JsonSerializer.Serialize(new
            {
                ID = App.user.ID,
                last_checked_time = _lastCheckedTime.ToUniversalTime()
            });
            byte[] bodyBytes = Encoding.UTF8.GetBytes(eventJson);

            string method = "GET";
            string path = $"/check-update";
            Debug.WriteLine($"Polling server for updates with path: {path}");
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Host", $"{App.host}:{App.port}" },
                { "Connection", "close" }
            };

            byte[] requestBytes = httpHandler.BuildHttpRequest(method, path, headers, bodyBytes);
            // 3. Establish raw TCP Socket and connect
            networkHandler secureNetwork = new networkHandler(App.host, App.port);

            // 6. Send raw request bytes (Headers + Payload)
            await secureNetwork.SendRequestAsync(requestBytes);

            // 7. Receive raw server response until server closes connection
            byte[] responseBytes = await secureNetwork.ReceiveResponseAsync();

            secureNetwork.Close();

            string response = Encoding.UTF8.GetString(responseBytes);
            var responseLines = response.Split(new[] { "\r\n\r\n" }, StringSplitOptions.None);
            var responseBody = responseLines.Length > 1 ? responseLines[1] : string.Empty;
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new CustomDateTimeConverter());
            if (response.StartsWith("HTTP/1.1 401"))
            {
                Console.WriteLine(responseBody);
                return;
            }
            var FileGroupData = JsonSerializer.Deserialize<UpdateResponse>(responseBody, options) ?? throw new NullReferenceException("Failed to deserialize Update");
            if (FileGroupData.events.Count > 0)
            {
                foreach (var ev in FileGroupData.events)
                {
                    var existingEvent = App.user.events.FirstOrDefault(e => e.ID == ev.ID);

                    if (existingEvent != null)
                    {
                        existingEvent.title = ev.title;
                        existingEvent.start_time = ev.start_time;
                        existingEvent.end_time = ev.end_time;
                        existingEvent.created_at = ev.created_at;
                        existingEvent.owner_id = ev.owner_id;
                        existingEvent.group_id = ev.group_id;
                        existingEvent.payload = ev.payload;
                    }
                    else
                    {
                        App.user.events.Add(ev);
                        App.CurrentEventsPage?._allEvents.Add(ev);
                        App.CurrentEventsPage?.RefreshCurrentView();
                    }
                }
            }
            if (FileGroupData.groups != null)
            {
                foreach (var grp in FileGroupData.groups)
                {
                    var existingGroup = App.user.groups.FirstOrDefault(g => g.ID == grp.ID);
                    if (existingGroup != null)
                    {
                        existingGroup.members = grp.members; // Update the new Group Members
                        existingGroup.group_name = grp.group_name;
                        existingGroup.created_at = grp.created_at;
                    }
                    else
                    {
                        // Brand new group, Add it to the user's local list
                        App.user.groups.Add(grp);

                        // If the groups page view is open, add it there too so it shows up on screen
                        if (App.CurrentGroupsPage != null && !App.CurrentGroupsPage.allGroups.Any(g => g.ID == grp.ID))
                        {
                            App.CurrentGroupsPage.allGroups.Add(grp);
                        }
                    }
                }
            }
            App.initializeEventGroup(App.user);
            _lastCheckedTime = DateTime.UtcNow;
        }
    }
}