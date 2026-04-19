using ClosedXML.Excel;
using VPMS.Models;

namespace VPMS.Services;

public class ExcelParserService
{
    // Expected DataLog column names (case-insensitive partial match)
    private static readonly Dictionary<string, string> DataLogColumnMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["timestamp"] = "Timestamp",
        ["datetime"] = "Timestamp",
        ["time"] = "Timestamp",
        ["vdc_bus"] = "VdcBus",
        ["vdc bus"] = "VdcBus",
        ["dc bus voltage"] = "VdcBus",
        ["vbatt"] = "VbattTotal",
        ["battery voltage"] = "VbattTotal",
        ["ibatt"] = "Ibatt",
        ["battery current"] = "Ibatt",
        ["soc"] = "StateOfCharge",
        ["state of charge"] = "StateOfCharge",
        ["batt temp"] = "BatteryTemp",
        ["battery temp"] = "BatteryTemp",
        ["vac in l1"] = "VacInputL1",
        ["vac input l1"] = "VacInputL1",
        ["vac in l2"] = "VacInputL2",
        ["vac input l2"] = "VacInputL2",
        ["vac in l3"] = "VacInputL3",
        ["vac input l3"] = "VacInputL3",
        ["freq in"] = "FrequencyInput",
        ["freq input"] = "FrequencyInput",
        ["frequency input"] = "FrequencyInput",
        ["vac out l1"] = "VacOutputL1",
        ["vac output l1"] = "VacOutputL1",
        ["vac out l2"] = "VacOutputL2",
        ["vac output l2"] = "VacOutputL2",
        ["vac out l3"] = "VacOutputL3",
        ["vac output l3"] = "VacOutputL3",
        ["freq out"] = "FrequencyOutput",
        ["freq output"] = "FrequencyOutput",
        ["frequency output"] = "FrequencyOutput",
        ["load %"] = "LoadPercent",
        ["load percent"] = "LoadPercent",
        ["output kw"] = "PowerOutputKw",
        ["power output"] = "PowerOutputKw",
        ["ambient temp"] = "AmbientTemp",
        ["ambient"] = "AmbientTemp",
        ["heatsink temp"] = "HeatsinkTemp",
        ["heatsink"] = "HeatsinkTemp",
        ["transformer temp"] = "TransformerTemp",
        ["fan1 rpm"] = "Fan1SpeedRpm",
        ["fan 1"] = "Fan1SpeedRpm",
        ["fan2 rpm"] = "Fan2SpeedRpm",
        ["fan 2"] = "Fan2SpeedRpm",
        ["mode"] = "OperatingMode",
        ["operating mode"] = "OperatingMode",
        ["idc"] = "IdcBus",
        ["dc current"] = "IdcBus"
    };

    public (List<TelemetryRow> rows, List<string> errors) ParseDataLog(string filePath)
    {
        var rows = new List<TelemetryRow>();
        var errors = new List<string>();

        try
        {
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();
            var headerRow = ws.Row(1);

            // Build column index map
            var colMap = new Dictionary<string, int>();
            foreach (var cell in headerRow.CellsUsed())
            {
                var header = cell.Value.ToString()?.Trim() ?? string.Empty;
                foreach (var (pattern, fieldName) in DataLogColumnMap)
                {
                    if (header.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                        header.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!colMap.ContainsKey(fieldName))
                            colMap[fieldName] = cell.Address.ColumnNumber;
                        break;
                    }
                }
            }

            if (!colMap.ContainsKey("Timestamp"))
            {
                errors.Add("DataLog is missing a recognizable Timestamp column.");
                return (rows, errors);
            }

            int rowNum = 2;
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                try
                {
                    var tr = new TelemetryRow
                    {
                        Timestamp = ParseDateTime(row, colMap, "Timestamp"),
                        VdcBus = ParseDouble(row, colMap, "VdcBus"),
                        IdcBus = ParseDouble(row, colMap, "IdcBus"),
                        VbattTotal = ParseDouble(row, colMap, "VbattTotal"),
                        Ibatt = ParseDouble(row, colMap, "Ibatt"),
                        BatteryTemp = ParseDouble(row, colMap, "BatteryTemp"),
                        StateOfCharge = ParseDouble(row, colMap, "StateOfCharge"),
                        VacInputL1 = ParseDouble(row, colMap, "VacInputL1"),
                        VacInputL2 = ParseDouble(row, colMap, "VacInputL2"),
                        VacInputL3 = ParseDouble(row, colMap, "VacInputL3"),
                        FrequencyInput = ParseDouble(row, colMap, "FrequencyInput"),
                        VacOutputL1 = ParseDouble(row, colMap, "VacOutputL1"),
                        VacOutputL2 = ParseDouble(row, colMap, "VacOutputL2"),
                        VacOutputL3 = ParseDouble(row, colMap, "VacOutputL3"),
                        FrequencyOutput = ParseDouble(row, colMap, "FrequencyOutput"),
                        LoadPercent = ParseDouble(row, colMap, "LoadPercent"),
                        PowerOutputKw = ParseDouble(row, colMap, "PowerOutputKw"),
                        AmbientTemp = ParseDouble(row, colMap, "AmbientTemp"),
                        HeatsinkTemp = ParseDouble(row, colMap, "HeatsinkTemp"),
                        TransformerTemp = ParseDouble(row, colMap, "TransformerTemp"),
                        Fan1SpeedRpm = ParseDouble(row, colMap, "Fan1SpeedRpm"),
                        Fan2SpeedRpm = ParseDouble(row, colMap, "Fan2SpeedRpm"),
                        OperatingMode = ParseString(row, colMap, "OperatingMode")
                    };

                    if (tr.Timestamp == DateTime.MinValue)
                    {
                        tr.IsValid = false;
                        tr.ValidationWarnings.Add($"Row {rowNum}: Invalid timestamp");
                    }

                    rows.Add(tr);
                }
                catch (Exception ex)
                {
                    errors.Add($"Row {rowNum}: {ex.Message}");
                }
                rowNum++;
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to open DataLog: {ex.Message}");
        }

        return (rows, errors);
    }

    public (List<AlarmEvent> alarms, List<string> errors) ParseAlarmLog(string filePath)
    {
        var alarms = new List<AlarmEvent>();
        var errors = new List<string>();

        try
        {
            using var wb = new XLWorkbook(filePath);
            var ws = wb.Worksheets.First();

            // Flexible header detection
            var headerRow = ws.Row(1);
            var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in headerRow.CellsUsed())
            {
                var h = cell.Value.ToString()?.Trim().ToLower() ?? string.Empty;
                if (h.Contains("time") || h.Contains("date")) colMap["Timestamp"] = cell.Address.ColumnNumber;
                else if (h.Contains("cleared") || h.Contains("end")) colMap["ClearedAt"] = cell.Address.ColumnNumber;
                else if (h.Contains("code") || h.Contains("id")) colMap["Code"] = cell.Address.ColumnNumber;
                else if (h.Contains("desc") || h.Contains("message") || h.Contains("alarm")) colMap["Description"] = cell.Address.ColumnNumber;
                else if (h.Contains("sever") || h.Contains("type") || h.Contains("level")) colMap["Severity"] = cell.Address.ColumnNumber;
                else if (h.Contains("subsystem") || h.Contains("system") || h.Contains("module")) colMap["Subsystem"] = cell.Address.ColumnNumber;
            }

            int id = 1;
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                try
                {
                    var alarm = new AlarmEvent
                    {
                        Id = id++,
                        Timestamp = ParseDateTime(row, colMap, "Timestamp"),
                        ClearedAt = colMap.ContainsKey("ClearedAt") ? ParseDateTimeNullable(row, colMap, "ClearedAt") : null,
                        Code = ParseString(row, colMap, "Code") ?? $"ALM{id:D4}",
                        Description = ParseString(row, colMap, "Description") ?? "Unknown alarm",
                        Severity = ParseAlarmSeverity(ParseString(row, colMap, "Severity")),
                        Subsystem = ParseString(row, colMap, "Subsystem") ?? "Unknown"
                    };
                    alarms.Add(alarm);
                }
                catch (Exception ex)
                {
                    errors.Add($"Alarm row {id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to open AlarmLog: {ex.Message}");
        }

        return (alarms, errors);
    }

    private static DateTime ParseDateTime(IXLRow row, Dictionary<string, int> map, string field)
    {
        if (!map.TryGetValue(field, out int col)) return DateTime.MinValue;
        var cell = row.Cell(col);
        if (cell.DataType == XLDataType.DateTime) return cell.GetDateTime();
        if (DateTime.TryParse(cell.Value.ToString(), out var dt)) return dt;
        return DateTime.MinValue;
    }

    private static DateTime? ParseDateTimeNullable(IXLRow row, Dictionary<string, int> map, string field)
    {
        var dt = ParseDateTime(row, map, field);
        return dt == DateTime.MinValue ? null : dt;
    }

    private static double ParseDouble(IXLRow row, Dictionary<string, int> map, string field)
    {
        if (!map.TryGetValue(field, out int col)) return 0;
        var cell = row.Cell(col);
        if (cell.DataType == XLDataType.Number) return cell.GetDouble();
        if (double.TryParse(cell.Value.ToString(), out var d)) return d;
        return 0;
    }

    private static string? ParseString(IXLRow row, Dictionary<string, int> map, string field)
    {
        if (!map.TryGetValue(field, out int col)) return null;
        return row.Cell(col).Value.ToString()?.Trim();
    }

    private static AlarmSeverity ParseAlarmSeverity(string? text) => text?.ToLower() switch
    {
        "critical" or "crit" => AlarmSeverity.Critical,
        "fault" or "error" or "err" => AlarmSeverity.Fault,
        "warning" or "warn" => AlarmSeverity.Warning,
        _ => AlarmSeverity.Info
    };
}
