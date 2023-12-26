using GHelper.Battery;
using GHelper.Display;
using GHelper.Gpu;
using GHelper.Helpers;
using GHelper.Input;
using GHelper.Mode;
using GHelper.Peripherals;
using GHelper.UI;
using Microsoft.Win32;
using Ryzen;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Timers;
using static NativeMethods;

namespace GHelper
{

    static class Program
    {
        public static NotifyIcon trayIcon = new NotifyIcon
        {
            Text = "G-Helper",
            Icon = Properties.Resources.standard,
            Visible = true 
        };

        public static AsusACPI acpi;

        public static SettingsForm settingsForm = new SettingsForm();

        public static ModeControl modeControl = new ModeControl();
        public static GPUModeControl gpuControl = new GPUModeControl(settingsForm);
        public static ScreenControl screenControl = new ScreenControl();
        public static ClamshellModeControl clamshellControl = new ClamshellModeControl();

        public static ToastForm toast = new ToastForm();

        public static IntPtr unRegPowerNotify;

        private static long lastAuto;
        private static long lastTheme;

        public static InputDispatcher? inputDispatcher;

        private static PowerLineStatus isPlugged = SystemInformation.PowerStatus.PowerLineStatus;
        private static PerformanceCounter _cpuCounter = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total");


        // The main entry point for the application
        public static void Main(string[] args)
        {

            string action = "";
            if (args.Length > 0) action = args[0];

            string language = AppConfig.GetString("language");

            if (language != null && language.Length > 0)
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            else
            {
                var culture = CultureInfo.CurrentUICulture;
                if (culture.ToString() == "kr") culture = CultureInfo.GetCultureInfo("ko");
                Thread.CurrentThread.CurrentUICulture = culture;
            }

            ProcessHelper.CheckAlreadyRunning();

            Logger.WriteLine("------------");
            Logger.WriteLine("App launched: " + AppConfig.GetModel() + " :" + Assembly.GetExecutingAssembly().GetName().Version.ToString() + CultureInfo.CurrentUICulture + (ProcessHelper.IsUserAdministrator() ? "." : ""));

            acpi = new AsusACPI();

            if (!acpi.IsConnected() && AppConfig.IsASUS())
            {
                DialogResult dialogResult = MessageBox.Show(Properties.Strings.ACPIError, Properties.Strings.StartupError, MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo("https://www.asus.com/support/FAQ/1047338/") { UseShellExecute = true });
                }

                Application.Exit();
                return;
            }

            Application.EnableVisualStyles();

            HardwareControl.RecreateGpuControl();
            RyzenControl.Init();

            trayIcon.MouseClick += TrayIcon_MouseClick;
            
            inputDispatcher = new InputDispatcher();

            settingsForm.InitAura();
            settingsForm.InitMatrix();

            gpuControl.InitXGM();

            SetAutoModes(init : true);
            SetTimer();

            // Subscribing for system power change events
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
            SystemEvents.SessionEnding += SystemEvents_SessionEnding;

            clamshellControl.RegisterDisplayEvents();
            clamshellControl.ToggleLidAction();

            // Subscribing for monitor power on events
            PowerSettingGuid settingGuid = new NativeMethods.PowerSettingGuid();
            unRegPowerNotify = NativeMethods.RegisterPowerSettingNotification(settingsForm.Handle, settingGuid.ConsoleDisplayState, NativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE);


            Task task = Task.Run((Action)PeripheralsProvider.DetectAllAsusMice);
            PeripheralsProvider.RegisterForDeviceEvents();

            if (Environment.CurrentDirectory.Trim('\\') == Application.StartupPath.Trim('\\') || action.Length > 0)
            {
                SettingsToggle(false);
            }

            switch (action)
            {
                case "cpu":
                    Startup.ReScheduleAdmin();
                    settingsForm.FansToggle();
                    break;
                case "gpu":
                    Startup.ReScheduleAdmin();
                    settingsForm.FansToggle(1);
                    break;
                case "gpurestart":
                    gpuControl.RestartGPU(false);
                    break;
                case "services":
                    settingsForm.extraForm = new Extra();
                    settingsForm.extraForm.Show();
                    settingsForm.extraForm.ServiesToggle();
                    break;
                case "uv":
                    Startup.ReScheduleAdmin();
                    settingsForm.FansToggle(2);
                    modeControl.SetRyzen();
                    break;
                default:
                    Startup.StartupCheck();
                    break;
            }

            Application.Run();

        }

        private static void SetTimer()
        {
            var sensorTimer = new System.Timers.Timer(1000);
            sensorTimer.Elapsed += OnTimedEvent;
            sensorTimer.Enabled = true;
        }

        private static void OnTimedEvent(object? sender, ElapsedEventArgs e)
        {
            RefreshSensors();
        }
        static Bitmap bitmap = new Bitmap(Program.trayIcon.Icon.Size.Width, Program.trayIcon.Icon.Size.Height);
        static Graphics g = Graphics.FromImage(bitmap);
        static Brush b = new SolidBrush(Color.White);
        static Font f = new Font(SystemFonts.DefaultFont.FontFamily, 9);
        static  Font f2 = new Font(SystemFonts.DefaultFont.FontFamily, 6);
        static ManagementObjectSearcher mos = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
        static ManagementObject obj = mos.Get().OfType<ManagementObject>().FirstOrDefault();

