using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
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
public sealed partial class Accessibility : Page
{
    public Accessibility()
    {
        InitializeComponent();
        DeleteUser.Foreground = new SolidColorBrush(Colors.Red);
        if (App.user != null)
            userNameTextBlock.Text = App.user.display_name;
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        App.user = null;
        loginWindow login = new loginWindow();
        login.Activate();
        if (App._window != null)
            App._window.Close();
    }

    private async void DeleteUser_Click(object sender, RoutedEventArgs e)
    {
        if (App.user.ID != null)
        {
            string method = "DELETE";
            string path = "/delete-user";
            string deleteJson = JsonSerializer.Serialize(new
            {
                user_id = App.user.ID
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
            if (response.Contains("200 OK"))
            {
                App.user = null;
                loginWindow login = new loginWindow();
                login.Activate();
                if (App._window != null)
                    App._window.Close();
            }
            else if(response.Contains("404 Not Found"))
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to delete your user. User not found",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                errorDialog.XamlRoot = this.Content.XamlRoot;
                await errorDialog.ShowAsync();
                return;
            }
            else {
                var errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to delete your user. Unknown error",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                errorDialog.XamlRoot = this.Content.XamlRoot;
                await errorDialog.ShowAsync();
                return;
            }
        }
    }
}
