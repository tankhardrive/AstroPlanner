using Avalonia.Controls;
using AstroPlanner.ViewModels;

namespace AstroPlanner.Views;

public partial class PlannerView : UserControl
{
    public PlannerView()
    {
        InitializeComponent();
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
            _                => path
        };

        vm.SortByCommand.Execute(col);

        // Prevent DataGrid from doing its own sort (we manage the collection)
        e.Handled = true;
    }
}
