using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace KR_VU1_Sensors
{
    public class ClassVUSensors
    {
        public class VU1_Sensor
        {
            public ISensor Sensor { get; set; } // Actual Sensor node
            public String DisplayName { get; set; } = String.Empty; // "Pretty" name displayed in the GUI

            public VU1_Sensor(ISensor sens, String name = "")
            {
                Sensor = sens;

                DisplayName = String.IsNullOrWhiteSpace(name)
                    ? sens.Identifier.ToString()
                    : name;
                
            }
        }

        public class VU1_SensorManager
        {
            private List<VU1_Sensor> gSensorsAvailable = new List<VU1_Sensor>();
            private List<VU1_Sensor> gSensorsUsed = new List<VU1_Sensor>();

            private Computer computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsNetworkEnabled = true,
                IsStorageEnabled = true,
                IsPsuEnabled = true,
                IsBatteryEnabled = true
            };

            public String[] UsedSensors =
            {
                "Voltage",
                //"Current",
                "Power",
                "Clock",
                "Temperature",
                "Load",
                //"Frequency",
                //"Fan",
                //"Flow",
                //"Control",
                //"Level",
                //"Factor",
                //"Data",
                //"SmallData",
                //"Throughput",
                //"TimeSpan",
                //"Energy",
                //"Noise"
            };

            public VU1_SensorManager()
            {
                computer.Open();
                computer.Accept(new UpdateVisitor());
                reload_available_sensors();
            }

            public VU1_SensorManager(IEnumerable<VU1_Sensor> seededSensors, bool skipHardwareInitialization)
            {
                if (!skipHardwareInitialization)
                {
                    computer.Open();
                    computer.Accept(new UpdateVisitor());
                    reload_available_sensors();
                    return;
                }

                gSensorsAvailable = seededSensors.ToList();
                gSensorsUsed = seededSensors.ToList();
            }

            public List<VU1_Sensor> get_available_sensors()
            {
                return gSensorsAvailable;
            }

            public List<VU1_Sensor> get_used_sensors()
            {
                return gSensorsUsed;
            }


            public void reload_available_sensors()
            {
                gSensorsAvailable.Clear();
                gSensorsUsed.Clear();

                foreach (IHardware hardware in computer.Hardware)
                {
                    CollectSensorsFromHardware(hardware);
                }
            }

            private void CollectSensorsFromHardware(IHardware hardware)
            {
                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (UsedSensors.Contains(sensor.SensorType.ToString()))
                    {
                        VU1_Sensor tmp = new VU1_Sensor(sensor);
                        gSensorsAvailable.Add(tmp);
                        gSensorsUsed.Add(tmp);
                    }
                }

                foreach (IHardware subHardware in hardware.SubHardware)
                {
                    CollectSensorsFromHardware(subHardware);
                }
            }

            public VU1_Sensor? FindSensorByIdentifier(string identifier)
            {
                if (String.IsNullOrWhiteSpace(identifier))
                {
                    return null;
                }

                string? resolvedIdentifier = ResolveIdentifierCandidate(
                    gSensorsAvailable.Select(item => item.Sensor.Identifier.ToString()),
                    identifier);

                if (String.IsNullOrWhiteSpace(resolvedIdentifier))
                {
                    return null;
                }

                VU1_Sensor? resolvedSensor = gSensorsAvailable.Find(item => item.Sensor.Identifier.ToString() == resolvedIdentifier);
                if (resolvedSensor != null && !resolvedIdentifier.Equals(identifier, StringComparison.Ordinal))
                {
                    Log.Warning("Recovered sensor binding via fallback match. Requested identifier: {RequestedIdentifier}, resolved identifier: {ResolvedIdentifier}",
                        identifier,
                        resolvedIdentifier);
                }

                return resolvedSensor;
            }

            private static string[] GetIdentifierSegments(string identifier)
            {
                return identifier.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            private static int ParseIdentifierIndex(string[] segments)
            {
                if (segments.Length < 4)
                {
                    return int.MaxValue;
                }

                return int.TryParse(segments[3], out int idx) ? idx : int.MaxValue;
            }

            public static string? ResolveIdentifierCandidate(IEnumerable<string> availableIdentifiers, string identifier)
            {
                string[] requestedSegments = GetIdentifierSegments(identifier);
                if (requestedSegments.Length < 3)
                {
                    return null;
                }

                string requestedDomain = requestedSegments[0];
                string requestedBus = requestedSegments[1];
                string requestedType = requestedSegments[2];
                int requestedIndex = ParseIdentifierIndex(requestedSegments);

                List<string> normalizedIdentifiers = availableIdentifiers.Where(item => !String.IsNullOrWhiteSpace(item)).ToList();
                string? exact = normalizedIdentifiers.FirstOrDefault(item => item.Equals(identifier, StringComparison.OrdinalIgnoreCase));
                if (!String.IsNullOrWhiteSpace(exact))
                {
                    return exact;
                }

                List<string> strictCandidates = normalizedIdentifiers.Where(item =>
                {
                    string[] candidateSegments = GetIdentifierSegments(item);
                    return candidateSegments.Length >= 3
                        && candidateSegments[0].Equals(requestedDomain, StringComparison.OrdinalIgnoreCase)
                        && candidateSegments[1].Equals(requestedBus, StringComparison.OrdinalIgnoreCase)
                        && candidateSegments[2].Equals(requestedType, StringComparison.OrdinalIgnoreCase);
                }).ToList();

                if (strictCandidates.Count == 1)
                {
                    return strictCandidates[0];
                }

                if (strictCandidates.Count > 1)
                {
                    return strictCandidates
                        .OrderBy(item => Math.Abs(ParseIdentifierIndex(GetIdentifierSegments(item)) - requestedIndex))
                        .ThenBy(item => item)
                        .FirstOrDefault();
                }

                List<string> typeCandidates = normalizedIdentifiers.Where(item =>
                {
                    string[] candidateSegments = GetIdentifierSegments(item);
                    return candidateSegments.Length >= 3
                        && candidateSegments[2].Equals(requestedType, StringComparison.OrdinalIgnoreCase);
                }).ToList();

                if (typeCandidates.Count == 1)
                {
                    return typeCandidates[0];
                }

                return null;
            }


        }


        public class UpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer)
            {
                computer.Traverse(this);
            }
            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
                foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
            }
            public void VisitSensor(ISensor sensor) { }
            public void VisitParameter(IParameter parameter) { }
        }
    }
}
