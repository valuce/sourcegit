using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class LauncherPage : UserControl
    {
        public LauncherPage()
        {
            InitializeComponent();

            AddHandler(GotFocusEvent, OnGotFocusInPage, RoutingStrategies.Bubble, true);
            PopupPanel.PropertyChanged += OnPopupPanelPropertyChanged;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _lastFocused = null;
        }

        private void OnGotFocusInPage(object sender, GotFocusEventArgs e)
        {
            // Remember the last focused element outside the popup, so that the focus can go back there after the popup
            // (which steals the focus when it is shown) has been closed.
            if (e.Source is InputElement element &&
                !PopupPanel.IsVisualAncestorOf(element) &&
                TopLevel.GetTopLevel(element) == TopLevel.GetTopLevel(this))
                _lastFocused = element;
        }

        private void OnPopupPanelPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != ContentControl.ContentProperty)
                return;

            if (e.NewValue != null)
            {
                _popupOwner = DataContext;
                return;
            }

            // Switching to another page also hides the popup. Keep the focus where it is in that case.
            var owner = _popupOwner;
            _popupOwner = null;
            if (e.OldValue == null || owner != DataContext || _lastFocused is not { } target)
                return;

            Dispatcher.UIThread.Post(() => RestoreFocusAfterPopup(target), DispatcherPriority.Background);
        }

        private void RestoreFocusAfterPopup(InputElement target)
        {
            var focusManager = TopLevel.GetTopLevel(this)?.FocusManager;
            if (focusManager == null)
                return;

            // Do not take the focus back if it has been moved out of this page on purpose (to another window, for example).
            if (focusManager.GetFocusedElement() is Visual current &&
                current != this &&
                TopLevel.GetTopLevel(current) != null &&
                !this.IsVisualAncestorOf(current) &&
                !current.IsVisualAncestorOf(this))
                return;

            // The items of a list are rebuilt when it refreshes after the popup (a discarded change disappears, for example),
            // so focus the list itself. The arrow keys still move from its selected item.
            if (target is ListBoxItem item && item.FindAncestorOfType<ListBox>() is { Focusable: true } list)
                target = list;

            if (TopLevel.GetTopLevel(target) != null && target.IsEffectivelyVisible && target.IsEffectivelyEnabled)
                target.Focus();
        }

        private async void OnPopupSureByHotKey(object sender, RoutedEventArgs e)
        {
            var children = PopupPanel.GetLogicalDescendants();
            foreach (var child in children)
            {
                if (child is Control { IsKeyboardFocusWithin: true, Tag: StealHotKey steal } control &&
                    steal is { Key: Key.Enter, KeyModifiers: KeyModifiers.None })
                {
                    var fake = new KeyEventArgs()
                    {
                        RoutedEvent = KeyDownEvent,
                        Route = RoutingStrategies.Direct,
                        Source = control,
                        Key = Key.Enter,
                        KeyModifiers = KeyModifiers.None,
                        PhysicalKey = PhysicalKey.Enter,
                    };

                    control.RaiseEvent(fake);
                    e.Handled = false;
                    return;
                }
            }

            if (DataContext is ViewModels.LauncherPage page)
                await page.ProcessPopupAsync();

            e.Handled = true;
        }

        private async void OnPopupSure(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LauncherPage page)
                await page.ProcessPopupAsync();

            e.Handled = true;
        }

        private void OnPopupCancel(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LauncherPage page)
                page.CancelPopup();

            e.Handled = true;
        }

        private void OnPopupTerminate(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LauncherPage page)
                page.TerminatePopup();

            e.Handled = true;
        }

        private void OnMaskClicked(object sender, PointerPressedEventArgs e)
        {
            OnPopupCancel(sender, e);
        }

        private void OnDismissNotification(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.Notification notice } &&
                DataContext is ViewModels.LauncherPage page)
                page.Notifications.Remove(notice);

            e.Handled = true;
        }

        private void OnToolBarPointerPressed(object sender, PointerPressedEventArgs e)
        {
            this.FindAncestorOfType<ChromelessWindow>()?.BeginMoveWindow(sender, e);
        }

        private InputElement _lastFocused = null;
        private object _popupOwner = null;
    }
}
