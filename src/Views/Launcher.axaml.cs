using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;

namespace SourceGit.Views
{
    public partial class Launcher : ChromelessWindow
    {
        public static readonly DirectProperty<Launcher, GridLength> CaptionHeightProperty =
            AvaloniaProperty.RegisterDirect<Launcher, GridLength>(
                nameof(CaptionHeight),
                static o => o.CaptionHeight);

        public GridLength CaptionHeight
        {
            get => _captionHeight;
            set => SetAndRaise(CaptionHeightProperty, ref _captionHeight, value);
        }

        public bool HasRightCaptionButton
        {
            get
            {
                if (OperatingSystem.IsLinux())
                    return !Native.OS.UseSystemWindowFrame;

                return OperatingSystem.IsWindows();
            }
        }

        public Launcher()
        {
            if (OperatingSystem.IsMacOS())
                CaptionHeight = new GridLength(34);
            else if (UseSystemWindowFrame)
                CaptionHeight = new GridLength(30);
            else
                CaptionHeight = new GridLength(38);

            InitializeComponent();
            PositionChanged += OnPositionChanged;

            var layout = ViewModels.Preferences.Instance.Layout;
            Width = layout.LauncherWidth;
            Height = layout.LauncherHeight;

            var x = layout.LauncherPositionX;
            var y = layout.LauncherPositionY;
            if (x != int.MinValue && y != int.MinValue && Screens is { } screens)
            {
                var position = new PixelPoint(x, y);
                var size = new PixelSize((int)layout.LauncherWidth, (int)layout.LauncherHeight);
                var desiredRect = new PixelRect(position, size);
                for (var i = 0; i < screens.ScreenCount; i++)
                {
                    var screen = screens.All[i];
                    if (screen.WorkingArea.Contains(desiredRect))
                    {
                        Position = position;
                        return;
                    }
                }
            }

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        public void BringToTop()
        {
            if (WindowState == WindowState.Minimized)
                WindowState = _lastWindowState;
            else
                Activate();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            var preferences = ViewModels.Preferences.Instance;
            WindowState = preferences.Layout.LauncherWindowState;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == WindowStateProperty)
            {
                var state = (WindowState)change.NewValue!;
                _lastWindowState = (WindowState)change.OldValue!;

                if (!OperatingSystem.IsMacOS() && !UseSystemWindowFrame)
                    CaptionHeight = new GridLength(state == WindowState.Maximized ? 30 : 38);

                ViewModels.Preferences.Instance.Layout.LauncherWindowState = state;
            }
            else if (change.Property == IsActiveProperty)
            {
                if (!IsActive && DataContext is ViewModels.Launcher { CommandPalette: { } } vm)
                    vm.CommandPalette = null;
            }

            if (OperatingSystem.IsMacOS() && WindowState != WindowState.FullScreen)
            {
                if (change.Property == WindowStateProperty ||
                    change.Property == BoundsProperty ||
                    change.Property == TitleProperty)
                    Native.MacOSUtilities.AdjustTrafficLightsForThickTitleBar(this);
            }
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);

            if (WindowState == WindowState.Normal)
            {
                var layout = ViewModels.Preferences.Instance.Layout;
                layout.LauncherWidth = Width;
                layout.LauncherHeight = Height;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (DataContext is not ViewModels.Launcher vm)
                return;

            // AltGr is reported as Ctrl+Alt. Never treat it as a hotkey while the user is typing text.
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                e.KeyModifiers.HasFlag(KeyModifiers.Alt) &&
                IsEditingText(e.Source))
            {
                base.OnKeyDown(e);
                return;
            }

            var repo = vm.ActivePage.Data as ViewModels.Repository;
            if (vm.CommandPalette != null)
            {
                if (e.Key == Key.Escape)
                {
                    vm.CommandPalette = null;
                    e.Handled = true;
                }
                else if (repo != null &&
                    vm.CommandPalette is ViewModels.LauncherPagesCommandPalette &&
                    ViewModels.HotkeyManager.Instance.Matches("Repo.OpenCommandPalette", e))
                {
                    vm.CommandPalette = new ViewModels.RepositoryCommandPalette(repo);
                    e.Handled = true;
                }
                else if (TryRunHotkey(e, vm, repo, true))
                {
                    e.Handled = true;
                }

                return;
            }

