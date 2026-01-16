using System.Windows;
using System.Windows.Threading;

namespace MSCC.Tests;

/// <summary>
/// Basisklasse für WPF UI Tests.
/// Stellt sicher, dass Tests im STA-Thread laufen.
/// </summary>
public abstract class WpfTestBase
{
    protected Dispatcher? TestDispatcher { get; private set; }

    [SetUp]
    public void BaseSetUp()
    {
        // Für jeden Test einen neuen Dispatcher erstellen falls nötig
    }

    /// <summary>
    /// Führt eine Aktion im UI-Thread aus.
    /// </summary>
    protected void RunOnUIThread(Action action)
    {
        Exception? exception = null;

        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            // Bereits im STA-Thread
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }
        else
        {
            // Neuen STA-Thread erstellen
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(30));
        }

        if (exception != null)
            throw new Exception("UI Thread Exception", exception);
    }

    /// <summary>
    /// Führt eine Funktion im UI-Thread aus und gibt das Ergebnis zurück.
    /// </summary>
    protected T RunOnUIThread<T>(Func<T> func)
    {
        T result = default!;
        RunOnUIThread(() => result = func());
        return result;
    }

    /// <summary>
    /// Verarbeitet ausstehende UI-Events.
    /// </summary>
    protected void DoEvents()
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new DispatcherOperationCallback(_ =>
                {
                    frame.Continue = false;
                    return null;
                }),
                null);
            Dispatcher.PushFrame(frame);
        }
    }
}
