namespace VPMS.Models;

public class TelemetryRow
{
    public DateTime Timestamp { get; set; }

    // DC Bus
    public double VdcBus { get; set; }
    public double IdcBus { get; set; }

    // Battery
    public double VbattTotal { get; set; }
    public double Ibatt { get; set; }
    public double BatteryTemp { get; set; }
    public double StateOfCharge { get; set; }

    // AC Input
    public double VacInputL1 { get; set; }
    public double VacInputL2 { get; set; }
    public double VacInputL3 { get; set; }
    public double FrequencyInput { get; set; }

    // AC Output
    public double VacOutputL1 { get; set; }
    public double VacOutputL2 { get; set; }
    public double VacOutputL3 { get; set; }
    public double FrequencyOutput { get; set; }
    public double LoadPercent { get; set; }
    public double PowerOutputKw { get; set; }

    // Thermal
    public double AmbientTemp { get; set; }
    public double HeatsinkTemp { get; set; }
    public double TransformerTemp { get; set; }

    // Fans
    public double Fan1SpeedRpm { get; set; }
    public double Fan2SpeedRpm { get; set; }

    // Misc
    public string? OperatingMode { get; set; }
    public bool IsValid { get; set; } = true;
    public List<string> ValidationWarnings { get; set; } = [];
}
