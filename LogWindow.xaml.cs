using System.Windows;

namespace GuessWho;

/// <summary>
/// Okno konsoli logów wyświetlające aktywność aplikacji w czasie rzeczywistym.
/// </summary>
public partial class LogWindow : Window
{
    public LogWindow()
    {
        InitializeComponent();

        foreach (string log in AppLogger.GetAll())
        {
            logList.Items.Add(log);
        }

        AppLogger.LogAdded += OnLogAdded;
    }

    private void OnLogAdded(string entry)
    {
        Dispatcher.InvokeAsync(() =>
        {
            logList.Items.Add(entry);
            logList.ScrollIntoView(logList.Items[logList.Items.Count - 1]);
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        AppLogger.LogAdded -= OnLogAdded;
        base.OnClosed(e);
    }
}
