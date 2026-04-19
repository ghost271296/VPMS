using System.IO;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Newtonsoft.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VPMS.Models;

namespace VPMS.Services;

public enum ReportSection
{
    ExecutiveSummary,
    HealthDashboard,
    DetectedIssues,
    RootCauseAnalysis,
    FishboneDiagram,
    CorrectiveActions,
    PredictiveRisks,
    EngineeringAppendix
}

public class ReportOptions
{
    public HashSet<ReportSection> IncludedSections { get; set; } = [.. Enum.GetValues<ReportSection>()];
    public bool IncludeFishboneImage { get; set; } = true;
}

public class ReportService
{
    public ReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<string> ExportPdfAsync(DiagnosticSession session, string outputPath, ReportOptions? options = null)
    {
        options ??= new ReportOptions();
        await Task.Run(() => BuildPdf(session, outputPath, options));
        return outputPath;
    }

    public async Task<string> ExportDocxAsync(DiagnosticSession session, string outputPath)
    {
        await Task.Run(() => BuildDocx(session, outputPath));
        return outputPath;
    }

    public async Task<string> ExportCsvAsync(DiagnosticSession session, string outputPath)
    {
        await Task.Run(() => BuildCsv(session, outputPath));
        return outputPath;
    }

    public async Task<string> ExportJsonAsync(DiagnosticSession session, string outputPath)
    {
        var artifact = new
        {
            session.Id,
            session.CreatedAt,
            session.EngineerName,
            session.SiteId,
            session.UpsModel,
            session.UpsSerial,
            session.DataQuality,
            session.Features,
            Health = session.Health,
            Issues = session.Issues,
            RootCauses = session.RootCauses,
            Queries = session.Queries,
            Actions = session.Actions,
            PredictiveRisk = session.PredictiveRisk
        };
        var json = JsonConvert.SerializeObject(artifact, Formatting.Indented);
        await File.WriteAllTextAsync(outputPath, json);
        return outputPath;
    }

