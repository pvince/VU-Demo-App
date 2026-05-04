using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using VU1WPF;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Serilog;
using Serilog.Events;
using static KR_VU1_Sensors.ClassVUSensors;
using System.Threading.Tasks;


namespace KR_VU1_ConfigurationManager
{
    public class ClassConfigurationManager
    {
        private const string DefaultConfigFileName = "vu1demo_config.yaml";
        private readonly float default_update_period = 0.5f;
        private readonly string default_master_key = "cTpAWYuRpA2zx75Yh961Cg";
        private readonly string default_server_host = "localhost";
        private readonly int default_server_port = 5340;
        private readonly string default_log_level = LogEventLevel.Information.ToString();
        private readonly bool default_diagnostics_mode = false;
        private readonly string pathConfigFile;
        private readonly string pathFileName;
        private readonly bool showLoadFailureDialog;
        private ConfigContentsRoot localConfig;
        private readonly DebouncedConfigSaver debouncedConfigSaver;
        private readonly object saveFileLock = new object();

        public ClassConfigurationManager()
            : this(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "KaranovicResearch", "VU1-DemoApp"),
                DefaultConfigFileName,
                true)
        {
        }

        public ClassConfigurationManager(string configDirectoryPath, bool showLoadFailureDialog = true)
            : this(configDirectoryPath, DefaultConfigFileName, showLoadFailureDialog)
        {
        }

        public ClassConfigurationManager(string configDirectoryPath, string configFileName, bool showLoadFailureDialog = true)
        {
            if (string.IsNullOrWhiteSpace(configDirectoryPath))
            {
                throw new ArgumentException("Config directory path is required.", nameof(configDirectoryPath));
            }

            if (string.IsNullOrWhiteSpace(configFileName))
            {
                throw new ArgumentException("Config file name is required.", nameof(configFileName));
            }

            this.showLoadFailureDialog = showLoadFailureDialog;
            pathConfigFile = configDirectoryPath;
            pathFileName = configFileName;

            Log.Information("The global logger has been configured");

            localConfig = new ConfigContentsRoot();
            debouncedConfigSaver = new DebouncedConfigSaver(
                () =>
                {
                    SaveConfigFile();
                    return Task.CompletedTask;
                },
                TimeSpan.FromMilliseconds(120),
                ex => Log.Error(ex, "Debounced config save failed"));

            LoadConfigFile();

            Log.Information(String.Format("Dial update period: {0} seconds", localConfig.dialUpdatePeriod));

            // Check update period
            if (localConfig.dialUpdatePeriod == 0 || localConfig.dialUpdatePeriod < 0.2F)
            {
                Log.Error("Config reqests invalid dial update period of `{0}`. Limiting to 0.2 seconds.", localConfig.dialUpdatePeriod);
                localConfig.dialUpdatePeriod = 0.2F;
            }

            if (localConfig.masterKey == null || localConfig.masterKey == String.Empty) 
            {
                Log.Error("Invalid master key `{0}` found in config. Resetting to default key.", localConfig.masterKey);
                localConfig.masterKey = default_master_key;
            }

            if (!Enum.TryParse<LogEventLevel>(localConfig.logLevel, true, out _))
            {
                Log.Warning("Invalid log level `{LogLevel}` found in config. Resetting to {DefaultLogLevel}.", localConfig.logLevel, default_log_level);
                localConfig.logLevel = default_log_level;
            }

            SaveConfigFile();   // Save default if no config file exists
        }

        private string GetConfigPath()
        {
            return Path.Combine(pathConfigFile, pathFileName);
        }

        private void Initialize_EmptyConfigFile()
        {
            string path = GetConfigPath();
            Log.Information("Initializing empty config file at: {0}", path);

            // Set default values
            localConfig.dialUpdatePeriod = default_update_period;
            localConfig.masterKey = default_master_key;
            localConfig.serverHost = default_server_host;
            localConfig.serverPort = default_server_port;
            localConfig.logLevel = default_log_level;
            localConfig.diagnosticsMode = default_diagnostics_mode;

            Log.Information("Set update period to {0}.", localConfig.dialUpdatePeriod);
            Log.Information("Set Master Key to {0}.", localConfig.masterKey);

            // Check if file exists
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(pathConfigFile);
                using (File.Create(path))
                {
                }
            }

