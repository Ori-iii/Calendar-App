using Microsoft.UI.Windowing;
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
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace calender
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class loginWindow : Window
    {
        public loginWindow()
        {
            ElementSoundPlayer.State = ElementSoundPlayerState.On;

            AppWindow.Resize(new Windows.Graphics.SizeInt32(350, 450));

            AppWindow.Move(new Windows.Graphics.PointInt32(800, 300));

            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
            InitializeComponent();
        }

        private async void login_Click(object sender, RoutedEventArgs e)
        {
            string loginJson = JsonSerializer.Serialize(new
            {
                username = username_Box.Text,
                password = password_box.Password
            }); 
            byte[] bodyBytes = Encoding.UTF8.GetBytes(loginJson);

            // 2. Build raw HTTP/1.1 request header matching Python client's headers and CRLF formatting

            string method = "POST";
            string path = "/login";

            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Host", $"{App.host}:{App.port}" },
                { "Content-Type", "application/json" },
                { "Content-Length", bodyBytes.Length.ToString() },
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
            App.user = JsonSerializer.Deserialize<User>(responseBody, options) ?? throw new NullReferenceException("Failed to deserialize user");
            App.initializeEventGroup(App.user);
            MainWindow mainWindow = new MainWindow();
            mainWindow.Activate();
            App._window = mainWindow;
            this.Close();
        }

        private void signup_Click(object sender, RoutedEventArgs e)
        {
            signupWindow signupwindow = new signupWindow();
            signupwindow.Activate();
            App._window = signupwindow;
            this.Close();
        }

        
    }
}
