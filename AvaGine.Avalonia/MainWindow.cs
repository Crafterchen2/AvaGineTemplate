using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaGine.Avalonia.Controls;

namespace AvaGine.Avalonia;

public class MainWindow : Window
{
    private readonly EvergineControl evCon;

    public MainWindow()
    {
        Title = "AvaGine";
        Width = 1280;
        Height = 720;
        evCon = new EvergineControl();
        {
            evCon.IsReadyChanged += status => AvaloniaApp.Current?.EnableEngineLoop = status;
        }
        Content = evCon;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e); // Required.
        evCon.Unload();
    }
}