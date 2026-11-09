using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
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
public sealed partial class GroupDetailsPage : Page
{
    private Group? selected = null;
    public GroupDetailsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is Group selectedGroup)
        {
            this.selected = selectedGroup;
            GroupNameHeader.Text = selectedGroup.group_name;
            GroupCreatedSubtext.Text = selectedGroup.created_at.ToString("MMMM dd, yyyy");

            MembersListView.ItemsSource = selectedGroup.members;
            EventsListView.ItemsSource = selectedGroup.eventPointers;
        }
    }

    private async void KickMember_Click(object sender, RoutedEventArgs e)
    {
        if (App.user == null)
        {
            Debug.WriteLine("User is null, skipping update check.");
            return;
        }
        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is Member memberToKick && this.selected is Group selectedGroup)
        {
            string method = "DELETE";
            string path = "/delete-member";
            string deleteJson = JsonSerializer.Serialize(new
            {
                owner_id = App.user.ID,
                group_id = selectedGroup.ID,
                member = memberToKick.username
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
            Debug.WriteLine($"kick member Response: {response}");

            if (response.Contains("404 Not Found") || response.Contains("403 Forbidden"))
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Failed to Kick Member",
                    Content = $"Could not remove '{memberToKick.username}'. You may lack permission, or the user is no longer in the group.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot // Required in WinUI 3 when creating dialogs in code-behind
                };

                await errorDialog.ShowAsync();
            }

            else if (response.Contains("200 OK"))
            {
                // Remove the member from the ListView's ItemsSource
                var members = MembersListView.ItemsSource as List<Member>;
                if (members != null)
                {
                    members.Remove(memberToKick);
                    MembersListView.ItemsSource = null; // Reset the ItemsSource to refresh the ListView
                    MembersListView.ItemsSource = members;
                }
                ContentDialog successDialog = new ContentDialog
                {
                    Title = "Kicked Member",
                    Content = $"successcefully Kicked {memberToKick.username} from Group {selectedGroup.group_name}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot // Required in WinUI 3 when creating dialogs in code-behind
                };

                await successDialog.ShowAsync();
            }
            else
            {
                Debug.WriteLine($"unknown issue with member kick: {response}");
            }
        }
    }
}
