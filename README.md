# VPMS — Vertiv Predictive Maintenance System

**Version 3.0** | AI-Assisted UPS Diagnostics, Repair & Reporting

## Overview

VPMS is a Windows desktop application (WPF, .NET 9) that transforms UPS telemetry logs into guided, evidence-backed diagnostic reports. It combines deterministic feature engineering with a multi-agent AI pipeline and two human validation gates.

## Quick Start

### Requirements
- Windows 10/11, .NET 9 SDK
- OpenAI API key (for AI pipeline; falls back to rule-based detection if absent)

### Configuration

Set your OpenAI API key in one of two ways:

**Environment variable (recommended):**
```
set OPENAI_API_KEY=sk-...
```

**`appsettings.json`:**
```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-4o"
  }
}
```

### Build & Run
```
dotnet build VPMS/VPMS.csproj
dotnet run --project VPMS/VPMS.csproj
```

---

## Pipeline

```
Upload → Parse → Feature Engine → Issue Agent → [Gate 1] →
RCA Agent → Query Agent → Field Input → Fishbone Agent →
[Gate 2] → Action Agent → Predictive Engine → Report
```

### Steps

| Step | Description | Human Gate |
|------|-------------|------------|
| **Upload** | Drag-drop DataLog.xlsx + AlarmLog.xlsx; validates quality | — |
| **Issues** | AI-detected issues with severity & confidence | ✅ Gate 1 |
| **RCA** | Ranked root causes with probability bars | — |
| **Field Questions** | 3–5 targeted AI-generated questions | — |
| **Fishbone** | Editable Ishikawa cause diagram | ✅ Gate 2 |
| **Actions** | Immediate / Scheduled / Monitor action plan | — |
| **Prediction** | 30/60/90-day subsystem failure risk | — |
| **Report** | PDF, DOCX, CSV, JSON export | — |

---

## Features

- **Data Quality Score (0–100)** — automatic, penalises downstream confidence
- **Feature Engine** — 30+ deterministic, auditable engineering features
  - VdcStd (DC ripple), RbattProxy (battery internal resistance), FreqOutputStd, AlarmStormIndex, LoadHistogram, ThermalHeadroom, FanAnomalyDetected
- **Health Index** — Battery (35%), DC Link (25%), Power Stage (25%), Thermal (15%)
- **Multi-agent AI** — GPT-4o for issue detection, RCA, adaptive query, fishbone, actions, predictive risk
- **Offline fallback** — Rule-based detection runs if AI is unavailable
- **Local-first** — no data leaves the machine unless exported
- **Report formats** — PDF (QuestPDF), DOCX (OpenXML), CSV (CsvHelper), JSON (Newtonsoft)

---

## Tech Stack

| Component | Technology |
|-----------|-----------|
| UI | WPF (.NET 9) |
| Language | C# 12 |
| MVVM | CommunityToolkit.Mvvm 8.3 |
| Excel parsing | ClosedXML |
| AI | OpenAI SDK (GPT-4o) |
| PDF | QuestPDF |
| DOCX | DocumentFormat.OpenXml |
| CSV | CsvHelper |
| Charts | LiveChartsCore (WPF) |
| DI | Microsoft.Extensions.DependencyInjection |

---

## Architecture

```
VPMS/
├── Models/          Domain models (TelemetryRow, FeatureSet, DiagnosticSession…)
├── Services/        Deterministic services (parse, quality, features, health, report)
│   └── AI/          Six AI agents + OpenAI client
├── ViewModels/      CommunityToolkit.Mvvm view models for each pipeline stage
├── Views/           XAML UserControls (one per stage) + MainWindow
├── Converters/      WPF value converters
└── Resources/       Styles.xaml (colour palette, component styles)
```

---

## Roadmap

- **V1.1** — DOCX export polish, comparative multi-log analysis, contextual help overlays
- **V1.2** — Persistent local case history, search across past diagnoses
- **V2.0** — Cloud sync, fleet dashboard, role-based access
- **V3.0** — Digital twin integration, cross-site predictive analytics

---

*VPMS — From hours of log-hunting to minutes of guided insight.*
