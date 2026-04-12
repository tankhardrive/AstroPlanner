using Avalonia.Controls;
using AstroPlanner.ViewModels;

namespace AstroPlanner.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Set child view DataContexts once the window DataContext is ready.
        // This avoids AXAML compiled-binding type-inference errors from DataContext rebinding.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                PlannerViewControl.DataContext = vm.Planner;
                DetailViewControl.DataContext  = vm.Detail;
                SettingsViewControl.DataContext = vm.Settings;
            }
        };
    }
}
