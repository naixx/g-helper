using GHelper.UI;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace GHelper.Helpers
{
    // Live CPU temperature and effective clock drawn on the tray icon.
    // Runs on the UI thread via a WinForms timer, so there is no sensor polling from a
    // background thread and no second poller next to SettingsForm.sensorTimer: the
    // temperature goes through the 2-second cache in HardwareControl.GetCPUTemp and the
    // clock comes from a performance counter.
    public static class TrayMonitor
    {
        const int IntervalMs = 1000;
        const int MaxFailures = 5;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool DestroyIcon(IntPtr handle);

        static System.Windows.Forms.Timer? timer;
        static Bitmap? bitmap;
        static Graphics? graphics;
        static Font? tempFont, clockFont;
        static float scale = 1; // layout below was tuned for a 32 px icon at 100% DPI
        static readonly Brush textBrush = new SolidBrush(Color.White);

        // "% Processor Performance" x base clock is how Task Manager computes the current speed
        static PerformanceCounter? perfCounter;
        static double maxClockMhz; // Win32_Processor.MaxClockSpeed, 0 until read
        static (string temp, string clock) last;
        static int failures;

        public static void Start()
        {
            if (timer is not null || Program.trayIcon is null) return;

            var size = SystemInformation.IconSize;
            scale = size.Height / 32f;
            bitmap = new Bitmap(size.Width, size.Height);
            graphics = Graphics.FromImage(bitmap);
            tempFont = new Font(SystemFonts.DefaultFont.FontFamily, 6 * scale);
            clockFont = new Font(SystemFonts.DefaultFont.FontFamily, 4 * scale);

            try
            {
                perfCounter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", true);
                perfCounter.NextValue(); // the first sample is always 0
            }
            catch (Exception ex)
            {
                Logger.WriteLine("TrayMonitor: CPU performance counter unavailable: " + ex.Message);
                perfCounter = null;
            }

            Task.Run(ReadMaxClock);

            timer = new System.Windows.Forms.Timer { Interval = IntervalMs };
            timer.Tick += (_, _) => Refresh();
            timer.Start();
        }

        public static void Stop()
        {
            timer?.Dispose();
            timer = null;

            perfCounter?.Dispose();
            perfCounter = null;

            graphics?.Dispose();
            bitmap?.Dispose();
            tempFont?.Dispose();
            clockFont?.Dispose();
            graphics = null;
            bitmap = null;
            tempFont = null;
            clockFont = null;
        }

        static void ReadMaxClock()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor");
                using var cpus = searcher.Get();
                foreach (ManagementObject cpu in cpus)
                    using (cpu)
                    {
                        maxClockMhz = Convert.ToDouble(cpu["MaxClockSpeed"]);
                        return;
                    }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("TrayMonitor: Win32_Processor.MaxClockSpeed unavailable: " + ex.Message);
            }
        }

        static void Refresh()
        {
            try
            {
                Draw();
                failures = 0;
            }
            catch (Exception ex)
            {
                Logger.WriteLine("TrayMonitor: " + ex.Message);
                if (++failures >= MaxFailures)
                {
                    Logger.WriteLine("TrayMonitor: too many failures, stopping");
                    Stop();
                }
            }
        }

        static void Draw()
        {
            if (Program.trayIcon is null || bitmap is null || graphics is null || tempFont is null || clockFont is null) return;

            string temp = "";
            float? celsius = Program.acpi is null ? null : HardwareControl.GetCPUTemp();
            if (celsius > 0)
            {
                double value = TempHelper.IsFahrenheit ? TempHelper.CelsiusToFahrenheit(celsius.Value) : celsius.Value;
                temp = Math.Round(value) + "°";
            }

            string clock = "";
            if (perfCounter is not null && maxClockMhz > 0)
            {
                double mhz = perfCounter.NextValue() / 100 * maxClockMhz;
                if (mhz > 0) clock = Math.Round(mhz).ToString();
            }

            if ((temp, clock) == last) return;
            last = (temp, clock);

            graphics.Clear(Color.FromArgb(100, RForm.colorStandard));
            graphics.DrawString(temp, tempFont, textBrush, 0, -4 * scale);
            graphics.DrawString(clock, clockFont, textBrush, 0, 18 * scale);

            Icon newIcon;
            IntPtr hIcon = bitmap.GetHicon();
            try
            {
                using var borrowed = Icon.FromHandle(hIcon); // does not own the handle
                newIcon = (Icon)borrowed.Clone();            // this copy does
            }
            finally
            {
                DestroyIcon(hIcon);
            }

            Icon? oldIcon = Program.trayIcon.Icon;
            Program.trayIcon.Icon = newIcon;
            oldIcon?.Dispose();
        }
    }
}