        private static object syncRoot = new Object();
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);
        public static async void RefreshSensors(bool force = false)
        {
            string cpuTemp = "";
            string cpu = "";
            string gpuTemp = "";
            string gpu = "";
            string battery = "";
            string charge = "";
            string cpuFreq = "";

            HardwareControl.ReadSensors();

            if (HardwareControl.cpuTemp > 0)
            {
                cpuTemp = ": " + Math.Round((decimal)HardwareControl.cpuTemp).ToString() + "°C";
                cpu = Math.Round((decimal)HardwareControl.cpuTemp).ToString() + "°";
            }

            if (HardwareControl.gpuTemp > 0)
            {
                gpuTemp = $": {HardwareControl.gpuTemp}°C";
                gpu = HardwareControl.gpuTemp + "°";
            }

            double clockSpeed = 0;
            //foreach (ManagementObject obj in mos.Get())
            //{
                  clockSpeed = Convert.ToDouble(obj["MaxClockSpeed"]);
                cpuFreq += " " + clockSpeed;
            //}
           // GC.WaitForPendingFinalizers();
              cpuFreq = Double.Round((_cpuCounter.NextValue() / 100) * clockSpeed).ToString();

          
              string trayTip = "CPU" + cpuTemp + " " + cpuFreq + "MHz " + HardwareControl.cpuFan;

              var c = Color.FromArgb(100, RForm.colorStandard);
            lock (syncRoot) { 
              g.Clear(c);
              g.DrawString(cpu, f, b, 0, -7);
              //g.DrawString((Program.acpi.GetFan(AsusFan.CPU)*100).ToString(), f2, b, -5, 26);
              g.DrawString(cpuFreq, f2, b, 0, 26);

              // g.DrawString(gpu, f, b, -5, 20);
              var hicon = bitmap.GetHicon();
                var newIcon = Icon.FromHandle(hicon);
             //Program.trayIcon.Icon.Dispose();
                //Program.trayIcon.Icon.
              Program.trayIcon.Icon = (Icon) newIcon.Clone();
                newIcon.Dispose();
                DestroyIcon(hicon);
            }

        }

        private static void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            gpuControl.StandardModeFix();
            BatteryControl.AutoBattery();
        }

        private static void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLogon || e.Reason == SessionSwitchReason.SessionUnlock)
            {
                Logger.WriteLine("Session:" + e.Reason.ToString());
                screenControl.AutoScreen();
            }
        }

        static void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {

            if (Math.Abs(DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastTheme) < 2000) return;

            switch (e.Category)
            {
                case UserPreferenceCategory.General:
                    bool changed = settingsForm.InitTheme();
                    if (changed)
                    {
                        Debug.WriteLine("Theme Changed");
                        lastTheme = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    }

                    if (settingsForm.fansForm is not null && settingsForm.fansForm.Text != "")
                        settingsForm.fansForm.InitTheme();

                    if (settingsForm.extraForm is not null && settingsForm.extraForm.Text != "")
                        settingsForm.extraForm.InitTheme();

                    if (settingsForm.updatesForm is not null && settingsForm.updatesForm.Text != "")
                        settingsForm.updatesForm.InitTheme();

                    if (settingsForm.matrixForm is not null && settingsForm.matrixForm.Text != "")
                        settingsForm.matrixForm.InitTheme();
                    break;
            }
        }



        public static void SetAutoModes(bool powerChanged = false, bool init = false)
        {

            if (Math.Abs(DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastAuto) < 3000) return;
            lastAuto = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            isPlugged = SystemInformation.PowerStatus.PowerLineStatus;
            Logger.WriteLine("AutoSetting for " + isPlugged.ToString());

            inputDispatcher.Init();

            modeControl.AutoPerformance(powerChanged);

            bool switched = gpuControl.AutoGPUMode();

            if (!switched)
            {
                gpuControl.InitGPUMode();
                screenControl.AutoScreen();
            }

            BatteryControl.AutoBattery(init);

            settingsForm.AutoKeyboard();
            settingsForm.matrixControl.SetMatrix(true);
        }

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {

            if (e.Mode == PowerModes.Suspend)
            {
                Logger.WriteLine("Power Mode Changed:" + e.Mode.ToString());
                gpuControl.StandardModeFix();
            }

            if (SystemInformation.PowerStatus.PowerLineStatus == isPlugged) return;
            SetAutoModes(true);
        }

        public static void SettingsToggle(bool checkForFocus = true, bool trayClick = false)
        {
            if (settingsForm.Visible)
            {
                // If helper window is not on top, this just focuses on the app again
                // Pressing the ghelper button again will hide the app
                if (checkForFocus && !settingsForm.HasAnyFocus(trayClick))
                {
                    settingsForm.ShowAll();
                }
                else
                {
                    settingsForm.HideAll();
                }
            }
            else
            {

                settingsForm.Left = Screen.FromControl(settingsForm).WorkingArea.Width - 10 - settingsForm.Width;
                settingsForm.Top = Screen.FromControl(settingsForm).WorkingArea.Height - 10 - settingsForm.Height;

                settingsForm.Show();
                settingsForm.Activate();

                settingsForm.Left = Screen.FromControl(settingsForm).WorkingArea.Width - 10 - settingsForm.Width;
                settingsForm.Top = Screen.FromControl(settingsForm).WorkingArea.Height - 10 - settingsForm.Height;

                settingsForm.VisualiseGPUMode();
            }
        }

        static void TrayIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                SettingsToggle(trayClick: true);

        }



        static void OnExit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            PeripheralsProvider.UnregisterForDeviceEvents();
            clamshellControl.UnregisterDisplayEvents();
            NativeMethods.UnregisterPowerSettingNotification(unRegPowerNotify);
            Application.Exit();
        }


    }
}