            // Save new config file
            SaveConfigFile();
        }

        public float GetDialUpdatePeriod()
        {
            return localConfig.dialUpdatePeriod;
        }

        public String GetMasterKey()
        {
            return localConfig.masterKey;
        }

        public String GetServerHost()
        {
            return string.IsNullOrWhiteSpace(localConfig.serverHost) ? default_server_host : localConfig.serverHost;
        }

        public int GetServerPort()
        {
            return localConfig.serverPort > 0 ? localConfig.serverPort : default_server_port;
        }

        public string GetLogLevel()
        {
            if (Enum.TryParse<LogEventLevel>(localConfig.logLevel, true, out LogEventLevel parsedLevel))
            {
                return parsedLevel.ToString();
            }

            return default_log_level;
        }

        public bool IsDiagnosticsModeEnabled()
        {
            return localConfig.diagnosticsMode;
        }

        public void SetServerHost(string host)
        {
            localConfig.serverHost = host;
        }

        public void SetServerPort(int port)
        {
            localConfig.serverPort = port;
        }

        public bool UpdateDialConfig(ClassDialGUI sensor, bool saveAfter)
        {
            var dial = localConfig.dial_metrics.Find(item => item.dial_uid == sensor.UID);
            string sensorIdentifier = sensor.ConfiguredSensorIdentifier;

            if (dial != null)
            {
                dial.dial_uid = sensor.UID;
                dial.scaling_min = sensor.ScaleMin;
                dial.scaling_max = sensor.ScaleMax;
                dial.thresholds = DialGUI_to_ConfigThresholds(sensor.Thresholds);
                dial.sensor_identifier = String.IsNullOrWhiteSpace(sensorIdentifier)
                    ? dial.sensor_identifier
                    : sensorIdentifier;
                 
            }
            else
            {
                ConfigContentsDial tmp = new ConfigContentsDial
                {
                    dial_uid = sensor.UID,
                    sensor_identifier = sensorIdentifier,
                    scaling_min = sensor.ScaleMin,
                    scaling_max = sensor.ScaleMax,
                    thresholds = DialGUI_to_ConfigThresholds(sensor.Thresholds)
                };
                localConfig.dial_metrics.Add(tmp);
            }

            if (saveAfter)
            {
                RequestSaveConfigFileDebounced();
            }

            return true;
        }


        private List<ConfigThreshold> DialGUI_to_ConfigThresholds(List<ClassDialThreshold>? dialThresholds)
        {
            List<ConfigThreshold> ret = new();
        
            if (dialThresholds == null)
            {
                return ret;
            }

            foreach (ClassDialThreshold item in dialThresholds)
            {
                ConfigThreshold tmp = new ConfigThreshold
                {
                    value = item.Threshold,
                    red = item.BacklightRed,
                    green = item.BacklightGreen,
                    blue = item.BacklightBlue
                };
                ret.Add(tmp);
            }

            return ret;
        }

        public List<ClassDialThreshold> GetDialThresholds(String uid)
        {
            List<ClassDialThreshold> ret = new();

            ConfigContentsDial? sid = localConfig.dial_metrics.Find(item => item.dial_uid == uid);

            if (sid == null)
            {
                return ret;
            }
            else if (sid.thresholds == null)
            {
                return ret;
            }
            else if (sid.thresholds.Count <= 0)
            {
                return ret;
            }

            foreach (ConfigThreshold item in sid.thresholds)
            {

                ClassDialThreshold tmp = new ClassDialThreshold
                {
                    Threshold = item.value,
                    BacklightRed = item.red,
                    BacklightGreen = item.green,
                    BacklightBlue = item.blue
                };
                ret.Add(tmp);
            }

            return ret;
        }

        public string GetDialMetric(String uid)
        {
            var sid = localConfig.dial_metrics.Find(item => item.dial_uid == uid);

            if (sid != null)
            {
                return sid.sensor_identifier.ToString();
            }
            else
            {
                return "";
            }
        }

        public float GetDialMin(String uid)
        {
            var sid = localConfig.dial_metrics.Find(item => item.dial_uid == uid);

            if (sid != null)
            {
                float fValue = 100;
                float.TryParse(sid.scaling_min.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out fValue);
                return fValue;
            }

            return 100;
        }


        public float GetDialMax(String uid)
        {
            var sid = localConfig.dial_metrics.Find(item => item.dial_uid == uid);

            if (sid != null)
            {
                float fValue = 100;
                float.TryParse(sid.scaling_max.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out fValue);
                return fValue;
            }
            
            return 100;
        }


        private void LoadConfigFile()
        {
            try
            {
                string path = GetConfigPath();
                string fileContents;

                if (!File.Exists(path))
                {
                    Log.Debug("App config does not existing. Initalizing default one.");
                    Initialize_EmptyConfigFile();
                }
                else
                {
                    Log.Debug("Config exists. Reusing.");
                }

                using (StreamReader streamReader = new StreamReader(path, Encoding.UTF8))
                {
                    fileContents = streamReader.ReadToEnd();
                }

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                // TODO: Demote this to Verbose after verifying config loading works properly
                Log.Debug("---- Config contents: ---");
                Log.Debug(fileContents.ToString());
                Log.Debug("---- END debug info ---");

                ConfigContentsRoot? p = deserializer.Deserialize<ConfigContentsRoot>(fileContents);

                if (p == null)
                {
                    Log.Error("Config content is null. Aborting load.");
                    Log.Error("---- Additional debug info ---");
                    Log.Error("Config contents:");
                    Log.Error(fileContents.ToString());
                    Log.Error("---- END debug info ---");

                    localConfig = new ConfigContentsRoot
                    {
                        dialUpdatePeriod = default_update_period,
                        masterKey = default_master_key,
                        serverHost = default_server_host,
                        serverPort = default_server_port,
                        logLevel = default_log_level,
                        diagnosticsMode = default_diagnostics_mode
                    };
                    return;
                }

                localConfig = new ConfigContentsRoot
                {
                    dialUpdatePeriod = p.dialUpdatePeriod,
                    masterKey = string.IsNullOrWhiteSpace(p.masterKey) ? default_master_key : p.masterKey,
                    serverHost = string.IsNullOrWhiteSpace(p.serverHost) ? default_server_host : p.serverHost,
                    serverPort = p.serverPort > 0 ? p.serverPort : default_server_port,
                    logLevel = string.IsNullOrWhiteSpace(p.logLevel) ? default_log_level : p.logLevel,
                    diagnosticsMode = p.diagnosticsMode
                };

                foreach (ConfigContentsDial dial in p.dial_metrics)
                {
                    Log.Debug($"{dial.dial_uid} uses {dial.sensor_identifier}.");
                    localConfig.dial_metrics.Add(dial);
                }

            }
            catch (Exception e)
            {
                Log.Error("Encountered exception while loading config.");
                Log.Error(e.ToString());

                if (showLoadFailureDialog)
                {
                    MessageBox.Show(e.ToString(), "YAML read process failed.", MessageBoxButton.OK, MessageBoxImage.Warning);

                    // Let's close the app now
                    System.Windows.Forms.Application.Exit();
                    return;
                }

                throw;
            }

        }


        public void SaveConfigFile()
        {
            SaveConfigFileInternal();
        }

        public void RequestSaveConfigFileDebounced()
        {
            debouncedConfigSaver.RequestSave();
        }

        public Task FlushPendingConfigSaveAsync()
        {
            return debouncedConfigSaver.FlushAsync();
        }

        private void SaveConfigFileInternal()
        {
            string path = GetConfigPath();
            Log.Debug("Saving config file.");

            try
            {
                lock (saveFileLock)
                {
                    Directory.CreateDirectory(pathConfigFile);
                    using (StreamWriter streamWriter = new StreamWriter(path))
                    {
                        Serializer serializer = (Serializer)new SerializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
                        serializer.Serialize(streamWriter, localConfig);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save config file");
                throw;
            }


        }
    }
}
