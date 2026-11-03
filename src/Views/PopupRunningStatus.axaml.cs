using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;

namespace SourceGit.Views
{
    public partial class PopupRunningStatus : UserControl
    {
        public static readonly DirectProperty<PopupRunningStatus, string> DescriptionProperty =
            AvaloniaProperty.RegisterDirect<PopupRunningStatus, string>(
                nameof(Description),
                static o => o.Description,
                static (o, v) => o.Description = v);

        public string Description
        {
            get => _description;
            set => SetAndRaise(DescriptionProperty, ref _description, value);
        }

        public static readonly DirectProperty<PopupRunningStatus, double> ProgressProperty =
            AvaloniaProperty.RegisterDirect<PopupRunningStatus, double>(
                nameof(Progress),
                static o => o.Progress,
                static (o, v) => o.Progress = v);

        public double Progress
        {
            get => _progress;
            set => SetAndRaise(ProgressProperty, ref _progress, value);
        }

        public PopupRunningStatus()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _isUnloading = false;
            if (IsVisible)
                StartAnim();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            _isUnloading = true;
            base.OnUnloaded(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsVisibleProperty)
            {
                if (IsVisible && !_isUnloading)
                    StartAnim();
                else
                    StopAnim();
            }
            else if (change.Property == ProgressProperty)
            {
                if (Icon.Content != null)
                    UpdateProgressBar();
            }
        }

        private void StartAnim()
        {
            Icon.Content = new Path() { Classes = { "waiting" } };
            UpdateProgressBar();
        }

        private void StopAnim()
        {
            if (Icon.Content is Path path)
                path.Classes.Clear();
            Icon.Content = null;
            ProgressBar.IsIndeterminate = false;
        }

        private void UpdateProgressBar()
        {
            // Unknown progress is shown as an indeterminate bar.
            var isKnown = _progress >= 0;
            ProgressBar.IsIndeterminate = !isKnown;
            ProgressBar.Value = isKnown ? _progress : 0;
            ProgressText.Text = isKnown ? $"{_progress:F0}%" : string.Empty;
        }

        private string _description = string.Empty;
        private double _progress = -1;
        private bool _isUnloading = false;
    }
}
