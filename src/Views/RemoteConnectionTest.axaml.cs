using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class RemoteConnectionTest : UserControl
    {
        public RemoteConnectionTest()
        {
            InitializeComponent();
        }

        private async void OnTest(object _, RoutedEventArgs e)
        {
            e.Handled = true;

            if (DataContext is ViewModels.RemoteConnectionTest vm)
                await vm.RunAsync();
        }
    }
}
