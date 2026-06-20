using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HostManage.Models;

namespace HostManage.Views;

public partial class HostRulesPage : Page
{
    public HostRulesPage()
    {
        InitializeComponent();
    }

    private void RulesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dataGrid || dataGrid.SelectedItem is not HostRule rule)
            return;

        var args = new RoutedEventArgs(EditRuleRequestedEvent, rule);
        RaiseEvent(args);
    }

    public static readonly RoutedEvent EditRuleRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(EditRuleRequested),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(HostRulesPage));

    public event RoutedEventHandler EditRuleRequested
    {
        add => AddHandler(EditRuleRequestedEvent, value);
        remove => RemoveHandler(EditRuleRequestedEvent, value);
    }
}
