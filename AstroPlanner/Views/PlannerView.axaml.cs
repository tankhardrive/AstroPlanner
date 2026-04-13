using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AstroPlanner.ViewModels;

namespace AstroPlanner.Views;

public partial class PlannerView : UserControl
{
    public PlannerView()
    {
        InitializeComponent();
    }

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not PlannerViewModel vm || vm.SelectedRow == null) return;
        var mainWindow = TopLevel.GetTopLevel(this) as Window;
        if (mainWindow?.DataContext is not MainWindowViewModel mainVm) return;
        mainVm.OpenFovPreview(vm.SelectedRow);
    }

    private void OnGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (DataContext is not PlannerViewModel vm) return;
        if (e.Column.SortMemberPath is not { Length: > 0 } path) return;

        // Map SortMemberPath to our ViewModel sort column names
        string col = path switch
        {
            "PrimaryName"    => "Name",
            "CatalogIds"     => "Name",
            "TypeDisplay"    => "Type",
            "Constellation"  => "Const",
            "SortMag"        => "Magnitude",
            "SizeDisplay"    => "Name",
            "SortDuration"   => "Duration",
            "VisStartDisplay"=> "VisStart",
            "VisEndDisplay"  => "VisStart",
            "SortPeakAlt"    => "PeakAlt",
            "SortClearance"  => "PeakClr",
            "SortMoonSep"    => "MoonSep",
            "SortScore"      => "Score",
            "SortBestFill"   => "BestSetup",
            _                => path
        };

        vm.SortByCommand.Execute(col);

        // Prevent DataGrid from doing its own sort (we manage the collection)
        e.Handled = true;
    }
}
