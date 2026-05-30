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
using Serilog.Events;
using System.Threading.Tasks;

namespace VU1WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<VU1_Sensor> computerSensors = new List<VU1_Sensor>();
        public ClassConfigurationManager ConfigManager;
        VU1_SensorManager SensorManager;
        VU1_Server DialServer;
        private readonly DialUpdateOrchestrator gDialUpdateOrchestrator = new DialUpdateOrchestrator();
        public List<ClassDialGUI> gDials = new List<ClassDialGUI>();
        public ClassDialGUI gCurrentlySelectedDial = new ClassDialGUI { FriendlyName = "", UID = "" };
        private float? gLastValidMetricValue;
        private readonly object gWarningThrottleLock = new object();
        private readonly Dictionary<string, DateTime> gWarningLastLoggedAtUtc = new Dictionary<string, DateTime>();
        private static readonly TimeSpan WarningThrottleInterval = TimeSpan.FromSeconds(15);
        bool gDialUpdatePaused = false;
        const String VU1_Registry_Key = "VU1-Demo-App";
        const String VU1_Registry_Path = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";


        public MainWindow()
        {
            InitializeComponent();

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            // Create configuration manager instance
            ConfigManager = new ClassConfigurationManager();

            // Create logger from configuration and keep it alive until process exit.
            LogEventLevel configuredLevel = ParseLogLevel(ConfigManager.GetLogLevel(), LogEventLevel.Information);
            bool diagnosticsMode = ConfigManager.IsDiagnosticsModeEnabled();
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(configuredLevel)
                .WriteTo.Console(restrictedToMinimumLevel: configuredLevel)
                .WriteTo.File(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\KaranovicResearch\VU1-DemoApp\log.txt",
                    restrictedToMinimumLevel: configuredLevel,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: 1048576,
                    retainedFileCountLimit: diagnosticsMode ? 20 : 10)
                .CreateLogger();


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
            Log.Information("Client log level set to {LogLevel}. Diagnostics mode: {DiagnosticsMode}", configuredLevel, diagnosticsMode);

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

        void notifyIcon_DoubleClick(object? sender, EventArgs e)
        {
            this.Show();
            WindowState = WindowState.Normal;
            Log.Verbose("Systray double click event");
        }


        private void lbDials_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbDials.SelectedItem is not ClassDialGUI sel)
            {
                return;
            }

            gCurrentlySelectedDial = sel;
            txtlSelectedDialName.Text = sel.FriendlyName.ToString();
            lblSelectedDialUID.Content = sel.UID.ToString();

            UpdateSelectedDialDisplay();
        }

        private void UpdateSelectedDialDisplay()
        {
            HideScalingValidationError();
            var currentSensor = gCurrentlySelectedDial.Sensor;

            if (currentSensor != null)
            {
                lblCurrentMetric.Content = String.Format("{0} - {1} - {2}", currentSensor.Name.ToString(), currentSensor.SensorType.ToString(), currentSensor.Identifier.ToString());
                //lblCurrentValue.Content = gCurrentlySelectedDial.Sensor.Value.ToString();
                lblScalingMin.Content = gCurrentlySelectedDial.ScaleMin.ToString();
                lblScalingMax.Content = gCurrentlySelectedDial.ScaleMax.ToString();
                txtMinValue.Text = gCurrentlySelectedDial.ScaleMin.ToString();
                txtMaxValue.Text = gCurrentlySelectedDial.ScaleMax.ToString();

                lblCurrentValue.Content = currentSensor.Value?.ToString() ?? "";
                SetMetricStatusForCurrentSensor(currentSensor);
                brdSensorUnavailable.Visibility = Visibility.Collapsed;
                txtUnavailableSensorMessage.Text = String.Empty;

                string sensorTypeStr = currentSensor.SensorType.ToString();
                cbDialMetricCategory.SelectedItem = sensorTypeStr;
                // cbDialMetricCategory_SelectionChanged fires synchronously and repopulates cbDialMetric.ItemsSource
                cbDialMetric.SelectedItem = cbDialMetric.Items
                    .OfType<VU1_Sensor>()
                    .FirstOrDefault(s => s.Sensor.Identifier.ToString() == currentSensor.Identifier.ToString());
            }
            else
            {
                cbDialMetricCategory.SelectedItem = null;
                cbDialMetric.SelectedItem = null;

                if (gCurrentlySelectedDial.HasConfiguredSensorBinding)
                {
                    lblCurrentMetric.Content = $"Unavailable - {gCurrentlySelectedDial.ConfiguredSensorIdentifier}";
                    lblScalingMin.Content = gCurrentlySelectedDial.ScaleMin.ToString(CultureInfo.InvariantCulture);
                    lblScalingMax.Content = gCurrentlySelectedDial.ScaleMax.ToString(CultureInfo.InvariantCulture);
                    txtMinValue.Text = gCurrentlySelectedDial.ScaleMin.ToString(CultureInfo.InvariantCulture);
                    txtMaxValue.Text = gCurrentlySelectedDial.ScaleMax.ToString(CultureInfo.InvariantCulture);
                    SetMetricStatusUnavailable("Binding needs repair");
                    brdSensorUnavailable.Visibility = Visibility.Visible;
                    txtUnavailableSensorMessage.Text = $"Sensor unavailable. The saved binding is still preserved so you can recover it later: {gCurrentlySelectedDial.ConfiguredSensorIdentifier}";
                }
                else
                {
                    lblCurrentMetric.Content = "";
                    lblScalingMin.Content = "";
                    lblScalingMax.Content = "";
                    txtMinValue.Text = "0";
                    txtMaxValue.Text = "100";
                    SetMetricStatusNotConfigured();
                    brdSensorUnavailable.Visibility = Visibility.Collapsed;
                    txtUnavailableSensorMessage.Text = String.Empty;
                }

                lblCurrentValue.Content = "";
                lblCurrentPercent.Content = "";
            }
        }

        private static string GetStatusTimestamp()
        {
            return DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private void SetMetricStatusAvailable(float value)
        {
            gLastValidMetricValue = value;
            txtMetricStatus.Text = "Live";
            txtMetricLastUpdate.Text = GetStatusTimestamp();
            txtMetricLastValid.Text = value.ToString("0.000", CultureInfo.InvariantCulture);
            icoMetricStatus.Kind = PackIconKind.CheckCircle;
            icoMetricStatus.Foreground = System.Windows.Media.Brushes.ForestGreen;
            brdMetricStatus.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 231, 246, 235));
            brdMetricStatus.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 113, 179, 124));
        }

        private void SetMetricStatusUnavailable(string reason)
        {
            txtMetricStatus.Text = reason;
            txtMetricLastUpdate.Text = GetStatusTimestamp();
            txtMetricLastValid.Text = gLastValidMetricValue.HasValue
                ? gLastValidMetricValue.Value.ToString("0.000", CultureInfo.InvariantCulture)
                : "n/a";
            icoMetricStatus.Kind = PackIconKind.AlertCircle;
            icoMetricStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 154, 103, 0));
            brdMetricStatus.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 253, 231, 194));
            brdMetricStatus.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 224, 122, 0));
        }

        private void ShowScalingValidationError(string message)
        {
            txtScalingValidationError.Text = message;
            brdScalingValidation.Visibility = Visibility.Visible;
        }

        private void HideScalingValidationError()
        {
            brdScalingValidation.Visibility = Visibility.Collapsed;
            txtScalingValidationError.Text = String.Empty;
        }

        private void SetMetricStatusNotConfigured()
        {
            txtMetricStatus.Text = "Not configured";
            txtMetricLastUpdate.Text = "-";
            txtMetricLastValid.Text = gLastValidMetricValue.HasValue
                ? gLastValidMetricValue.Value.ToString("0.000", CultureInfo.InvariantCulture)
                : "n/a";
            icoMetricStatus.Kind = PackIconKind.Information;
            icoMetricStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 96, 96, 96));
            brdMetricStatus.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 240, 240, 240));
            brdMetricStatus.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 207, 207, 207));
        }

        private void SetMetricStatusForCurrentSensor(LibreHardwareMonitor.Hardware.ISensor sensor)
        {
            float? value = sensor.Value;
            if (!value.HasValue || float.IsNaN(value.Value) || float.IsInfinity(value.Value))
            {
                SetMetricStatusUnavailable("No live reading");
                return;
            }

            SetMetricStatusAvailable(value.Value);
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

            tmpDial.SensorIdentifier = sensorIdentifier;
            tmpDial.Metric = sensorIdentifier;
            tmpDial.ScaleMin = sensorMin;
            tmpDial.ScaleMax = sensorMax;
            tmpDial.Thresholds = thresholds;

            if (sensorIdentifier != "")
            {
                VU1_Sensor? tmpSensor = SensorManager.FindSensorByIdentifier(sensorIdentifier);
                if(tmpSensor != null)
                {
                    tmpDial.Sensor = tmpSensor.Sensor;
                    tmpDial.SensorIdentifier = tmpSensor.Sensor.Identifier.ToString();
                    tmpDial.SensorName = tmpSensor.Sensor.Name.ToString();
                }
            }
            return tmpDial;
        }

        private void RefreshDialList()
        {
            Log.Information("Refreshing dial list");
            DialServer.RefreshDialList();
            List<DialInfo> restDials = DialServer.GetDialList();

            Log.Information("Updating local dials");
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

        private async void btnSaveDialSettings_click(object sender, RoutedEventArgs e)
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

            if (!DialComputationEngine.AreScalingBoundsValid(minValue, maxValue, out string scalingError))
            {
                ShowScalingValidationError(scalingError);
                return;
            }

            HideScalingValidationError();

            // Update currently selected sensor
            gCurrentlySelectedDial.FriendlyName = newName;
            gCurrentlySelectedDial.ScaleMin = minValue;
            gCurrentlySelectedDial.ScaleMax = maxValue;

            // API Call to update dial name
            if (await DialServer.UpdateDialNameAsync(gCurrentlySelectedDial.UID, newName).ConfigureAwait(true))
            {
                lbDials.Items.SortDescriptions.Clear();
                lbDials.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("FriendlyName", System.ComponentModel.ListSortDirection.Ascending));
                lbDials.Items.Refresh();
            }

            // Update config/UI only if:
            // - valid sensor is selected from the drop-down
            // - valud sensor is stored in config, then update only scaling values
            if (cbDialMetric.SelectedItem != null || gCurrentlySelectedDial.HasConfiguredSensorBinding)
            {
                // New sensor is selected
                if (cbDialMetric.SelectedItem != null)
                {
                    //gCurrentlySelectedDial.Sensor = computerSensors[cbDialMetric.SelectedIndex].Sensor;
                    VU1_Sensor? selectedSensor = cbDialMetric.SelectedItem as VU1_Sensor;
                    if (selectedSensor != null)
                    {
                        var sensorNode = selectedSensor.Sensor;
                        gCurrentlySelectedDial.Sensor = sensorNode;
                        gCurrentlySelectedDial.SensorIdentifier = sensorNode.Identifier.ToString();
                        gCurrentlySelectedDial.SensorName = sensorNode.Name.ToString();
                        gCurrentlySelectedDial.Metric = sensorNode.Identifier.ToString();
                        lblCurrentMetric.Content = String.Format("{0} - {1} - {2}", sensorNode.Name.ToString(), sensorNode.SensorType.ToString(), sensorNode.Identifier.ToString());
                    }
                }

                lblCurrentValue.Content = gCurrentlySelectedDial.Sensor?.Value?.ToString() ?? "0";
                lblScalingMin.Content = gCurrentlySelectedDial.ScaleMin.ToString();
                lblScalingMax.Content = gCurrentlySelectedDial.ScaleMax.ToString();

                // Update config file
                ConfigManager.UpdateDialConfig(gCurrentlySelectedDial, true);
                UpdateSelectedDialDisplay();
                lbDials.Items.Refresh();
            }
            
        }

        private void cbDialMetricCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string? selectedCategory = cbDialMetricCategory.SelectedItem?.ToString();
            if (String.IsNullOrWhiteSpace(selectedCategory))
            {
                return;
            }

            List<VU1_Sensor> filtered = new List<VU1_Sensor> { };

            foreach (var sens in computerSensors)
            {

                if (sens.Sensor.SensorType.ToString().Contains(selectedCategory))
                {
                    filtered.Add(sens);
                }
            }

            cbDialMetric.ItemsSource = filtered;
            cbDialMetric.Items.Refresh();
        }

        private async void btnChangeImage_click(object sender, RoutedEventArgs e)
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
                await DialServer.UpdateDialBackgroundImageAsync(gCurrentlySelectedDial.UID, res.FileName).ConfigureAwait(true);
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
            using RegistryKey? rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (rk == null)
            {
                return;
            }

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

            PauseDialUpdate();

            ConfigManager.SetServerHost(host);
            ConfigManager.SetServerPort(port);
            ConfigManager.RequestSaveConfigFileDebounced();

            string masterKey = ConfigManager.GetMasterKey();
            DialServer = new VU1_Server(host, port, masterKey);

            bool ok = DialServer.RefreshDialList();
            SetConnectionStatus(ok);

            if (ok)
            {
                RefreshDialList();
                ResumeDialUpdate();
                return;
            }

            Log.Warning("Reconnect failed for {Host}:{Port}; dial updates remain paused until next successful reconnect.", host, port);
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
            ConfigManager.RequestSaveConfigFileDebounced();
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
            var sensor = dial.Sensor;
            if (sensor != null)
            {
                // Initial dial value
                int dialValue = 0;
                int dialRed = 0;
                int dialGreen = 0;
                int dialBlue = 0;
                bool bBacklightUpdate = false;

                float fSensorValue = await Task.Run(() =>
                {
                    SensorReadingResult reading = MetricPollingService.ReadSensorValue(sensor);
                    if (reading.Status == SensorReadingStatus.Available && reading.Value.HasValue)
                    {
                        return reading.Value.Value;
                    }

                    return float.NaN;
                }).ConfigureAwait(false);

                if (float.IsNaN(fSensorValue) || float.IsInfinity(fSensorValue))
                {
                    string selectedSensorIdentifierUnavailable = gCurrentlySelectedDial.ConfiguredSensorIdentifier;
                    string dialSensorIdentifierUnavailable = sensor.Identifier.ToString();

                    if (selectedSensorIdentifierUnavailable == dialSensorIdentifierUnavailable)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            lblCurrentPercent.Content = "--";
                            lblCurrentValue.Content = "[n/a]";
                            SetMetricStatusUnavailable("No live reading");
                        });
                    }

                    LogThrottledWarning($"metric-unavailable-{dial.UID}",
                        "Skipping dial update due to unavailable metric reading. Dial: {DialUid}, Sensor: {SensorIdentifier}",
                        dial.UID,
                        dialSensorIdentifierUnavailable);
                    return;
                }

                dialValue = DialComputationEngine.ComputeDialValuePercent(dial.ScaleMin, dial.ScaleMax, fSensorValue);

                // Update backlight based on defined thresholds
                if (dial.Thresholds.Count > 0)
                {
                    ClassDialThreshold? match = DialComputationEngine.ResolveThresholdColor(dial.Thresholds, dialValue);

                    if (match != null)
                    {
                        dialRed = match.BacklightRed;
                        dialGreen = match.BacklightGreen;
                        dialBlue = match.BacklightBlue;
                        bBacklightUpdate = true;
                    }
                }


                string selectedSensorIdentifier = gCurrentlySelectedDial.ConfiguredSensorIdentifier;
                string dialSensorIdentifier = sensor.Identifier.ToString();

                Log.Verbose(String.Format("Dial:{0} set to {1}% [Sensor: {2}] - Raw value: {3}", dial.UID, dialValue, sensor.Identifier, fSensorValue));
                await DialServer.UpdateDialValueAsync(dial.UID, dialValue).ConfigureAwait(false);

                if (bBacklightUpdate)
                {
                    await DialServer.UpdateDialBacklightAsync(dial.UID, dialRed, dialGreen, dialBlue).ConfigureAwait(false);
                }

                if (selectedSensorIdentifier == dialSensorIdentifier)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        lblCurrentPercent.Content = String.Format("{0}%", dialValue);
                        lblCurrentValue.Content = String.Format("[{0:0.000}]", fSensorValue);
                        SetMetricStatusAvailable(fSensorValue);
                    });
                }
            }
        }

        private async Task ProcessDialUpdatesAsync()
        {
            List<ClassDialGUI> dialsToUpdate = gDials.Where(d => d.Sensor != null).ToList();
            foreach (ClassDialGUI dial in dialsToUpdate)
            {
                try
                {
                    await RefreshDialMetricAsync(dial).WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    LogThrottledWarning($"update-timeout-{dial.UID}", "Dial update timed out. Dial: {DialUid}", dial.UID);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Dial update failed. Dial: {DialUid}", dial.UID);
                }
            }
        }

        private void LogThrottledWarning(string key, string messageTemplate, params object[] propertyValues)
        {
            DateTime now = DateTime.UtcNow;
            lock (gWarningThrottleLock)
            {
                if (gWarningLastLoggedAtUtc.TryGetValue(key, out DateTime lastLoggedAt)
                    && now - lastLoggedAt < WarningThrottleInterval)
                {
                    return;
                }

                gWarningLastLoggedAtUtc[key] = now;
            }

            Log.Warning(messageTemplate, propertyValues);
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
            _ = Task.Run(async () =>
            {
                foreach (ClassDialGUI dial in gDials)
                {
                    await DialServer.UpdateDialValueAsync(dial.UID, 0).ConfigureAwait(false);
                    await DialServer.UpdateDialBacklightAsync(dial.UID, 0, 0, 0).ConfigureAwait(false);
                }
            });
        }

        public static bool RegistryValueExists(string hive_HKLM_or_HKCU, string registryRoot, string valueName)
        {
            RegistryKey? root;
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

            return root?.GetValue(valueName) != null;
        }


        private void TimerTick(object? sender, EventArgs e)
        {
            // Don't run if pause has been requested
            if (gDialUpdatePaused) return;

            if (!gDialUpdateOrchestrator.TryRun(ProcessDialUpdatesAsync))
            {
                Log.Verbose("Skipping tick because previous update is still running.");
            }
        }

        private static LogEventLevel ParseLogLevel(string levelText, LogEventLevel fallback)
        {
            return Enum.TryParse(levelText, true, out LogEventLevel parsedLevel) ? parsedLevel : fallback;
        }


        static void OnProcessExit(object? sender, EventArgs e)
        {
            Log.CloseAndFlush();
        }

    }
}
