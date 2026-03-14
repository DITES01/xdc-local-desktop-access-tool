using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XdcLocalDesktopAccessTool.App
{
    public partial class MainWindow : Window
    {
        private TabItem? _pendingTabChangeTarget;
        private bool _suppressTabSelectionChanged;
        private TabItem? _lastSelectedTab;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += (_, __) =>
            {
                _lastSelectedTab = MainTabs.SelectedItem as TabItem;
            };
        }

        private void TabItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TabItem clickedTab)
                return;

            if (MainTabs.SelectedItem is not TabItem currentTab)
                return;

            if (clickedTab == currentTab)
                return;

            _pendingTabChangeTarget = clickedTab;

            if (currentTab == SendTabItem)
            {
                if (SendTabControl != null && SendTabControl.HasDataToLoseForTabChange())
                {
                    var result = MessageBox.Show(
                        "Changing tabs will deauthorise the current wallet and clear the current Send tab information.\n\n" +
                        "This includes the authorised session, entered address, amount, gas settings, and any preview/output shown in this tab.\n\n" +
                        "You will need to authorise again to sign transactions.\n\n" +
                        "Do you want to continue?",
                        "Confirm Tab Change",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        _pendingTabChangeTarget = null;
                        e.Handled = true;
                        return;
                    }

                    SendTabControl.DeauthoriseAndClearForTabChange();

                    _suppressTabSelectionChanged = true;
                    MainTabs.SelectedItem = clickedTab;
                    _suppressTabSelectionChanged = false;

                    _lastSelectedTab = clickedTab;
                    _pendingTabChangeTarget = null;
                    e.Handled = true;
                    return;
                }
            }

            if (currentTab == BalanceTabItem)
            {
                BalanceTabControl?.ClearForTabChange();
            }
        }

        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressTabSelectionChanged)
                return;

            if (e.Source != MainTabs)
                return;

            if (MainTabs.SelectedItem is not TabItem selectedTab)
                return;

            if (_lastSelectedTab == null)
            {
                _lastSelectedTab = selectedTab;
                return;
            }

            if (selectedTab == _lastSelectedTab)
                return;

            if (_pendingTabChangeTarget != null)
            {
                _lastSelectedTab = selectedTab;
                _pendingTabChangeTarget = null;
                return;
            }

            if (_lastSelectedTab == SendTabItem)
            {
                if (SendTabControl != null && SendTabControl.HasDataToLoseForTabChange())
                {
                    var result = MessageBox.Show(
                        "Changing tabs will deauthorise the current wallet and clear the current Send tab information.\n\n" +
                        "This includes the authorised session, entered address, amount, gas settings, and any preview/output shown in this tab.\n\n" +
                        "You will need to authorise again to sign transactions.\n\n" +
                        "Do you want to continue?",
                        "Confirm Tab Change",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result != MessageBoxResult.Yes)
                    {
                        _suppressTabSelectionChanged = true;
                        MainTabs.SelectedItem = _lastSelectedTab;
                        _suppressTabSelectionChanged = false;
                        return;
                    }

                    SendTabControl.DeauthoriseAndClearForTabChange();
                }
            }

            if (_lastSelectedTab == BalanceTabItem)
            {
                BalanceTabControl?.ClearForTabChange();
            }

            _lastSelectedTab = selectedTab;
        }
    }
}