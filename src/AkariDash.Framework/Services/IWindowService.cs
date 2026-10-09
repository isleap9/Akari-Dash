using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace AkariDash.Framework.Services;

/// <summary>
/// Window management: creating secondary windows, AppWindow operations
/// (title, icon, always-on-top, size, centering) and window handle access.
/// </summary>
public interface IWindowService
{
    /// <summary>Creates and activates a new window hosting the given content.</summary>
    Window CreateWindow(string title, UIElement content, double width = 1000, double height = 700);

    /// <summary>Gets the AppWindow for a window, or null when unavailable.</summary>
    AppWindow? GetAppWindow(Window window);

    /// <summary>Gets the Win32 HWND for a window.</summary>
    IntPtr GetWindowHandle(Window window);

    /// <summary>Sets the window icon from a file path. Returns false on failure.</summary>
    bool SetIcon(Window window, string iconPath);

    /// <summary>Toggles always-on-top. Returns false when not supported.</summary>
    bool SetAlwaysOnTop(Window window, bool isAlwaysOnTop);

    /// <summary>Resizes the window.</summary>
    bool SetSize(Window window, int width, int height);

    /// <summary>Centers the window on its display.</summary>
    void Center(Window window);

    /// <summary>Activates / brings the window to the foreground.</summary>
    void Activate(Window window);

    /// <summary>Closes a window.</summary>
    void Close(Window window);
}

public sealed class WindowService : IWindowService
{
    private readonly HashSet<Window> _windows = [];

    public Window CreateWindow(string title, UIElement content, double width = 1000, double height = 700)
    {
        var window = new Window();
        window.Title = title;
        window.Content = content;
        window.Activate();

        var appWindow = GetAppWindow(window);
        if (appWindow is not null)
        {
            appWindow.Resize(new SizeInt32((int)width, (int)height));
        }

        _windows.Add(window);
        return window;
    }

    public AppWindow? GetAppWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    public IntPtr GetWindowHandle(Window window)
        => WinRT.Interop.WindowNative.GetWindowHandle(window);

    public bool SetIcon(Window window, string iconPath)
    {
        if (!File.Exists(iconPath))
        {
            return false;
        }

        var appWindow = GetAppWindow(window);
        if (appWindow is null)
        {
            return false;
        }

        appWindow.SetIcon(iconPath);
        return true;
    }

    public bool SetAlwaysOnTop(Window window, bool isAlwaysOnTop)
    {
        var appWindow = GetAppWindow(window);
        if (appWindow?.Presenter is not OverlappedPresenter presenter)
        {
            return false;
        }

        presenter.IsAlwaysOnTop = isAlwaysOnTop;
        return true;
    }

    public bool SetSize(Window window, int width, int height)
    {
        var appWindow = GetAppWindow(window);
        if (appWindow is null)
        {
            return false;
        }

        appWindow.Resize(new SizeInt32(width, height));
        return true;
    }

    public void Center(Window window)
    {
        var appWindow = GetAppWindow(window);
        if (appWindow is null)
        {
            return;
        }

        var windowId = appWindow.Id;
        var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
        if (area is null)
        {
            return;
        }

        var workArea = area.WorkArea;
        var x = workArea.X + (workArea.Width - appWindow.Size.Width) / 2;
        var y = workArea.Y + (workArea.Height - appWindow.Size.Height) / 2;
        appWindow.Move(new PointInt32(Math.Max(0, x), Math.Max(0, y)));
    }

    public void Activate(Window window) => window.Activate();

    public void Close(Window window)
    {
        _windows.Remove(window);
        window.Close();
    }
}
