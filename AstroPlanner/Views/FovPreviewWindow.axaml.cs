using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using AstroPlanner.ViewModels;

namespace AstroPlanner.Views;

public partial class FovPreviewWindow : Window
{
    // Guard against slider↔numeric circular updates
    private bool _syncingRotation;

    public FovPreviewWindow()
    {
        InitializeComponent();

        WebView.EnvironmentRequested += (_, e) =>
        {
            Console.WriteLine($"[FOV] EnvironmentRequested: {e.GetType().Name}");
            if (e is GtkWebViewEnvironmentRequestedEventArgs gtk)
            {
                gtk.ExperimentalOffscreen = true;
                Console.WriteLine("[FOV] ExperimentalOffscreen = true applied");
            }
        };

        WebView.AdapterCreated += (_, _) =>
        {
            Console.WriteLine($"[FOV] AdapterCreated, DataContext={DataContext?.GetType().Name ?? "null"}");
            if (DataContext is FovPreviewViewModel vm)
            {
                Console.WriteLine("[FOV] Calling NavigateToString...");
                WebView.NavigateToString(vm.HtmlContent, new Uri("https://aladin.cds.unistra.fr/"));
            }
            else
            {
                Console.WriteLine("[FOV] WARNING: DataContext not set at AdapterCreated time");
            }
        };

        WebView.NavigationCompleted += (_, e) =>
        {
            Console.WriteLine($"[FOV] NavigationCompleted");
            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(300);
                Console.WriteLine("[FOV] Nudging size...");
                NudgeSize();
            }, DispatcherPriority.Background);
        };

        RotationSlider.ValueChanged  += OnSliderChanged;
        RotationNumeric.ValueChanged += OnNumericChanged;
        ResetRotationButton.Click    += OnResetClicked;
    }

    private async void NudgeSize()
    {
        var s = ClientSize;
        ClientSize = new Avalonia.Size(s.Width + 1, s.Height);
        await Task.Delay(50);
        ClientSize = s;
    }

    private void OnSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncingRotation) return;
        _syncingRotation = true;
        RotationNumeric.Value = (decimal)e.NewValue;
        _syncingRotation = false;
        ApplyRotation(e.NewValue);
    }

    private void OnNumericChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_syncingRotation) return;
        _syncingRotation = true;
        RotationSlider.Value = (double)(e.NewValue ?? 0m);
        _syncingRotation = false;
        ApplyRotation((double)(e.NewValue ?? 0m));
    }

    private void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        RotationSlider.Value = 0;
    }

    private void ApplyRotation(double deg)
    {
        var js = $"setRotation({deg.ToString("F2", CultureInfo.InvariantCulture)})";
        WebView.InvokeScript(js);
    }
}
