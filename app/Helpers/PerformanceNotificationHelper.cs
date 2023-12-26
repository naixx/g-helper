using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Timers;
using GHelper.UI;
using Timer = System.Timers.Timer;

namespace GHelper.Helpers;

public static class PerformanceNotificationHelper
{
    private static readonly PerformanceCounter _cpuCounter = new("Processor Information", "% Processor Performance",
        "_Total");

    // private static readonly Bitmap bitmap = new(Program.trayIcon.Icon!.Width, Program.trayIcon.Icon.Height);
    private static readonly Bitmap bitmap = new(48, 48);
    private static readonly Graphics g = Graphics.FromImage(bitmap);
    private static readonly Brush b = new SolidBrush(Color.White);
    private static readonly Font f = new(SystemFonts.DefaultFont.FontFamily, 9);
    private static readonly Font f2 = new(SystemFonts.DefaultFont.FontFamily, 6);
    private static readonly ManagementObjectSearcher mos = new("SELECT * FROM Win32_Processor");
    private static readonly ManagementObject obj = mos.Get().OfType<ManagementObject>().FirstOrDefault();

    private static readonly object syncRoot = new();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(nint handle);

    public static void SetTimer()
    {
        var sensorTimer = new Timer(1000);
        sensorTimer.Elapsed += OnTimedEvent;
        sensorTimer.Enabled = true;
    }

    private static void OnTimedEvent(object? sender, ElapsedEventArgs e)
    {
        RefreshSensors();
    }

    private static async void RefreshSensors(bool force = false)
    {
        var cpuTemp = "";
        var cpu = "";
        var gpuTemp = "";
        var gpu = "";
        var battery = "";
        var charge = "";
        var cpuFreq = "";

        HardwareControl.ReadSensors();

        if (HardwareControl.cpuTemp > 0)
        {
            cpuTemp = ": " + Math.Round((decimal)HardwareControl.cpuTemp) + "°C";
            cpu = Math.Round((decimal)HardwareControl.cpuTemp) + "°";
        }

        if (HardwareControl.gpuTemp > 0)
        {
            gpuTemp = $": {HardwareControl.gpuTemp}°C";
            gpu = HardwareControl.gpuTemp + "°";
        }

        var clockSpeed = Convert.ToDouble(obj["MaxClockSpeed"]);
        cpuFreq = double.Round(_cpuCounter.NextValue() / 100 * clockSpeed).ToString();


        var trayTip = "CPU" + cpuTemp + " " + cpuFreq + "MHz " + HardwareControl.cpuFan;

        var c = Color.FromArgb(100, RForm.colorStandard);
        lock (syncRoot)
        {
            g.Clear(c);
            g.DrawString(cpu, f, b, 0, -7);
            g.DrawString(cpuFreq, f2, b, 0, 26);


            var hicon = bitmap.GetHicon();
            var newIcon = Icon.FromHandle(hicon);

            Program.trayIcon.Icon = (Icon)newIcon.Clone();
            newIcon.Dispose();
            DestroyIcon(hicon);
        }
    }
}