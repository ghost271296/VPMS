namespace VPMS.Models;

public class FeatureSet
{
    // DC Bus features
    public double VdcMean { get; set; }
    public double VdcStd { get; set; }         // DC ripple proxy
    public double VdcMin { get; set; }
    public double VdcMax { get; set; }
    public double VdcRipplePeakToPeak { get; set; }

    // Battery features
    public double RbattProxy { get; set; }     // Internal resistance proxy
    public double VbattMean { get; set; }
    public double VbattSagDepth { get; set; }  // Max sag under load transient
    public double IbattMean { get; set; }
    public double SocMean { get; set; }
    public double SocMin { get; set; }
    public double BattTempMean { get; set; }
    public double BattTempMax { get; set; }

    // Frequency features
    public double FreqOutputMean { get; set; }
    public double FreqOutputStd { get; set; }   // Jitter
    public double FreqOutputMaxDeviation { get; set; }
    public double FreqInputMean { get; set; }
    public double FreqInputStd { get; set; }

    // Load profile
    public double LoadMean { get; set; }
    public double LoadMax { get; set; }
    public double LoadStd { get; set; }
    public Dictionary<string, double> LoadHistogram { get; set; } = [];  // bucket → % time
    public double LoadDutyCycleHigh { get; set; }   // % time above 80%

    // Thermal features
    public double AmbientTempMean { get; set; }
    public double AmbientTempMax { get; set; }
    public double HeatsinkTempMean { get; set; }
    public double HeatsinkTempMax { get; set; }
    public double ThermalHeadroom { get; set; }     // Max rated temp - actual max

    // Fan features
    public double Fan1SpeedMean { get; set; }
    public double Fan2SpeedMean { get; set; }
    public bool FanAnomalyDetected { get; set; }

    // Alarm features
    public int TotalAlarmCount { get; set; }
    public int FaultCount { get; set; }
    public int CriticalAlarmCount { get; set; }
    public double AlarmStormIndex { get; set; }     // Alarms per hour in worst 5-min window
    public Dictionary<string, int> AlarmsBySubsystem { get; set; } = [];
    public Dictionary<string, int> AlarmsByCode { get; set; } = [];

    // Log meta
    public TimeSpan LogDuration { get; set; }
    public int TotalRows { get; set; }
    public DateTime LogStart { get; set; }
    public DateTime LogEnd { get; set; }
    public double SamplingRateHz { get; set; }
}
