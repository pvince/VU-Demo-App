using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static VU1WPF.ClassDialGUI;
using KR_VU1_Server;
using MaterialDesignThemes.Wpf;
using System.Windows.Threading;
using static KR_VU1_Sensors.ClassVUSensors;
using KR_VU1_ConfigurationManager;
using System.Text.RegularExpressions;
using System.Globalization;
using Microsoft.Win32;
using Serilog;
using System.Threading.Tasks;

namespace VU1WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public object FormWindowState { get; private set; }
        List<VU1_Sensor> computerSensors = new List<VU1_Sensor>();
        public ClassConfigurationManager ConfigManager;
        VU1_SensorManager SensorManager;
        VU1_Server DialServer;
        private readonly DialUpdateOrchestrator gDialUpdateOrchestrator = new DialUpdateOrchestrator();
        public List<ClassDialGUI> gDials = new List<ClassDialGUI>();
        public ClassDialGUI gCurrentlySelectedDial = new ClassDialGUI { FriendlyName = "", UID = "" };
        bool gDialUpdatePaused = false;
        const String VU1_Registry_Key = "VU1-Demo-App";
        const String VU1_Registry_Path = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";


        public MainWindow()
        {
            InitializeComponent();

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            // Create logger
            using var log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\KaranovicResearch\VU1-DemoApp\log.txt",
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: 1048576,
                    retainedFileCountLimit: 10)
                .CreateLogger();
            Log.Logger = log;

            // Create configuration manager instance
            ConfigManager = new ClassConfigurationManager();


            // Create instance of VU1 server
            string MasterKey = ConfigManager.GetMasterKey();
            DialServer = new VU1_Server(ConfigManager.GetServerHost(), ConfigManager.GetServerPort(), MasterKey);

            // Initiate sensor manager
            SensorManager = new VU1_SensorManager();
            computerSensors = SensorManager.get_used_sensors();

            // Bind UI and back-end items
            lbDials.ItemsSource = gDials;

            // Bind UI sensor ComboBox to sensor list
            cbDialMetric.ItemsSource = computerSensors;
            cbDialMetricCategory.ItemsSource = SensorManager.UsedSensors;

            // Add build stamp to title
            lblTitle.Content = lblTitle.Content + Properties.Resources.BuildDate.ToString();

            // Add drag
            //cnvsTitleCanvas.MouseDown += delegate { DragMove(); };
            lblTitle.MouseDown += delegate { DragMove(); };

            RefreshDialList();
            //SetAllDialValue(50);

            // Populate server settings UI
            txtServerHost.Text = ConfigManager.GetServerHost();
            txtServerPort.Text = ConfigManager.GetServerPort().ToString();
            SetConnectionStatus(DialServer.GetDialList().Count > 0);

            //Systray icon
            System.Windows.Forms.NotifyIcon notifyIcon;
            notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.DoubleClick += notifyIcon_DoubleClick;
            notifyIcon.Icon = new System.Drawing.Icon("VU1_Icon.ico");
            notifyIcon.Visible = true;

            // Create timer
            float dialUpdatePeriod = ConfigManager.GetDialUpdatePeriod();
            //DispatcherTimer timer = new DispatcherTimer(System.Windows.Threading.DispatcherPriority.Send);
            DispatcherTimer timer = new DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render);
            timer.Tick += TimerTick;
            timer.Interval = TimeSpan.FromSeconds(dialUpdatePeriod);
            timer.Start();

            Log.Information(String.Format("Dials updated every {0} seconds.", dialUpdatePeriod));

            if (RegistryValueExists("HKCU", VU1_Registry_Path, VU1_Registry_Key))
            {
                chRunOnStartup.IsChecked = true;
            }
        }

        private void btnX_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void btnMin_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void btnMinToTray_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
            this.Hide();
        }

        void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
            WindowState = WindowState.Normal;
            Log.Verbose("Systray double click event");
        }


        private void lbDials_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClassDialGUI sel = lbDials.SelectedItem as ClassDialGUI;
            if (sel == null) return;

            gCurrentlySelectedDial = sel;

            txtlSelectedDialName.Text = sel.FriendlyName.ToString();
            lblSelectedDialUID.Content = sel.UID.ToString();
            if (gCurrentlySelectedDial.Sensor != null)
            {
                lblCurrentMetric.Content = String.Format("{0} - {1} - {2}", gCurrentlySelectedDial.Sensor.Name.ToString(), gCurrentlySelectedDial.Sensor.SensorType.ToString(), gCurrentlySelectedDial.Sensor.Identifier.ToString());
                //lblCurrentValue.Content = gCurrentlySelectedDial.Sensor.Value.ToString();
                lblScalingMin.Content = gCurrentlySelectedDial.ScaleMin.ToString();
                lblScalingMax.Content = gCurrentlySelectedDial.ScaleMax.ToString();
                txtMinValue.Text = gCurrentlySelectedDial.ScaleMin.ToString();
                txtMaxValue.Text = gCurrentlySelectedDial.ScaleMax.ToString();
                
            }
            else
            {
                lblCurrentMetric.Content = "";
                lblCurrentValue.Content = "";
                lblCurrentPercent.Content = "";
                lblScalingMin.Content = "";
                lblScalingMax.Content = "";
                txtMinValue.Text = "0";
                txtMaxValue.Text = "100";
            }
            
        }

        private ClassDialGUI CreateGUIDial(String UID, String FriendlyName)
        {
            Log.Debug(String.Format("Adding dial UID:{0} Name:{1}", UID, FriendlyName));
            ClassDialGUI tmpDial = new ClassDialGUI() { FriendlyName = FriendlyName, UID = UID };

            // Try to read sensor identifier from config
            string sensorIdentifier = ConfigManager.GetDialMetric(UID);
            float sensorMin = ConfigManager.GetDialMin(UID);
            float sensorMax = ConfigManager.GetDialMax(UID);
            List<ClassDialThreshold> thresholds = ConfigManager.GetDialThresholds(UID);

            if (sensorIdentifier != "")
            {
                VU1_Sensor tmpSensor = SensorManager.FindSensorByIdentifier(sensorIdentifier);
                if(tmpSensor != null)
                {
                    tmpDial.Sensor = tmpSensor.Sensor;
                    tmpDial.SensorIdentifier = tmpSensor.Sensor.Identifier.ToString();
                    tmpDial.SensorName = tmpSensor.Sensor.Name.ToString();
                    tmpDial.ScaleMin = sensorMin;
                    tmpDial.ScaleMax = sensorMax;
                    tmpDial.Thresholds = thresholds;
                }
            }
            return tmpDial;
        }

        private void RefreshDialList()
        {
            Log.Information("Refreshing dial list");
            DialServer.RefreshDialList();
            List<DialInfo> restDials = DialServer.GetDialList();

            Log.Information("Updatig local dials");
            gDials.Clear();
            foreach (DialInfo dial in restDials)
            {
                ClassDialGUI tmpDial = CreateGUIDial(dial.uid, dial.dial_name);
                gDials.Add(tmpDial);
            }

            Log.Information("Updating GUI dials");
            lbDials.ItemsSource = gDials;
            lbDials.Items.SortDescriptions.Clear();
            lbDials.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("FriendlyName", System.ComponentModel.ListSortDirection.Ascending));
            lbDials.Items.Refresh();

            // Sort dial thresholds
            sortDialThresholds();
        }

        public void PauseDialUpdate()
        {
            gDialUpdatePaused = true;
            btnToggleDialUpdate.Content = new PackIcon { Kind = PackIconKind.Play };
        }

        public void ResumeDialUpdate()
        {
            gDialUpdatePaused = false;
            btnToggleDialUpdate.Content = new PackIcon { Kind = PackIconKind.Pause };

        }

        private void btnRefreshDials_click(object sender, RoutedEventArgs e)
        {
            RefreshDialList();
        }

        private void btnToggleDialUpdate_click(object sender, RoutedEventArgs e)
        {
            if(gDialUpdatePaused)
            {
                ResumeDialUpdate();
            }
            else
            {
                PauseDialUpdate();
            }
        }

        private float ParseMinMaxValue(String value, float def)
        {
            float fValue = def;
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out fValue);
            return fValue;
        }

        private void btnSaveDialSettings_click(object sender, RoutedEventArgs e)
        {
            if (gCurrentlySelectedDial == null)
            {
                return;
            }

            // Prepare data
            string newName = txtlSelectedDialName.Text;
            float minValue = ParseMinMaxValue(txtMinValue.Text, 100);
            float maxValue = ParseMinMaxValue(txtMaxValue.Text, 100);

            // Minimum can not be bigger than maximum
            if (minValue > maxValue)
            {
                float tpm = minValue;
                minValue = maxValue;
                maxValue = tpm;
            }
            
            // Update currently selected sensor
            gCurrentlySelectedDial.FriendlyName = newName;
            gCurrentlySelectedDial.ScaleMin = minValue;
            gCurrentlySelectedDial.ScaleMax = maxValue;

            // API Call to update dial name
            if (DialServer.UpdateDialName(gCurrentlySelectedDial.UID, newName))
            {
                lbDials.Items.SortDescriptions.Clear();
                lbDials.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("FriendlyName", System.ComponentModel.ListSortDirection.Ascending));
                lbDials.Items.Refresh();
            }

            // Update config/UI only if:
            // - valid sensor is selected from the drop-down
            // - valud sensor is stored in config, then update only scaling values
            if (cbDialMetric.SelectedItem != null || gCurrentlySelectedDial.Sensor != null)
            {
                // New sensor is selected
                if (cbDialMetric.SelectedItem != null)
                {
                    //gCurrentlySelectedDial.Sensor = computerSensors[cbDialMetric.SelectedIndex].Sensor;
                    VU1_Sensor selectedSensor = cbDialMetric.SelectedItem as VU1_Sensor;
                    if (selectedSensor != null)
                    {
                        gCurrentlySelectedDial.Sensor = selectedSensor.Sensor;
                        lblCurrentMetric.Content = String.Format("{0} - {1} - {2}", gCurrentlySelectedDial.Sensor.Name.ToString(), gCurrentlySelectedDial.Sensor.SensorType.ToString(), gCurrentlySelectedDial.Sensor.Identifier.ToString());
                    }
                }

                lblCurrentValue.Content = gCurrentlySelectedDial.Sensor.Value.ToString();
                lblScalingMin.Content = gCurrentlySelectedDial.ScaleMin.ToString();
                lblScalingMax.Content = gCurrentlySelectedDial.ScaleMax.ToString();

                // Update config file
                ConfigManager.UpdateDialConfig(gCurrentlySelectedDial, true);
            }
            
        }

        private void cbDialMetricCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDialMetricCategory.SelectedItem == null)
            {
                return;
            }

            List<VU1_Sensor> filtered = new List<VU1_Sensor> { };

            foreach (var sens in computerSensors)
            {

                if (sens.Sensor.SensorType.ToString().Contains(cbDialMetricCategory.SelectedItem.ToString()))
                {
                    filtered.Add(sens);
                }
            }

            cbDialMetric.ItemsSource = filtered;
            cbDialMetric.Items.Refresh();
        }

        private void btnChangeImage_click(object sender, RoutedEventArgs e)
        {
            if (gCurrentlySelectedDial == null || gCurrentlySelectedDial.UID == "")
            {
                return;
            }

            //Create a new instance of openFileDialog
            System.Windows.Forms.OpenFileDialog res = new System.Windows.Forms.OpenFileDialog();

            //Filter
            res.Filter = "Image Files|*.jpg;*.jpeg;*.png";

            //When the user select the file
            if (res.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DialServer.UpdateDialBackgroundImage(gCurrentlySelectedDial.UID, res.FileName);
            }
            
        }

        private void btnSetDialRules_click(object sender, RoutedEventArgs e)
        {
            if (gCurrentlySelectedDial == null || gCurrentlySelectedDial.UID == "")
            {
                return;
            }

            ThresholdsWindow thresholdsWindow = new ThresholdsWindow();
            thresholdsWindow.Owner = this;
            thresholdsWindow.SetMainWindow(this);
            thresholdsWindow.SetConfigManager(ConfigManager);
            thresholdsWindow.ShowDialog();
        }

        private void btnChangeDialColor_click(object sender, RoutedEventArgs e)
        {
            if (gCurrentlySelectedDial == null || gCurrentlySelectedDial.UID == "")
            {
                return;
            }

            SetColorWindow setColorWindow = new SetColorWindow();
            setColorWindow.Owner = this;
            setColorWindow.SetMainWindow(this);
            setColorWindow.Show();
        }

        private void chRunOnSystemStart(object sender, RoutedEventArgs e)
        {

            RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (chRunOnStartup.IsChecked == true)
            {
                rk.SetValue(VU1_Registry_Key, System.Windows.Forms.Application.ExecutablePath);
            }

            else
            {
                rk.DeleteValue(VU1_Registry_Key, false);
            }

        }

        public void Selected_Dial_Change_Color(int red, int green, int blue)
        {
            DialServer.UpdateDialBacklight(gCurrentlySelectedDial.UID, red, green, blue);
        }

        private void btnAbout_click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Show();
        }

        private void SetConnectionStatus(bool connected)
        {
            if (connected)
            {
                lblConnectionStatus.Content = $"Connected to {ConfigManager.GetServerHost()}:{ConfigManager.GetServerPort()}";
                lblConnectionStatus.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                lblConnectionStatus.Content = $"Cannot reach {ConfigManager.GetServerHost()}:{ConfigManager.GetServerPort()}";
                lblConnectionStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void btnReconnect_Click(object sender, RoutedEventArgs e)
        {
            string host = txtServerHost.Text.Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                lblConnectionStatus.Content = "Host cannot be empty";
                lblConnectionStatus.Foreground = System.Windows.Media.Brushes.OrangeRed;
                return;
            }

            if (!int.TryParse(txtServerPort.Text, out int port) || port < 1 || port > 65535)
            {
                lblConnectionStatus.Content = "Port must be 1–65535";
                lblConnectionStatus.Foreground = System.Windows.Media.Brushes.OrangeRed;
                return;
            }

            lblConnectionStatus.Content = "Connecting...";
            lblConnectionStatus.Foreground = System.Windows.Media.Brushes.Gray;

            ConfigManager.SetServerHost(host);
            ConfigManager.SetServerPort(port);
            ConfigManager.SaveConfigFile();

            string masterKey = ConfigManager.GetMasterKey();
            DialServer = new VU1_Server(host, port, masterKey);

            bool ok = DialServer.RefreshDialList();
            SetConnectionStatus(ok);

            if (ok)
            {
                RefreshDialList();
            }
        }


        void NumericTextBoxInput(object sender, TextCompositionEventArgs e)
        {
            var regex = new Regex(@"^[0-9]*(?:\.[0-9]*)?$");

            // Limit lenght to 6 characters
            if (((System.Windows.Controls.TextBox)sender).Text.Length >= 6)
            {
                e.Handled = true;
            }
            // Regex match
            else if (regex.IsMatch(e.Text) && !(e.Text == "." && ((System.Windows.Controls.TextBox)sender).Text.Contains(e.Text)))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }

        void IntegerTextBoxInput(object sender, TextCompositionEventArgs e)
        {
            // Only allow digits, max 5 characters (port max 65535)
            if (((System.Windows.Controls.TextBox)sender).Text.Length >= 5 || !Regex.IsMatch(e.Text, @"^[0-9]$"))
            {
                e.Handled = true;
            }
        }

        public void SaveDialConfig()
        {
            ConfigManager.SaveConfigFile();
        }


        private void SetAllDialValue(int val)
        {
            foreach (ClassDialGUI dial in gDials)
            {
                DialServer.UpdateDialValue(dial.UID, val);
            }
        }

        private async Task RefreshDialMetricAsync(ClassDialGUI dial)
        {
            // Refresh dial sensor value
            if (dial.Sensor != null)
            {
                // Initial dial value
                int dialValue = 0;
                int dialRed = 0;
                int dialGreen = 0;
                int dialBlue = 0;
                bool bBacklightUpdate = false;

                float fSensorValue = await Task.Run(() =>
                {
                    dial.Sensor.Hardware.Update();
                    return dial.Sensor.Value ?? 0;
                }).ConfigureAwait(false);

                dialValue = DialComputationEngine.ComputeDialValuePercent(dial.ScaleMin, dial.ScaleMax, fSensorValue);

                // Update backlight based on defined thresholds
                if (dial.Thresholds != null && dial.Thresholds.Count > 0)
                {
                    ClassDialThreshold match = DialComputationEngine.ResolveThresholdColor(dial.Thresholds, dialValue);

                    if (match != null)
                    {
                        dialRed = match.BacklightRed;
                        dialGreen = match.BacklightGreen;
                        dialBlue = match.BacklightBlue;
                        bBacklightUpdate = true;
                    }
                }


                string selectedSensorIdentifier = gCurrentlySelectedDial.Sensor?.Identifier.ToString() ?? String.Empty;
                string dialSensorIdentifier = dial.Sensor.Identifier.ToString();

                Log.Verbose(String.Format("Dial:{0} set to {1}% [Sensor: {2}] - Raw value: {3}", dial.UID, dialValue, dial.Sensor.Identifier, fSensorValue));
                await Task.Run(() =>
                {
                    DialServer.UpdateDialValue(dial.UID, dialValue);

                    if (bBacklightUpdate)
                    {
                        DialServer.UpdateDialBacklight(dial.UID, dialRed, dialGreen, dialBlue);
                    }
                }).ConfigureAwait(false);

                if (selectedSensorIdentifier == dialSensorIdentifier)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        lblCurrentPercent.Content = String.Format("{0}%", dialValue);
                        lblCurrentValue.Content = String.Format("[{0:0.000}]", fSensorValue);
                    });
                }
            }
        }

        private async Task ProcessDialUpdatesAsync()
        {
            List<ClassDialGUI> dialsToUpdate = gDials.Where(d => d.Sensor != null).ToList();
            foreach (ClassDialGUI dial in dialsToUpdate)
            {
                await RefreshDialMetricAsync(dial).ConfigureAwait(false);
            }
        }

        public void sortDialThresholds()
        {
            foreach (ClassDialGUI dial in gDials)
            {
                dial.Thresholds = dial.Thresholds.OrderBy(item => item.Threshold).ToList();
            }
        }


        private void mainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Reset each dial
            foreach (ClassDialGUI dial in gDials)
            {
                DialServer.UpdateDialValue(dial.UID, 0);
                DialServer.UpdateDialBacklight(dial.UID, 0, 0, 0);
            }
        }

        public static bool RegistryValueExists(string hive_HKLM_or_HKCU, string registryRoot, string valueName)
        {
            RegistryKey root;
            switch (hive_HKLM_or_HKCU.ToUpper())
            {
                case "HKLM":
                    root = Registry.LocalMachine.OpenSubKey(registryRoot, false);
                    break;
                case "HKCU":
                    root = Registry.CurrentUser.OpenSubKey(registryRoot, false);
                    break;
                default:
                    throw new System.InvalidOperationException("parameter registryRoot must be either \"HKLM\" or \"HKCU\"");
            }

            return root.GetValue(valueName) != null;
        }


        private void TimerTick(object sender, EventArgs e)
        {
            // Don't run if pause has been requested
            if (gDialUpdatePaused) return;

            if (!gDialUpdateOrchestrator.TryRun(ProcessDialUpdatesAsync))
            {
                Log.Verbose("Skipping tick because previous update is still running.");
            }
        }


        static void OnProcessExit(object sender, EventArgs e)
        {
            Log.CloseAndFlush();
        }

    }
}
