using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace MSCC.Tests;

/// <summary>
/// Hilfsklasse für UI-Test-Operationen.
/// </summary>
public static class UITestHelper
{
    /// <summary>
    /// Findet ein Element im visuellen Baum nach Name.
    /// </summary>
    public static T? FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
    {
        if (parent == null) return null;

        T? foundChild = null;
        int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < childrenCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild)
            {
                if (child is FrameworkElement fe && fe.Name == childName)
                {
                    foundChild = typedChild;
                    break;
                }
            }

            foundChild = FindChild<T>(child, childName);
            if (foundChild != null) break;
        }

        return foundChild;
    }

    /// <summary>
    /// Findet alle Elemente eines Typs im visuellen Baum.
    /// </summary>
    public static List<T> FindAllChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var results = new List<T>();
        if (parent == null) return results;

        int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < childrenCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild)
                results.Add(typedChild);

            results.AddRange(FindAllChildren<T>(child));
        }

        return results;
    }

    /// <summary>
    /// Simuliert einen Button-Klick.
    /// </summary>
    public static void ClickButton(Button button)
    {
        if (button.Command != null && button.Command.CanExecute(button.CommandParameter))
        {
            button.Command.Execute(button.CommandParameter);
        }
        else
        {
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }
    }

    /// <summary>
    /// Setzt Text in eine TextBox.
    /// </summary>
    public static void SetText(TextBox textBox, string text)
    {
        textBox.Text = text;
        textBox.RaiseEvent(new RoutedEventArgs(TextBox.TextChangedEvent));
    }

    /// <summary>
    /// Wählt ein Element in einer ListBox aus.
    /// </summary>
    public static void SelectItem(Selector selector, int index)
    {
        if (index >= 0 && index < selector.Items.Count)
        {
            selector.SelectedIndex = index;
            selector.RaiseEvent(new RoutedEventArgs(Selector.SelectionChangedEvent));
        }
    }

    /// <summary>
    /// Simuliert einen Menü-Klick.
    /// </summary>
    public static void ClickMenuItem(MenuItem menuItem)
    {
        menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
    }

    /// <summary>
    /// Prüft ob ein Element sichtbar ist.
    /// </summary>
    public static bool IsVisible(UIElement element)
    {
        return element.Visibility == Visibility.Visible;
    }

    /// <summary>
    /// Wartet auf eine Bedingung mit Timeout.
    /// </summary>
    public static bool WaitFor(Func<bool> condition, int timeoutMs = 5000)
    {
        var start = DateTime.Now;
        while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            Thread.Sleep(50);
        }
        return condition();
    }
}