    private static void BuildPdf(DiagnosticSession s, string path, ReportOptions opts)
    {
        var hi = s.Health;
        var dq = s.DataQuality;

        Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("VPMS — Vertiv Predictive Maintenance System")
                            .Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                        row.ConstantItem(120).AlignRight().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                    col.Item().LineHorizontal(1).LineColor(Colors.Blue.Darken3);
                    col.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text($"Site: {s.SiteId}  |  UPS: {s.UpsModel} ({s.UpsSerial})  |  Engineer: {s.EngineerName}").FontSize(9);
                        if (hi != null)
                            row.ConstantItem(120).AlignRight()
                                .Text($"Health Index: {hi.OverallScore:F0}/100 — {hi.OverallStatus}").FontSize(9).Bold();
                    });
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    // Executive Summary
                    if (opts.IncludedSections.Contains(ReportSection.ExecutiveSummary))
                    {
                        col.Item().Section("executive-summary").Text("Executive Summary").Bold().FontSize(13);
                        col.Item().PaddingBottom(6).Text(BuildExecutiveSummary(s)).FontSize(10);
                    }

                    // Health Dashboard
                    if (opts.IncludedSections.Contains(ReportSection.HealthDashboard) && hi != null)
                    {
                        col.Item().Text("System Health Dashboard").Bold().FontSize(13);
                        col.Item().PaddingBottom(6).Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); });
                            t.Header(h =>
                            {
                                h.Cell().Text("Subsystem").Bold();
                                h.Cell().Text("Score").Bold();
                                h.Cell().Text("Status").Bold();
                            });
                            AddHealthRow(t, hi.Battery);
                            AddHealthRow(t, hi.DcLinkCapacitor);
                            AddHealthRow(t, hi.PowerStage);
                            AddHealthRow(t, hi.Thermal);
                            t.Cell().Text("OVERALL").Bold();
                            t.Cell().Text($"{hi.OverallScore:F0}/100").Bold();
                            t.Cell().Text(hi.OverallStatus).Bold();
                        });
                    }

                    // Detected Issues
                    if (opts.IncludedSections.Contains(ReportSection.DetectedIssues) && s.Issues.Count > 0)
                    {
                        col.Item().Text("Detected Issues").Bold().FontSize(13);
                        foreach (var issue in s.Issues.Where(i => i.IsApproved).OrderByDescending(i => i.Severity))
                        {
                            col.Item().PaddingBottom(4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(ic =>
                            {
                                ic.Item().Row(r =>
                                {
                                    r.RelativeItem().Text($"[{issue.Severity}] {issue.Title}").Bold();
                                    r.ConstantItem(80).AlignRight().Text($"{issue.Confidence:P0} confidence").FontSize(8);
                                });
                                ic.Item().Text(issue.Evidence).FontSize(9).FontColor(Colors.Grey.Darken2);
                            });
                        }
                    }

                    // Root Cause Analysis
                    if (opts.IncludedSections.Contains(ReportSection.RootCauseAnalysis) && s.RootCauses.Count > 0)
                    {
                        col.Item().PaddingTop(6).Text("Root Cause Analysis").Bold().FontSize(13);
                        foreach (var rc in s.RootCauses.OrderBy(r => r.Rank))
                        {
                            col.Item().PaddingBottom(4).Column(rc2 =>
                            {
                                rc2.Item().Text($"#{rc.Rank} {rc.Title} — {rc.Probability:P0} probability").Bold().FontSize(10);
                                rc2.Item().Text(rc.Explanation).FontSize(9).FontColor(Colors.Grey.Darken2);
                            });
                        }
                    }

                    // Corrective Actions
                    if (opts.IncludedSections.Contains(ReportSection.CorrectiveActions) && s.Actions.Count > 0)
                    {
                        col.Item().PaddingTop(6).Text("Corrective Actions").Bold().FontSize(13);
                        foreach (var grp in s.Actions.GroupBy(a => a.Priority).OrderBy(g => g.Key))
                        {
                            col.Item().Text(grp.Key.ToString().ToUpper()).Bold().FontSize(10).FontColor(Colors.Orange.Darken2);
                            foreach (var action in grp)
                            {
                                col.Item().PaddingBottom(3).PaddingLeft(10).Column(ac =>
                                {
                                    ac.Item().Text($"• {action.Title} — {action.EstimatedTime}").FontSize(10);
                                    ac.Item().Text(action.Description).FontSize(9).FontColor(Colors.Grey.Darken1);
                                });
                            }
                        }
                    }

                    // Predictive Risks
                    if (opts.IncludedSections.Contains(ReportSection.PredictiveRisks) && s.PredictiveRisk != null)
                    {
                        col.Item().PaddingTop(6).Text("Predictive Risk Assessment").Bold().FontSize(13);
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(2); });
                            t.Header(h =>
                            {
                                h.Cell().Text("Subsystem").Bold();
                                h.Cell().Text("30-day").Bold();
                                h.Cell().Text("60-day").Bold();
                                h.Cell().Text("90-day").Bold();
                                h.Cell().Text("Remaining Life").Bold();
                            });
                            foreach (var sr in s.PredictiveRisk.Subsystems)
                            {
                                t.Cell().Text(sr.Subsystem);
                                t.Cell().Text($"{sr.Risk30Day:P0}");
                                t.Cell().Text($"{sr.Risk60Day:P0}");
                                t.Cell().Text($"{sr.Risk90Day:P0}");
                                t.Cell().Text(sr.RemainingLifeEstimate);
                            }
                        });
                    }

                    // Engineering Appendix
                    if (opts.IncludedSections.Contains(ReportSection.EngineeringAppendix) && s.Features != null)
                    {
                        col.Item().PaddingTop(6).Text("Engineering Appendix — Feature Set").Bold().FontSize(13);
                        col.Item().Text(BuildFeatureSummary(s.Features)).FontSize(8).FontFamily("Courier New");
                    }

                    // Signature block
                    col.Item().PaddingTop(10).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                    col.Item().PaddingTop(4).Text($"Diagnosed by: {s.EngineerName}  |  Session ID: {s.Id}  |  VPMS v3.0").FontSize(8).FontColor(Colors.Grey.Darken1);
                    if (s.ReportNotes is { Length: > 0 })
                        col.Item().Text($"Notes: {s.ReportNotes}").FontSize(8).Italic();
                });
            });
        }).GeneratePdf(path);
    }

    private static void AddHealthRow(TableDescriptor t, SubsystemHealth h)
    {
        t.Cell().Text(h.Name);
        t.Cell().Text($"{h.Score:F0}/100");
        t.Cell().Text(h.Status);
    }

    private static string BuildExecutiveSummary(DiagnosticSession s)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"This report summarises the diagnostic analysis performed on UPS unit {s.UpsModel} (S/N: {s.UpsSerial}) at site {s.SiteId} on {s.CreatedAt:yyyy-MM-dd}.");
        if (s.Health != null)
            sb.AppendLine($"The overall health index is {s.Health.OverallScore:F0}/100 ({s.Health.OverallStatus}). Battery: {s.Health.Battery.Score:F0}/100, DC Link: {s.Health.DcLinkCapacitor.Score:F0}/100, Power Stage: {s.Health.PowerStage.Score:F0}/100, Thermal: {s.Health.Thermal.Score:F0}/100.");
        int critical = s.Issues.Count(i => i.IsApproved && i.Severity == IssueSeverity.Critical);
        int high = s.Issues.Count(i => i.IsApproved && i.Severity == IssueSeverity.High);
        sb.AppendLine($"{s.Issues.Count(i => i.IsApproved)} issues detected ({critical} critical, {high} high). {s.Actions.Count(a => a.Priority == ActionPriority.Immediate)} immediate actions required.");
        if (s.PredictiveRisk != null)
            sb.AppendLine($"Predictive outlook: {s.PredictiveRisk.OverallTrend}. {s.PredictiveRisk.Recommendation}");
        return sb.ToString();
    }

    private static string BuildFeatureSummary(FeatureSet fs)
    {
        return $"""
            Log: {fs.LogStart:u} → {fs.LogEnd:u} ({fs.LogDuration.TotalMinutes:F1} min), {fs.TotalRows} rows, {fs.SamplingRateHz:F2} Hz
            VdcMean={fs.VdcMean:F2}V  VdcStd={fs.VdcStd:F4}V  VdcP-P={fs.VdcRipplePeakToPeak:F2}V
            VbattMean={fs.VbattMean:F2}V  RbattProxy={fs.RbattProxy * 1000:F1}mΩ  VbattSag={fs.VbattSagDepth:F2}V
            SoCMean={fs.SocMean:F1}%  SoCMin={fs.SocMin:F1}%  BattTempMax={fs.BattTempMax:F1}°C
            FreqOutMean={fs.FreqOutputMean:F4}Hz  FreqOutStd={fs.FreqOutputStd:F6}Hz
            LoadMean={fs.LoadMean:F1}%  LoadMax={fs.LoadMax:F1}%  HighLoad%={fs.LoadDutyCycleHigh:F1}%
            HeatsinkMax={fs.HeatsinkTempMax:F1}°C  AmbientMax={fs.AmbientTempMax:F1}°C  Headroom={fs.ThermalHeadroom:F1}°C
            FanAnomaly={fs.FanAnomalyDetected}  AlarmStorm={fs.AlarmStormIndex:F1}/hr  TotalAlarms={fs.TotalAlarmCount}
            """;
    }

    private static void BuildDocx(DiagnosticSession s, string path)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        AddDocxParagraph(body, $"VPMS Diagnostic Report", bold: true, fontSize: 28);
        AddDocxParagraph(body, $"Site: {s.SiteId} | UPS: {s.UpsModel} ({s.UpsSerial})", fontSize: 20);
        AddDocxParagraph(body, $"Engineer: {s.EngineerName} | Date: {s.CreatedAt:yyyy-MM-dd}", fontSize: 18);
        AddDocxParagraph(body, "");

        if (s.Health != null)
        {
            AddDocxParagraph(body, "System Health", bold: true, fontSize: 24);
            AddDocxParagraph(body, $"Overall: {s.Health.OverallScore:F0}/100 — {s.Health.OverallStatus}", fontSize: 20);
            AddDocxParagraph(body, $"Battery: {s.Health.Battery.Score:F0}/100  DC Link: {s.Health.DcLinkCapacitor.Score:F0}/100  Power Stage: {s.Health.PowerStage.Score:F0}/100  Thermal: {s.Health.Thermal.Score:F0}/100", fontSize: 18);
        }

        AddDocxParagraph(body, "");
        AddDocxParagraph(body, "Detected Issues", bold: true, fontSize: 24);
        foreach (var issue in s.Issues.Where(i => i.IsApproved).OrderByDescending(i => i.Severity))
            AddDocxParagraph(body, $"[{issue.Severity}] {issue.Title}: {issue.Evidence}", fontSize: 18);

        AddDocxParagraph(body, "");
        AddDocxParagraph(body, "Corrective Actions", bold: true, fontSize: 24);
        foreach (var action in s.Actions.OrderBy(a => a.Priority))
            AddDocxParagraph(body, $"[{action.Priority}] {action.Title} ({action.EstimatedTime}): {action.Description}", fontSize: 18);

        if (s.ReportNotes is { Length: > 0 })
        {
            AddDocxParagraph(body, "");
            AddDocxParagraph(body, "Engineer Notes", bold: true, fontSize: 24);
            AddDocxParagraph(body, s.ReportNotes, fontSize: 18);
        }

        mainPart.Document.Save();
    }

    private static void AddDocxParagraph(Body body, string text, bool bold = false, int fontSize = 20)
    {
        var para = body.AppendChild(new Paragraph());
        var run = para.AppendChild(new Run());
        if (bold || fontSize > 20)
        {
            run.RunProperties = new RunProperties();
            if (bold) run.RunProperties.AppendChild(new Bold());
            run.RunProperties.AppendChild(new FontSize { Val = fontSize.ToString() });
        }
        run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static void BuildCsv(DiagnosticSession s, string path)
    {
        using var writer = new StreamWriter(path, false, Encoding.UTF8);
        using var csv = new CsvWriter(writer, new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture));

        // Features
        writer.WriteLine("# FEATURE SET");
        if (s.Features != null)
        {
            csv.WriteField("Feature"); csv.WriteField("Value"); csv.NextRecord();
            csv.WriteField("VdcMean"); csv.WriteField(s.Features.VdcMean); csv.NextRecord();
            csv.WriteField("VdcStd"); csv.WriteField(s.Features.VdcStd); csv.NextRecord();
            csv.WriteField("RbattProxy_mOhm"); csv.WriteField(s.Features.RbattProxy * 1000); csv.NextRecord();
            csv.WriteField("FreqOutputStd_Hz"); csv.WriteField(s.Features.FreqOutputStd); csv.NextRecord();
            csv.WriteField("HeatsinkTempMax_C"); csv.WriteField(s.Features.HeatsinkTempMax); csv.NextRecord();
            csv.WriteField("LoadMax_pct"); csv.WriteField(s.Features.LoadMax); csv.NextRecord();
            csv.WriteField("AlarmStormIndex"); csv.WriteField(s.Features.AlarmStormIndex); csv.NextRecord();
        }

        writer.WriteLine();
        writer.WriteLine("# ISSUES");
        csv.WriteField("ID"); csv.WriteField("Title"); csv.WriteField("Severity"); csv.WriteField("Confidence"); csv.WriteField("Subsystem"); csv.NextRecord();
        foreach (var i in s.Issues.Where(x => x.IsApproved))
        {
            csv.WriteField(i.Id); csv.WriteField(i.Title); csv.WriteField(i.Severity); csv.WriteField(i.Confidence); csv.WriteField(i.Subsystem); csv.NextRecord();
        }

        writer.WriteLine();
        writer.WriteLine("# ACTIONS");
        csv.WriteField("Priority"); csv.WriteField("Title"); csv.WriteField("Estimated Time"); csv.WriteField("Skill Level"); csv.NextRecord();
        foreach (var a in s.Actions.OrderBy(x => x.Priority))
        {
            csv.WriteField(a.Priority); csv.WriteField(a.Title); csv.WriteField(a.EstimatedTime); csv.WriteField(a.SkillLevel); csv.NextRecord();
        }
    }
}
