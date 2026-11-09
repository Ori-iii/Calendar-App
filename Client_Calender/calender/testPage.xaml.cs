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
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace calender
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class testPage : Page
    {
        public testPage()
        {
            InitializeComponent();
        }

        private async void async_Click(object sender, RoutedEventArgs e)
        {
            button1.IsEnabled = false;
            await Task.Delay(3000);
            button1.IsEnabled = true;
        }

        private void reg_Click(object sender, RoutedEventArgs e)
        {
            Thread.Sleep(3000);
        }
    }
}
