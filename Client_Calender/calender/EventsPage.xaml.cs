using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace calender
{
    public class CustomDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? dateString = reader.GetString();
            if (string.IsNullOrWhiteSpace(dateString))
            {
                return default;
            }

            // Clean up extra spaces around the 'T' separator
            string cleanedString = dateString.Replace(" T ", "T").Trim();

            if (DateTime.TryParse(cleanedString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsedDate))
            {
                return parsedDate;
            }

            return DateTime.Parse(cleanedString);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("o"));
        }
    }

    public sealed partial class EventsPage : Page
    {
        public readonly List<Event> _allEvents = new();
        private readonly ObservableCollection<Event> _selectedDayEvents = new();

        public EventsPage()
        {
            this.InitializeComponent();
            EventsListView.ItemsSource = _selectedDayEvents;

            if (App.user != null)
            {
                foreach (Event evt in App.user.events)
                {
                    _allEvents.Add(evt);
                }
            }

            MyCalendar.SelectedDates.Add(DateTime.Today);
            App.CurrentEventsPage = this;
        }

        private void RefreshCalendarHighlights(bool isdeleteButton = true)
        {
            var currentDate = DateTime.Today;
            if (isdeleteButton == false)
                currentDate = MyCalendar.SelectedDates.Count > 0 ? MyCalendar.SelectedDates[0].Date : DateTime.Today;
            // Shift by a full month and back to force CalendarView to fully redraw its grid
            MyCalendar.SetDisplayDate(currentDate.AddMonths(12));
            MyCalendar.SetDisplayDate(currentDate);
        }

        private void MyCalendar_CalendarViewDayItemChanging(CalendarView sender, CalendarViewDayItemChangingEventArgs args)
        {
            if (args.Phase == 0)
            {
                args.RegisterUpdateCallback(MyCalendar_CalendarViewDayItemChanging);
            }
            else if (args.Phase == 1)
            {
                var dayItem = args.Item;
                DateTime itemDate = dayItem.Date.Date;

                bool hasEvents = _allEvents.Any(e => e.start_time.Date == itemDate);

                if (hasEvents)
                {
                    // Highlight dates that have events with a soft background tint
                    dayItem.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 0, 120, 215));
                }
                else
                {
                    // Reset background for recycled day items
                    dayItem.Background = null;
                }
            }
        }

        public void RefreshCurrentView()
        {
            if (MyCalendar.SelectedDates.Count > 0)
            {
                RefreshEventsForSelectedDate(MyCalendar.SelectedDates[0].Date);
            }
        }

        private void MyCalendar_SelectedDatesChanged(CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            if (args.AddedDates.Count > 0)
            {
                var selectedDate = args.AddedDates[0].Date;
                SelectedDateHeader.Text = selectedDate.ToString("dddd, MMMM d, yyyy");
                RefreshEventsForSelectedDate(selectedDate);
            }
            else
            {
                _selectedDayEvents.Clear();
                SelectedDateHeader.Text = "Select a date";
            }
        }

        private void RefreshEventsForSelectedDate(DateTime date)
        {
            _selectedDayEvents.Clear();

            var matchingEvents = _allEvents
                .Where(e => e.start_time.Date == date)
                .OrderBy(e => e.start_time);

            foreach (var evt in matchingEvents)
            {
                _selectedDayEvents.Add(evt);
            }
        }

        private async void AddEventButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.user == null)
            {
                Debug.WriteLine("User is null, skipping update check.");
                return;
            }
            if (MyCalendar.SelectedDates.Count == 0)
            {
                return;
            }

            var selectedDate = MyCalendar.SelectedDates[0].Date;

            var titleBox = new TextBox
            {
                Header = "Event Title",
                PlaceholderText = "e.g., Team Sync"
            };

            var groupComboBox = new ComboBox
            {
                Header = "Assign to Group (Optional)",
                PlaceholderText = "Select a group",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                DisplayMemberPath = "group_name" // Uses the 'Name' property of your Group object
            };

            if (App.user.groups != null)
            {
                groupComboBox.ItemsSource = App.user.groups;
            }

            var shareWithBox = new TextBox
            {
                Header = "Share With (Optional)",
                PlaceholderText = "Enter the Users username"
            };

            var premitionComboBox = new ComboBox
            {
                Header = "Access Level (Optional)",
                PlaceholderText = "Select access level",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = new List<string> { "viewer", "editor" },
                SelectedIndex = 0 // Default to "viewer"
            };

            var startTimePicker = new TimePicker
            {
                Header = "Start Time",
                Time = TimeSpan.FromHours(9)
            };

            var endTimePicker = new TimePicker
            {
                Header = "End Time",
                Time = TimeSpan.FromHours(10)
            };

            var errorTextBlock = new TextBlock
            {
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
                FontSize = 12,
                Visibility = Visibility.Collapsed
            };

            var stackPanel = new StackPanel
            {
                Spacing = 12,
                Children = { titleBox, groupComboBox, shareWithBox, premitionComboBox, startTimePicker, endTimePicker, errorTextBlock }
            };

            var dialog = new ContentDialog
            {
                Title = $"Add Event for {selectedDate:MMM d, yyyy}",
                Content = stackPanel,
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            void ValidateTimes()
            {
                if (endTimePicker.Time <= startTimePicker.Time)
                {
                    errorTextBlock.Text = "End time must be later than start time.";
                    errorTextBlock.Visibility = Visibility.Visible;
                    dialog.IsPrimaryButtonEnabled = false;
                }
                else
                {
                    errorTextBlock.Visibility = Visibility.Collapsed;
                    dialog.IsPrimaryButtonEnabled = true;
                }
            }

            startTimePicker.TimeChanged += (s, args) =>
            {
                if (startTimePicker.Time >= endTimePicker.Time)
                {
                    endTimePicker.Time = startTimePicker.Time.Add(TimeSpan.FromHours(1));
                }
                ValidateTimes();
            };

            endTimePicker.TimeChanged += (s, args) => ValidateTimes();

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(titleBox.Text))
            {
                string? selectedGroupId = null;
                if (groupComboBox.SelectedItem is Group selectedGroup)
                {
                    selectedGroupId = selectedGroup.ID; // Adjust to .ID if capitalized in your class
                }

                string eventJson = JsonSerializer.Serialize(new
                {
                    title = titleBox.Text.Trim(),
                    start_time = selectedDate.Add(startTimePicker.Time).ToUniversalTime(),
                    end_time = selectedDate.Add(endTimePicker.Time).ToUniversalTime(),
                    owner_id = App.user.ID,
                    group_id = selectedGroupId
                });
                byte[] bodyBytes = Encoding.UTF8.GetBytes(eventJson);
                Debug.WriteLine(selectedDate.Add(startTimePicker.Time).ToUniversalTime());
                // 2. Build raw HTTP/1.1 request header matching Python client's headers and CRLF formatting

                string method = "POST";
                string path = "/add-event";

                Dictionary<string, string> headers = new Dictionary<string, string>
                {
                    { "Host", $"{App.host}:{App.port}" },
                    { "Content-Type", "application/json" },
                    { "Content-Length", bodyBytes.Length.ToString() },
                    { "Connection", "close" }
                };

                byte[] requestBytes = httpHandler.BuildHttpRequest(method, path, headers, bodyBytes);

                networkHandler secureNetwork = new networkHandler(App.host, App.port);
                await secureNetwork.SendRequestAsync(requestBytes);
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

                Event ev = JsonSerializer.Deserialize<Event>(responseBody, options) ?? throw new NullReferenceException("Failed to deserialize event");
                App.user.events.Add(ev);

                App.initializeEventGroup(App.user);

                _allEvents.Add(ev);
                RefreshEventsForSelectedDate(selectedDate);
                RefreshCalendarHighlights(false);


                if (shareWithBox.Text != null && shareWithBox.Text.Trim() != "")
                {
                    string shareJson = JsonSerializer.Serialize(new
                    {
                        event_id = ev.ID,
                        owner_id = App.user.ID,
                        username = shareWithBox.Text.Trim(),
                        access_level = premitionComboBox.SelectedItem.ToString()
                    });
                    byte[] shareBodyBytes = Encoding.UTF8.GetBytes(shareJson);
                    string sharePath = "/share-event";
                    Dictionary<string, string> shareHeaders = new Dictionary<string, string>
                    {
                        { "Host", $"{App.host}:{App.port}" },
                        { "Content-Type", "application/json" },
                        { "Content-Length", shareBodyBytes.Length.ToString() },
                        { "Connection", "close" }
                    };
                    byte[] shareRequestBytes = httpHandler.BuildHttpRequest(method, sharePath, shareHeaders, shareBodyBytes);
                    networkHandler secureNetworkShare = new networkHandler(App.host, App.port);
                    await secureNetworkShare.SendRequestAsync(shareRequestBytes);
                    byte[] shareResponseBytes = await secureNetworkShare.ReceiveResponseAsync();
                    secureNetworkShare.Close();
                    string shareResponse = Encoding.UTF8.GetString(shareResponseBytes);
                    Debug.WriteLine($"Share Event Response: {shareResponse}");
                }
            }
        }

        private async void AddGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.user == null)
            {
                Debug.WriteLine("User is null, skipping update check.");
                return;
            }
            var groupNameBox = new TextBox
            {
                Header = "Group Name",
                PlaceholderText = "e.g., Study Group"
            };

            var usernameBox = new TextBox
            {
                Header = "Add Member",
                PlaceholderText = "Enter username"
            };

            var users = new ObservableCollection<string>();

            var userListView = new ListView
            {
                Height = 150,
                BorderThickness = new Thickness(1),
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                CornerRadius = new CornerRadius(4),
                ItemsSource = users
            };

            var deleteButton = new Button
            {
                Content = "Delete Selected",
                IsEnabled = false,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var addUserButton = new Button
            {
                Content = "Add User",
                Style = (Style)Application.Current.Resources["AccentButtonStyle"],
                VerticalAlignment = VerticalAlignment.Bottom
            };

            var inputGrid = new Grid
            {
                ColumnSpacing = 8
            };
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(usernameBox, 0);
            Grid.SetColumn(addUserButton, 1);
            inputGrid.Children.Add(usernameBox);
            inputGrid.Children.Add(addUserButton);

            var stackPanel = new StackPanel
            {
                Spacing = 12,
                Width = 360,
                Children =
        {
            groupNameBox,
            inputGrid,
            new TextBlock { Text = "Group Members", Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] },
            userListView,
            deleteButton
        }
            };

            var dialog = new ContentDialog
            {
                Title = "Create New Group",
                Content = stackPanel,
                PrimaryButtonText = "Create Group",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            // Helper method to handle adding users to the list
            void AddUser()
            {
                string username = usernameBox.Text.Trim();
                if (!string.IsNullOrEmpty(username) && !users.Contains(username))
                {
                    users.Add(username);
                    usernameBox.Text = string.Empty;
                }
            }

            // Event handlers inside dialog
            addUserButton.Click += (s, args) => AddUser();

            usernameBox.KeyDown += (s, args) =>
            {
                if (args.Key == Windows.System.VirtualKey.Enter)
                {
                    AddUser();
                    args.Handled = true;
                }
            };

            userListView.SelectionChanged += (s, args) =>
            {
                deleteButton.IsEnabled = userListView.SelectedItem != null;
            };

            deleteButton.Click += (s, args) =>
            {
                if (userListView.SelectedItem is string selectedUser)
                {
                    users.Remove(selectedUser);
                }
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(groupNameBox.Text))
            {
                string groupName = groupNameBox.Text.Trim();
                List<string> memberList = users.ToList();
                memberList.Add(App.user.username);

                // Send HTTP request to your backend to register the group
                string groupJson = JsonSerializer.Serialize(new
                {
                    group_name = groupName,
                    creator_id = App.user.ID,
                    members = memberList
                });
                byte[] bodyBytes = Encoding.UTF8.GetBytes(groupJson);

                string method = "POST";
                string path = "/create-group";

                Dictionary<string, string> headers = new Dictionary<string, string>
                {
                    { "Host", $"{App.host}:{App.port}" },
                    { "Content-Type", "application/json" },
                    { "Content-Length", bodyBytes.Length.ToString() },
                    { "Connection", "close" }
                };

                byte[] requestBytes = httpHandler.BuildHttpRequest(method, path, headers, bodyBytes);
                networkHandler secureNetwork = new networkHandler(App.host, App.port);

                await secureNetwork.SendRequestAsync(requestBytes);
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

                Group group = JsonSerializer.Deserialize<Group>(responseBody, options) ?? throw new NullReferenceException("Failed to deserialize group");

                App.user.groups.Add(group);
                Debug.WriteLine($"Group '{group.group_name}' created successfully with ID: {group.ID}");
            }
        }

        private async void deleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (App.user == null)
            {
                Debug.WriteLine("User is null.");
                return;
            }
            if (EventsListView.SelectedItem is Event selectedEvent)
            {
                
                string method = "DELETE";
                string path = "/delete-event";
                string deleteJson = JsonSerializer.Serialize(new
                {
                    event_id = selectedEvent.ID,
                    owner_id = App.user.ID
                });
                byte[] bodyBytes = Encoding.UTF8.GetBytes(deleteJson);
                Dictionary<string, string> headers = new Dictionary<string, string>
                {
                    { "Host", $"{App.host}:{App.port}" },
                    { "Content-Type", "application/json" },
                    { "Content-Length", bodyBytes.Length.ToString() },
                    { "Connection", "close" }
                };
                byte[] requestBytes = httpHandler.BuildHttpRequest(method, path, headers, bodyBytes);
                networkHandler secureNetwork = new networkHandler(App.host, App.port);
                await secureNetwork.SendRequestAsync(requestBytes);
                byte[] responseBytes = await secureNetwork.ReceiveResponseAsync();
                secureNetwork.Close();
                string response = Encoding.UTF8.GetString(responseBytes);
                Debug.WriteLine($"Delete Event Response: {response}");

                if (response.Contains("404 Not Found") || response.Contains("403 Forbidden"))
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = $"Failed to delete event '{selectedEvent.title}' do to a permissions issue or the event not being found.",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    };
                    errorDialog.XamlRoot = this.Content.XamlRoot;
                    await errorDialog.ShowAsync();
                    return;
                }
                if (response.Contains("200 OK"))
                {
                    var successDialog = new ContentDialog
                    {
                        Title = "Success",
                        Content = $"Event '{selectedEvent.title}' deleted successfully.",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    };
                    successDialog.XamlRoot = this.Content.XamlRoot;
                    await successDialog.ShowAsync();
                    App.user.events.Remove(selectedEvent);
                    _allEvents.Remove(selectedEvent);
                    RefreshCalendarHighlights(true);
                }

                if (MyCalendar.SelectedDates.Count > 0)
                {
                    RefreshEventsForSelectedDate(MyCalendar.SelectedDates[0].Date);
                }
            }
        }

        private async void shareEvent_Click(object sender, RoutedEventArgs e)
        {
            if (App.user == null)
            {
                Debug.WriteLine("User is null, skipping update check.");
                return;
            }
            if (EventsListView.SelectedItem is Event selectedEvent)
            {
                var usernameBox = new TextBox
                {
                    Header = "Share With",
                    PlaceholderText = "Enter the User's username"
                };
                var accessLevelComboBox = new ComboBox
                {
                    Header = "Access Level",
                    ItemsSource = new List<string> { "viewer", "editor" },
                    SelectedIndex = 0 // Default to "viewer"
                };
                var stackPanel = new StackPanel
                {
                    Spacing = 12,
                    Children = { usernameBox, accessLevelComboBox }
                };
                var dialog = new ContentDialog
                {
                    Title = $"Share Event: {selectedEvent.title}",
                    Content = stackPanel,
                    PrimaryButtonText = "Share",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(usernameBox.Text))
                {
                    string shareJson = JsonSerializer.Serialize(new
                    {
                        event_id = selectedEvent.ID,
                        owner_id = App.user.ID,
                        username = usernameBox.Text.Trim(),
                        access_level = accessLevelComboBox.SelectedItem.ToString()
                    });
                    byte[] bodyBytes = Encoding.UTF8.GetBytes(shareJson);
                    string method = "POST";
                    string path = "/share-event";
                    Dictionary<string, string> headers = new Dictionary<string, string>
                    {
                        { "Host", $"{App.host}:{App.port}" },
                        { "Content-Type", "application/json" },
                        { "Content-Length", bodyBytes.Length.ToString() },
                        { "Connection", "close" }
                    };
                    byte[] requestBytes = httpHandler.BuildHttpRequest(method, path, headers, bodyBytes);
                    networkHandler secureNetworkShare = new networkHandler(App.host, App.port);
                    await secureNetworkShare.SendRequestAsync(requestBytes);
                    byte[] responseBytes = await secureNetworkShare.ReceiveResponseAsync();
                    secureNetworkShare.Close();
                    string response = Encoding.UTF8.GetString(responseBytes);
                    Debug.WriteLine($"Share Event Response: {response}");

                    if (response.Contains("404 Not Found") || response.Contains("403 Forbidden"))
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "Error",
                            Content = $"Failed to share event '{selectedEvent.title}'. Response: {response}",
                            CloseButtonText = "OK",
                            XamlRoot = this.Content.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                        return;
                    }
                    else if(response.Contains("200 OK"))
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "success",
                            Content = $"shared event {selectedEvent.title} with {usernameBox.Text.Trim()}",
                            CloseButtonText = "OK",
                            XamlRoot = this.Content.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                        return;
                    }
                }
            }
        }

        private void Info_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Event clickedEvent)
            {

                var rootGrid = (Grid)this.Content;
                if (rootGrid.ColumnDefinitions.Count > 1)
                {
                    rootGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                }

                EventDetailsBorder.Visibility = Visibility.Visible;

                EventDetailsFrame.Navigate(typeof(EventDetailsPage), clickedEvent);
            }
        }
    }
}