            if (e.Key == Key.Escape && !e.KeyModifiers.HasFlag(Models.HotkeyGesture.Cmd))
            {
                vm.ActivePage.CancelPopup();
                vm.ActivePage.Notifications.Clear();
                e.Handled = true;
                return;
            }

            if (TryRunHotkey(e, vm, repo, false))
            {
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        private bool TryRunHotkey(KeyEventArgs e, ViewModels.Launcher vm, ViewModels.Repository repo, bool isCommandPaletteOpened)
        {
            var hotkeys = ViewModels.HotkeyManager.Instance;
            var isWelcome = vm.ActivePage.Data is ViewModels.Welcome;

            foreach (var def in hotkeys.Definitions)
            {
                if (!def.HandledByLauncher || !hotkeys.Matches(def.Id, e))
                    continue;

                if ((def.Scope == Models.HotkeyScope.Repository && repo == null) ||
                    (def.Scope == Models.HotkeyScope.Welcome && !isWelcome))
                    continue;

                // Only application level commands work while a command palette is shown.
                if (isCommandPaletteOpened && def.Id is not ("Global.OpenPreferences" or "Global.OpenHotkeys" or "Global.Quit" or "Global.OpenTerminal"))
                    continue;

                if (RunHotkey(def.Id, vm, repo))
                    return true;
            }

            return false;
        }

        private bool RunHotkey(string id, ViewModels.Launcher vm, ViewModels.Repository repo)
        {
            // On macOS these are registered in the application menu bar, which handles them before the window does.
            if (OperatingSystem.IsMacOS() && id is "Global.OpenPreferences" or "Global.OpenHotkeys" or "Global.Quit")
                return false;

            switch (id)
            {
                case "Global.OpenPreferences":
                    RunAsync(this.ShowDialogAsync(new Preferences()));
                    return true;
                case "Global.OpenHotkeys":
                    RunAsync(this.ShowDialogAsync(new Hotkeys()));
                    return true;
                case "Global.Quit":
                    App.Quit(0);
                    return true;
                case "Global.NewTab":
                    vm.AddNewTab();
                    return true;
                case "Global.CloseTab":
                    vm.CloseTab(null);
                    return true;
                case "Global.GotoPrevTab":
                    vm.GotoPrevTab();
                    return true;
                case "Global.GotoNextTab":
                    vm.GotoNextTab();
                    return true;
                case "Global.ConfigureWorkspace":
                    RunAsync(this.ShowDialogAsync(new ViewModels.ConfigureWorkspace()));
                    return true;
                case "Global.Clone":
                    if (vm.ActivePage.Data is not ViewModels.Welcome)
                        vm.AddNewTab();
                    ViewModels.Welcome.Instance.Clone();
                    return true;
                case "Global.OpenLocalRepository":
                    if (vm.ActivePage.Data is not ViewModels.Welcome)
                        vm.AddNewTab();
                    ViewModels.Welcome.Instance.OpenLocalRepository();
                    return true;
                case "Global.OpenTerminal":
                    if (repo != null)
                        Native.OS.OpenTerminal(repo.FullPath);
                    else
                        ViewModels.Welcome.Instance.OpenTerminal();
                    return true;
                case "Global.Refresh":
                    if (repo != null)
                    {
                        repo.RefreshAll();
                        return true;
                    }

                    if (vm.ActivePage.Data is ViewModels.Welcome welcome)
                    {
                        RunAsync(welcome.UpdateStatusAsync(true, null));
                        return true;
                    }

                    return false;
                case "Global.SSHKeyHelper":
                    App.OpenSSHKeyHelperCommand.Execute(null);
                    return true;
                case "Global.CheckForUpdate":
                    App.CheckForUpdateCommand.Execute(null);
                    return true;
            }

            if (repo == null)
                return false;

            switch (id)
            {
                case "Repo.ViewHistories":
                    repo.SelectedViewIndex = 0;
                    return true;
                case "Repo.ViewChanges":
                    repo.SelectedViewIndex = 1;
                    return true;
                case "Repo.ViewStashes":
                    repo.SelectedViewIndex = 2;
                    return true;
                case "Repo.OpenCommandPalette":
                    vm.CommandPalette = new ViewModels.RepositoryCommandPalette(repo);
                    return true;
                case "Repo.Fetch":
                    RunAsync(repo.FetchAsync(true));
                    return true;
                case "Repo.FetchWithOptions":
                    RunAsync(repo.FetchAsync(false));
                    return true;
                case "Repo.Pull":
                    RunAsync(repo.PullAsync(true));
                    return true;
                case "Repo.PullWithOptions":
                    RunAsync(repo.PullAsync(false));
                    return true;
                case "Repo.Push":
                    RunAsync(repo.PushAsync(true));
                    return true;
                case "Repo.PushWithOptions":
                    RunAsync(repo.PushAsync(false));
                    return true;
                case "Repo.Stash" when !repo.IsBare:
                    RunAsync(repo.StashAllAsync(false));
                    return true;
                case "Repo.StashDirectly" when !repo.IsBare:
                    RunAsync(repo.StashAllAsync(true));
                    return true;
                case "Repo.ApplyPatch" when !repo.IsBare:
                    repo.ApplyPatch();
                    return true;
                case "Repo.DiscardAll" when !repo.IsBare:
                    repo.WorkingCopy?.DiscardAllChanges();
                    return true;
                case "Repo.CleanWorkingCopy" when !repo.IsBare:
                    repo.WorkingCopy?.CleanWorkingCopy();
                    return true;
                case "Repo.NewBranch":
                    repo.CreateNewBranch();
                    return true;
                case "Repo.NewBranchFromSelected":
                    if (repo.CanCreatePopup() && repo.GetSelectedCommitInHistory() is { } branchBase)
                        repo.ShowPopup(new ViewModels.CreateBranch(repo, branchBase));
                    return true;
                case "Repo.NewTag":
                    repo.CreateNewTag();
                    return true;
                case "Repo.NewTagFromSelected":
                    if (repo.CanCreatePopup() && repo.GetSelectedCommitInHistory() is { } tagBase)
                        repo.ShowPopup(new ViewModels.CreateTag(repo, tagBase));
                    return true;
                case "Repo.Checkout":
                    vm.CommandPalette = new ViewModels.CheckoutCommandPalette(repo);
                    return true;
                case "Repo.Merge":
                    vm.CommandPalette = new ViewModels.MergeCommandPalette(repo);
                    return true;
                case "Repo.Compare":
                    vm.CommandPalette = new ViewModels.CompareCommandPalette(repo, null);
                    return true;
                case "Repo.Blame":
                    vm.CommandPalette = new ViewModels.BlameCommandPalette(repo.FullPath);
                    return true;
                case "Repo.FileHistory":
                    vm.CommandPalette = new ViewModels.FileHistoryCommandPalette(repo.FullPath);
                    return true;
                case "Repo.OpenFile":
                    vm.CommandPalette = new ViewModels.OpenFileCommandPalette(repo.FullPath);
                    return true;
                case "Repo.CustomActions":
                    vm.CommandPalette = new ViewModels.ExecuteCustomActionCommandPalette(repo);
                    return true;
                case "Repo.Explore":
                    Native.OS.OpenInFileManager(repo.FullPath);
                    return true;
                case "Repo.FindInHistory":
                    if (!repo.IsDashboardVisible || vm.ActivePage.Popup != null)
                        return false;
                    if (this.FindDescendantOfType<Histories>() is { } histories && histories.DataContext == repo.Histories)
                        histories.ShowFindBar();
                    return true;
                case "Repo.ToggleSearchCommits":
                    repo.Histories.IsSearchingCommits = !repo.Histories.IsSearchingCommits;
                    return true;
                case "Repo.ToggleFocusOnCurrentBranch":
                    repo.FocusOnCurrentBranch = !repo.FocusOnCurrentBranch;
                    return true;
                case "Repo.ExternalTools":
                case "Repo.GitFlow":
                case "Repo.GitLFS":
                case "Repo.Bisect":
                case "Repo.Cleanup":
                case "Repo.ViewLogs":
                case "Repo.Statistics":
                case "Repo.Configure":
                case "Repo.NavigateToHead":
                    return FindRepositoryToolbar(repo)?.RunHotkey(id) ?? false;
            }

            return false;
        }

        private RepositoryToolbar FindRepositoryToolbar(ViewModels.Repository repo)
        {
            foreach (var toolbar in this.GetVisualDescendants().OfType<RepositoryToolbar>())
            {
                if (toolbar.DataContext == repo)
                    return toolbar;
            }

            return null;
        }

        private static bool IsEditingText(object source)
        {
            if (source is not Visual visual)
                return false;

            if ((visual as TextBox ?? visual.FindAncestorOfType<TextBox>()) is { IsReadOnly: false })
                return true;

            return (visual as TextEditor ?? visual.FindAncestorOfType<TextEditor>()) is { IsReadOnly: false };
        }

        private static async void RunAsync(Task task)
        {
            if (task != null)
                await task;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (!e.Handled)
            {
                if (e.Properties.PointerUpdateKind == PointerUpdateKind.XButton1Pressed)
                    (DataContext as ViewModels.Launcher)?.GotoPrevTab();
                else if (e.Properties.PointerUpdateKind == PointerUpdateKind.XButton2Pressed)
                    (DataContext as ViewModels.Launcher)?.GotoNextTab();
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (!Design.IsDesignMode && DataContext is ViewModels.Launcher launcher)
                launcher.CloseAll();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (!Design.IsDesignMode)
                ViewModels.Preferences.Instance.Save();

            App.Quit(0);
        }

        private void OnPositionChanged(object sender, PixelPointEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                var layout = ViewModels.Preferences.Instance.Layout;
                layout.LauncherPositionX = Position.X;
                layout.LauncherPositionY = Position.Y;
            }
        }

        private void OnOpenWorkspaceMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && DataContext is ViewModels.Launcher launcher)
            {
                if (launcher.CommandPalette != null)
                    launcher.CommandPalette = null;

                var pref = ViewModels.Preferences.Instance;
                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
                menu.VerticalOffset = -6;

                var groupHeader = new TextBlock()
                {
                    Text = App.Text("Launcher.Workspaces"),
                    FontWeight = FontWeight.Bold,
                };

                var workspaces = new MenuItem();
                workspaces.Header = groupHeader;
                workspaces.IsEnabled = false;
                menu.Items.Add(workspaces);

                for (var i = 0; i < pref.Workspaces.Count; i++)
                {
                    var workspace = pref.Workspaces[i];

                    var icon = this.CreateMenuIcon(workspace.IsActive ? "Icons.Check" : "Icons.Workspace");
                    icon.Fill = workspace.Brush;

                    var item = new MenuItem();
                    item.Header = workspace.Name;
                    item.Icon = icon;
                    item.Click += (_, ev) =>
                    {
                        if (!workspace.IsActive)
                            launcher.SwitchWorkspace(workspace);

                        ev.Handled = true;
                    };

                    menu.Items.Add(item);
                }

                menu.Items.Add(new MenuItem() { Header = "-" });

                var configure = new MenuItem();
                configure.Header = App.Text("Workspace.Configure");
                configure.Click += async (_, ev) =>
                {
                    await this.ShowDialogAsync(new ViewModels.ConfigureWorkspace());
                    ev.Handled = true;
                };
                menu.Items.Add(configure);
                menu.Open(btn);
            }

            e.Handled = true;
        }

        private void OnOpenPagesCommandPalette(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Launcher vm)
                vm.CommandPalette = new ViewModels.LauncherPagesCommandPalette(vm);
            e.Handled = true;
        }

        private void OnCloseCommandPalette(object sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender && DataContext is ViewModels.Launcher vm)
                vm.CommandPalette = null;
            e.Handled = true;
        }

        private async void OnShowNewVersion(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Launcher { NewVersion: { } ver } vm)
            {
                vm.NewVersion = null;

                var ctx = new ViewModels.SelfUpdate { Data = ver };
                var dialog = new SelfUpdate() { DataContext = ctx };
                await dialog.ShowDialog(this);
            }

            e.Handled = true;
        }

        private GridLength _captionHeight = new(32);
        private WindowState _lastWindowState = WindowState.Normal;
    }
}

