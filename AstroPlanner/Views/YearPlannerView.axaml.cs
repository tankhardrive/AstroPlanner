using Avalonia.Controls;
using AstroPlanner.ViewModels;

namespace AstroPlanner.Views;

public partial class YearPlannerView : UserControl
{
    public YearPlannerView()
    {
        InitializeComponent();
    }

    private void OnYearGridCurrentCellChanged(object? sender, EventArgs e)
    {
        if (sender is not DataGrid grid) return;
        int month = grid.CurrentColumn?.SortMemberPath switch
        {
            "ScoreJan" => 1,  "ScoreFeb" => 2,  "ScoreMar" => 3,
            "ScoreApr" => 4,  "ScoreMay" => 5,  "ScoreJun" => 6,
            "ScoreJul" => 7,  "ScoreAug" => 8,  "ScoreSep" => 9,
            "ScoreOct" => 10, "ScoreNov" => 11, "ScoreDec" => 12,
            _ => 0
        };
        if (month == 0) return;

        var mainVm = (TopLevel.GetTopLevel(this) as Window)?.DataContext as MainWindowViewModel;
        if (mainVm != null)
            _ = mainVm.NavigateToMonthAsync(month);
    }

    private void OnYearGridSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (DataContext is not PlannerViewModel vm) return;
        if (e.Column.SortMemberPath is not { Length: > 0 } path) return;
        vm.SortByCommand.Execute(path);
        e.Handled = true;
    }
}
