using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class Repository : UserControl
    {
        public static readonly DirectProperty<Repository, bool> IsCommitSearchPanelVisibleProperty =
            AvaloniaProperty.RegisterDirect<Repository, bool>(
                nameof(IsCommitSearchPanelVisible),
                static o => o.IsCommitSearchPanelVisible,
                static (o, v) => o.IsCommitSearchPanelVisible = v);

        public bool IsCommitSearchPanelVisible
        {
            get => _isCommitSearchPanelVisible;
            set
            {
                if (SetAndRaise(IsCommitSearchPanelVisibleProperty, ref _isCommitSearchPanelVisible, value))
                    CalculateSidebarWidth();
            }
        }

        public static readonly DirectProperty<Repository, bool> IsNormalSidebarVisibleProperty =
            AvaloniaProperty.RegisterDirect<Repository, bool>(
                nameof(IsNormalSidebarVisible),
                static o => o.IsNormalSidebarVisible,
                static (o, v) => o.IsNormalSidebarVisible = v);

        public bool IsNormalSidebarVisible
        {
            get => _isNormalSidebarVisible;
            set
            {
                if (SetAndRaise(IsNormalSidebarVisibleProperty, ref _isNormalSidebarVisible, value))
                    CalculateSidebarWidth();
            }
        }

        public static readonly DirectProperty<Repository, double> SidebarMinWidthProperty =
            AvaloniaProperty.RegisterDirect<Repository, double>(
                nameof(SidebarMinWidth),
                static o => o.SidebarMinWidth,
                static (o, v) => o.SidebarMinWidth = v);

        public double SidebarMinWidth
        {
            get => _sidebarMinWidth;
            set => SetAndRaise(SidebarMinWidthProperty, ref _sidebarMinWidth, value);
        }

        public static readonly DirectProperty<Repository, GridLength> SidebarWidthProperty =
            AvaloniaProperty.RegisterDirect<Repository, GridLength>(
                nameof(SidebarWidth),
                static o => o.SidebarWidth,
                static (o, v) => o.SidebarWidth = v);

        public GridLength SidebarWidth
        {
            get => _sidebarWidth;
            set
            {
                if (SetAndRaise(SidebarWidthProperty, ref _sidebarWidth, value))
                {
                    var layout = ViewModels.Preferences.Instance.Layout;
                    if (_isCommitSearchPanelVisible)
                        layout.RepositorySearchCommitWidth = value.Value;
                    else if (_isNormalSidebarVisible)
                        layout.RepositorySidebarWidth = value.Value;
                }
            }
        }

        public Repository()
        {
            _sidebarWidth = new GridLength(ViewModels.Preferences.Instance.Layout.RepositorySidebarWidth);
            InitializeComponent();

            _leftSidebarGroups =
            [
                (LocalBranchGroupHeader, LocalBranchTree),
                (RemoteGroupHeader, RemoteBranchTree),
                (TagGroupHeader, TagsList),
                (SubmoduleGroupHeader, SubmoduleList),
                (WorktreeGroupHeader, WorktreeList),
            ];

            LeftSidebarGroups.AddHandler(RequestBringIntoViewEvent, OnLeftSidebarRequestBringIntoView);
            LeftSidebarGroups.AddHandler(KeyDownEvent, OnLeftSidebarKeyDown, RoutingStrategies.Tunnel);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            UpdateLeftSidebarLayout();
        }

        private async void OnOpenConfigure(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await this.ShowDialogAsync(new ViewModels.RepositoryConfigure(repo));
                e.Handled = true;
            }
        }

        private async void OnSkipInProgress(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
                await repo.SkipMergeAsync();

            e.Handled = true;
        }

        private void OnResolveInProgress(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
                repo.SelectedViewIndex = 1;

            e.Handled = true;
        }

        private async void OnAbortInProgress(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
                await repo.AbortMergeAsync();

            e.Handled = true;
        }

        private void OnToggleSidebarFilter(object _, RoutedEventArgs e)
        {
            FilterBox.Focus();
            e.Handled = true;
        }

        private void OnLocalBranchTreeSelectionChanged(object _1, RoutedEventArgs _2)
        {
            RemoteBranchTree.UnselectAll();
            TagsList.UnselectAll();
        }

        private void OnRemoteBranchTreeSelectionChanged(object _1, RoutedEventArgs _2)
        {
            LocalBranchTree.UnselectAll();
            TagsList.UnselectAll();
        }

        private void OnTagsSelectionChanged(object _1, RoutedEventArgs _2)
        {
            LocalBranchTree.UnselectAll();
            RemoteBranchTree.UnselectAll();
        }

        private void OnWorktreeContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is Control { DataContext: ViewModels.Worktree worktree } ctrl && DataContext is ViewModels.Repository repo)
            {
                var menu = new ContextMenu();

                var switchTo = new MenuItem();
                switchTo.Header = App.Text("Worktree.Open");
                switchTo.Icon = this.CreateMenuIcon("Icons.Folder.Open");
                switchTo.Click += (_, ev) =>
                {
                    repo.OpenWorktree(worktree);
                    ev.Handled = true;
                };
                menu.Items.Add(switchTo);
                menu.Items.Add(new MenuItem() { Header = "-" });

                if (worktree.IsLocked)
                {
                    var unlock = new MenuItem();
                    unlock.Header = App.Text("Worktree.Unlock");
                    unlock.Icon = this.CreateMenuIcon("Icons.Unlock");
                    unlock.Click += async (_, ev) =>
                    {
                        await repo.UnlockWorktreeAsync(worktree);
                        ev.Handled = true;
                    };
                    menu.Items.Add(unlock);
                }
                else
                {
                    var loc = new MenuItem();
                    loc.Header = App.Text("Worktree.Lock");
                    loc.Icon = this.CreateMenuIcon("Icons.Lock");
                    loc.IsEnabled = !worktree.IsMain;
                    loc.Click += async (_, ev) =>
                    {
                        await repo.LockWorktreeAsync(worktree);
                        ev.Handled = true;
                    };
                    menu.Items.Add(loc);
                }

                var remove = new MenuItem();
                remove.Header = App.Text("Worktree.Remove");
                remove.Icon = this.CreateMenuIcon("Icons.Clear");
                remove.IsEnabled = !worktree.IsCurrent && !worktree.IsMain;
                remove.Click += (_, ev) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.RemoveWorktree(repo, worktree));
                    ev.Handled = true;
                };
                menu.Items.Add(remove);

                var copy = new MenuItem();
                copy.Header = App.Text("Worktree.CopyPath");
                copy.Icon = this.CreateMenuIcon("Icons.Copy");
                copy.Click += async (_, ev) =>
                {
                    await this.CopyTextAsync(worktree.FullPath);
                    ev.Handled = true;
                };
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(copy);
                menu.Open(ctrl);
            }

            e.Handled = true;
        }

        private void OnWorktreeDoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is Control { DataContext: ViewModels.Worktree worktree } && DataContext is ViewModels.Repository repo)
                repo.OpenWorktree(worktree);

            e.Handled = true;
        }

        private void OnWorktreeListPropertyChanged(object _, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ItemsControl.ItemsSourceProperty || e.Property == IsVisibleProperty)
                UpdateLeftSidebarLayout();
        }

        private void OnLeftSidebarRowsChanged(object _, RoutedEventArgs e)
        {
            UpdateLeftSidebarLayout();
            e.Handled = true;
        }

        private void UpdateLeftSidebarLayout()
        {
            if (DataContext is not ViewModels.Repository vm)
                return;

            // All groups share the same scroll viewer, so every list is simply as tall as all of its rows.
            LocalBranchTree.Height = LocalBranchTree.Rows.Count * 24.0;
            RemoteBranchTree.Height = RemoteBranchTree.Rows.Count * 24.0;
            TagsList.Height = TagsList.Rows * 24.0;
            SubmoduleList.Height = SubmoduleList.Rows * 24.0;
            WorktreeList.Height = vm.Worktrees.Count * 24.0;
        }

        private void OnLeftSidebarLayoutUpdated(object _1, EventArgs _2)
        {
            // Pin the header of the group at the top of the viewport, until the next group's header pushes it away.
            // NOTE: use the real (pixel-snapped) position of the scrolled content rather than the raw scroll offset,
            //       otherwise pinned headers can be off by a fraction of a pixel and let the rows below show through.
            var offset = -LeftSidebarGroups.Bounds.Top;
            foreach (var (header, content) in _leftSidebarGroups)
            {
                var top = header.Bounds.Top;
                var end = content.IsVisible ? content.Bounds.Bottom : header.Bounds.Bottom;
                var shift = Math.Clamp(offset - top, 0, Math.Max(0, end - header.Bounds.Bottom));

                if (header.RenderTransform is TranslateTransform translate)
                    translate.Y = shift;
                else
                    header.RenderTransform = new TranslateTransform(0, shift);

                header.Classes.Set("stuck", shift > 0);
            }
        }

        private void OnLeftSidebarGroupHeaderClick(object sender, RoutedEventArgs e)
        {
            // Collapsing a pinned group: keep its header where it was clicked and let the next group follow right after it.
            if (sender is ToggleButton { IsChecked: false } header && e.Source == header && header.Classes.Contains("stuck"))
                LeftSidebarScroller.Offset = new Vector(0, header.Bounds.Top);
        }

        private void OnLeftSidebarRequestBringIntoView(object _, RequestBringIntoViewEventArgs e)
        {
            // Rows brought into view (keyboard navigation, locating a branch...) must not end up behind a pinned group header.
            if (e.TargetObject is Visual target && target.FindAncestorOfType<ListBoxItem>(true) != null)
            {
                var rect = e.TargetRect;
                e.TargetRect = new Rect(rect.X, rect.Y - 28.0, rect.Width, rect.Height + 28.0);
            }
        }

        private void OnLeftSidebarKeyDown(object _, KeyEventArgs e)
        {
            // The lists inside can not scroll by themselves anymore, so page the whole sidebar instead.
            if (e.Key is not (Key.PageUp or Key.PageDown) || e.KeyModifiers != KeyModifiers.None)
                return;

            var page = Math.Max(24.0, LeftSidebarScroller.Viewport.Height - 28.0);
            var offset = LeftSidebarScroller.Offset;
            LeftSidebarScroller.Offset = offset.WithY(e.Key == Key.PageUp ? offset.Y - page : offset.Y + page);
            e.Handled = true;
        }

        private void OnLeftSidebarFilterTextChanged(object _1, TextChangedEventArgs _2)
        {
            LeftSidebarScroller.ScrollToHome();
        }

        private void OnOpenSortLocalBranchMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var isSortByName = repo.IsSortingLocalBranchByName;
                var byNameAsc = new MenuItem();
                byNameAsc.Header = App.Text("Repository.BranchSort.ByName");
                if (isSortByName)
                    byNameAsc.Icon = this.CreateMenuIcon("Icons.Check");
                byNameAsc.Click += (_, ev) =>
                {
                    if (!isSortByName)
                        repo.IsSortingLocalBranchByName = true;
                    ev.Handled = true;
                };

                var byCommitterDate = new MenuItem();
                byCommitterDate.Header = App.Text("Repository.BranchSort.ByCommitterDate");
                if (!isSortByName)
                    byCommitterDate.Icon = this.CreateMenuIcon("Icons.Check");
                byCommitterDate.Click += (_, ev) =>
                {
                    if (isSortByName)
                        repo.IsSortingLocalBranchByName = false;
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
                menu.Items.Add(byNameAsc);
                menu.Items.Add(byCommitterDate);
                menu.Open(button);
            }

            e.Handled = true;
        }

        private void OnOpenSortRemoteBranchMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var isSortByName = repo.IsSortingRemoteBranchByName;
                var byNameAsc = new MenuItem();
                byNameAsc.Header = App.Text("Repository.BranchSort.ByName");
                if (isSortByName)
                    byNameAsc.Icon = this.CreateMenuIcon("Icons.Check");
                byNameAsc.Click += (_, ev) =>
                {
                    if (!isSortByName)
                        repo.IsSortingRemoteBranchByName = true;
                    ev.Handled = true;
                };

                var byCommitterDate = new MenuItem();
                byCommitterDate.Header = App.Text("Repository.BranchSort.ByCommitterDate");
                if (!isSortByName)
                    byCommitterDate.Icon = this.CreateMenuIcon("Icons.Check");
                byCommitterDate.Click += (_, ev) =>
                {
                    if (isSortByName)
                        repo.IsSortingRemoteBranchByName = false;
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
                menu.Items.Add(byNameAsc);
                menu.Items.Add(byCommitterDate);
                menu.Open(button);
            }

            e.Handled = true;
        }

        private void OnOpenSortTagMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var isSortByName = repo.IsSortingTagsByName;
                var byCreatorDate = new MenuItem();
                byCreatorDate.Header = App.Text("Repository.Tags.OrderByCreatorDate");
                if (!isSortByName)
                    byCreatorDate.Icon = this.CreateMenuIcon("Icons.Check");
                byCreatorDate.Click += (_, ev) =>
                {
                    if (isSortByName)
                        repo.IsSortingTagsByName = false;
                    ev.Handled = true;
                };

                var byName = new MenuItem();
                byName.Header = App.Text("Repository.Tags.OrderByName");
                if (isSortByName)
                    byName.Icon = this.CreateMenuIcon("Icons.Check");
                byName.Click += (_, ev) =>
                {
                    if (!isSortByName)
                        repo.IsSortingTagsByName = true;
                    ev.Handled = true;
                };

                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
                menu.Items.Add(byName);
                menu.Items.Add(byCreatorDate);
                menu.Open(button);
            }

            e.Handled = true;
        }

        private async void OnPruneWorktrees(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
                await repo.PruneWorktreesAsync();

            e.Handled = true;
        }

        private void OnRemoveSelectedHistoryFilter(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo && sender is Button { DataContext: Models.HistoryFilter filter })
                repo.RemoveHistoryFilter(filter);

            e.Handled = true;
        }

        private async void OnBisectCommand(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                DataContext is ViewModels.Repository { IsBisectCommandRunning: false } repo &&
                repo.CanCreatePopup())
                await repo.ExecBisectCommandAsync(button.Tag as string);

            e.Handled = true;
        }

        private void OnRightPagePropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Control.IsVisibleProperty && sender is Control page)
            {
                var diffViewer = page.FindDescendantOfType<DiffView>();
                diffViewer?.ToggleHotkeyBindings(page.IsVisible);
            }
        }

        private void CalculateSidebarWidth()
        {
            var layout = ViewModels.Preferences.Instance.Layout;
            if (_isCommitSearchPanelVisible)
            {
                SidebarMinWidth = 200;
                SidebarWidth = new GridLength(layout.RepositorySearchCommitWidth, GridUnitType.Pixel);
            }
            else if (_isNormalSidebarVisible)
            {
                SidebarMinWidth = 200;
                SidebarWidth = new GridLength(layout.RepositorySidebarWidth, GridUnitType.Pixel);
            }
            else
            {
                SidebarMinWidth = 0;
                SidebarWidth = new GridLength(0, GridUnitType.Pixel);
            }
        }

        private readonly (ToggleButton Header, Control Content)[] _leftSidebarGroups;
        private bool _isNormalSidebarVisible = true;
        private bool _isCommitSearchPanelVisible = false;
        private double _sidebarMinWidth = 200;
        private GridLength _sidebarWidth;
    }
}
