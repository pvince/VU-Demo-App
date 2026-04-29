using System;
using System.Collections.Generic;

namespace VU1WPF
{
    class ConfigThreshold
    {
        public int value { get; set; }
        public int red { get; set; }
        public int green { get; set; }
        public int blue { get; set; }
    }

    class ConfigContentsDial
    {
        public String dial_uid { get; set; } = String.Empty;
        public String sensor_identifier { get; set; } = String.Empty;
        public float scaling_min { get; set; }  // Value to be used as 0%
        public float scaling_max { get; set; } // Value to be used as 100%

        public List<ConfigThreshold> thresholds { get; set; } = new List<ConfigThreshold>();
    }

    class ConfigContentsRoot
    {
        public String masterKey { get; set; } = String.Empty;
        public float dialUpdatePeriod { get; set; }
        public String serverHost { get; set; } = String.Empty;
        public int serverPort { get; set; }

        public List<ConfigContentsDial> dial_metrics { get; set; } = new List<ConfigContentsDial>();
    }
}
