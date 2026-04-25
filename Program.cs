using System.Runtime.InteropServices;
using WinScrobb;

// Single-instance guard — second launch exits immediately
using var mutex = new Mutex(initiallyOwned: true, "Global\\WinScrobb-SingleInstance", out bool isNew);
if (!isNew)
{
    // Optionally flash the tray icon of the running instance (best-effort)
    mutex.Dispose();
    return;
}

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.SystemAware);

Application.Run(new TrayApp());
