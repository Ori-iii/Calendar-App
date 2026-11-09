using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace calender;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class GroupsPage : Page
{
    public readonly ObservableCollection<Group> allGroups = new();

    public GroupsPage()
    {
        InitializeComponent();
        GroupListView.ItemsSource = allGroups;

        if (App.user != null)
        {
            foreach (Group group in App.user.groups)
            {
                allGroups.Add(group);
            }
        }
        App.CurrentGroupsPage = this;
    }

    private async void AddMembersButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.user == null)
        {
            Debug.WriteLine("User is null, skipping update check.");
            return;
        }
        if (GroupListView.SelectedItem is Group selectedGroup)
        {
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
                    inputGrid,
                    new TextBlock { Text = "Group Members", Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] },
                    userListView,
                    deleteButton
                }
            };

            var dialog = new ContentDialog
            {
                Title = "Add Members to Group",
                Content = stackPanel,
                PrimaryButtonText = "Add Members",
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
            if (result == ContentDialogResult.Primary && users.Count > 0)
            {
                string groupJson = JsonSerializer.Serialize(new
                {
                    members = users,
                    admin_id = App.user.ID,
                    group_id = selectedGroup.ID
                });
                byte[] bodyBytes = Encoding.UTF8.GetBytes(groupJson);

                string method = "POST";
                string path = "/add-members";

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

                // Check if server returned an error response
                if (response.Contains("403 Forbidden") || response.Contains("400 Bad Request") || response.Contains("404 Not Found"))
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "Failed to Add Members",
                        Content = "Could not add members. Make sure you are an admin and the usernames exist.",
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    return;
                }

                var responseLines = response.Split(new[] { "\r\n\r\n" }, StringSplitOptions.None);
                var responseBody = responseLines.Length > 1 ? responseLines[1] : string.Empty;

                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    Debug.WriteLine("Response body is empty.");
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                options.Converters.Add(new CustomDateTimeConverter());

                try
                {
                    List<Member> members = JsonSerializer.Deserialize<List<Member>>(responseBody, options);
                    if (members != null)
                    {
                        selectedGroup.members.AddRange(members);
                        RightSideDetailsFrame.Navigate(typeof(GroupDetailsPage), selectedGroup);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Deserialization error: {ex.Message} | Response body was: {responseBody}");
                }
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

            // Send HTTP request to backend to register the group
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
            allGroups.Add(group);
        }
    }

    private async void deleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (App.user == null)
        {
            Debug.WriteLine("User is null, skipping update check.");
            return;
        }
        if (GroupListView.SelectedItem is Group selectedGroup)
        {
            string method = "DELETE";
            string path = "/delete-group";
            string deleteJson = JsonSerializer.Serialize(new
            {
                group_id = selectedGroup.ID,
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
            Debug.WriteLine($"Delete Group Response: {response}");

            if (response.Contains("404 Not Found") || response.Contains("403 Forbidden"))
            {
                Debug.WriteLine($"Failed to delete group '{selectedGroup.group_name}'. Response: {response}");
                return;
            }

            App.user.groups.Remove(selectedGroup);
            allGroups.Remove(selectedGroup);

            // Clear and hide the details panel when a group is deleted
            RightSideDetailsFrame.Content = null;
            RightSideDetailsFrame.Visibility = Visibility.Collapsed;
        }
    }

    private void Info_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is Group clickedGroup)
        {
            LeftColumnSpace.Width = new GridLength(1, GridUnitType.Star);

            var rootGrid = (Grid)this.Content;
            if (rootGrid.ColumnDefinitions.Count > 1)
            {
                rootGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }

            RightSideDetailsFrame.Visibility = Visibility.Visible;
            RightSideDetailsFrame.Navigate(typeof(GroupDetailsPage), clickedGroup);
        }
    }

    private async void leaveGroup_Click(object sender, RoutedEventArgs e)
    {
        if (App.user == null)
        {
            Debug.WriteLine("User is null, skipping update check.");
            return;
        }
        if (GroupListView.SelectedItem is Group selectedGroup)
        {
            string method = "DELETE";
            string path = "/leave-group";
            string leaveJson = JsonSerializer.Serialize(new
            {
                group_id = selectedGroup.ID,
                member_id = App.user.ID
            });
            byte[] bodyBytes = Encoding.UTF8.GetBytes(leaveJson);
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
            Debug.WriteLine($"Leave Group Response: {response}");

            if (response.Contains("404 Not Found") || response.Contains("400 Forbidden"))
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Failed to Leave Group",
                    Content = $"Could not leave group '{selectedGroup.group_name}'. You may lack permission, or the group does not exist.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };

                await errorDialog.ShowAsync();
                return;
            }
            else if (response.Contains("200 OK"))
            {
                ContentDialog successDialog = new ContentDialog
                {
                    Title = "Left Group",
                    Content = $"Successfully left group '{selectedGroup.group_name}'.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };

                await successDialog.ShowAsync();
                App.user.groups.Remove(selectedGroup);
                allGroups.Remove(selectedGroup);

                // Clear and hide the details panel when a user leaves a group
                RightSideDetailsFrame.Content = null;
                RightSideDetailsFrame.Visibility = Visibility.Collapsed;
            }
            else
            {
                ContentDialog unknownDialog = new ContentDialog
                {
                    Title = "Unknown Response",
                    Content = $"Received an unexpected response from the server: {response}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await unknownDialog.ShowAsync();
            }
        }
    }
}