# AI-Powered Focus Assistant

A Windows desktop application that tracks which applications you use, classifies that
activity as productive or distracting, and asks a reinforcement-learning agent when and
how to nudge you back on task. The agent learns from how you respond to each nudge.

## Architecture

Two processes talk over local HTTP:

```
┌──────────────────────────────┐        HTTP          ┌─────────────────────────────┐
│  WPF client (.NET 8)         │  127.0.0.1:5000      │  Flask backend (Python)     │
│                              │ ───────────────────► │                             │
│  • Win32 window/idle polling │                      │  • Q-learning agent         │
│  • SQLite session history    │ ◄─────────────────── │  • circadian + pattern      │
│  • intervention popups       │   interventions      │    subsystems               │
└──────────────────────────────┘                      └─────────────────────────────┘
```

The client launches and shuts down the backend itself, so you normally only run the WPF app.

## Repository layout

```
Python_Backend/
  app.py                     Flask API: /health /activity /suggestions
                             /analytics /insights /feedback /reset
  rl_agent.py                Adapter between the API and the agent
  enhanced_rl_agent.py       Q-learning agent + pattern/circadian/profile subsystems
  config.py                  Environment-driven settings
  models/                    Persisted agent state (JSON is tracked, .pkl is not)
  DEMO_Model_Creation/       Scripts to generate and replay a synthetic dataset
  requirements.txt

WPF_Frontend/FocusAssistant/
  App.xaml.cs                Composition root: DI, database creation, backend lifecycle
  MainWindow.xaml(.cs)       Shell and navigation
  Views/                     TrackingView, AnalyticsView, DashboardView,
                             RecommendationView, AiInterventionWindow
  ViewModels/                One view model per view
  Models/                    EF Core entities and API request/response DTOs
  Services/
    Application Monitoring/  Win32 foreground-window and idle polling
    Config/                  Backend URL, script path, polling intervals
    Data/                    EF Core DbContext
    Datafetch/               Generic EF Core repository per entity
    Flask/                   HTTP client, server lifecycle, endpoint services
    Session/                 Session lifecycle and app-usage accumulation
  SQL_analytics/             Daily statistics and CSV report generation
  Converters/                XAML value converters
```

## Requirements

- Windows 10 1809 or newer
- .NET 8 SDK
- Python 3.10 or newer

## Setup

```powershell
git clone https://github.com/sudeshkar/AI-Powered-Focus-Assistant.git
cd AI-Powered-Focus-Assistant
Copy-Item .env.example .env

# Backend. The virtual environment must live here — the WPF client looks for
# Python_Backend\.venv (or \venv) before falling back to system Python.
cd Python_Backend
python -m venv .venv
.venv\Scripts\python.exe -m pip install -r requirements.txt
cd ..

# Client
dotnet build WPF_Frontend\FocusAssistant\FocusAssistant.sln
```

## Running

```powershell
dotnet run --project WPF_Frontend\FocusAssistant\FocusAssistant.csproj
```

The client starts the Flask backend, waits for `/health`, then shuts it down on exit.
To run the backend on its own — useful when working on the agent:

```powershell
cd Python_Backend
.venv\Scripts\python.exe app.py
```

## Configuration

All settings come from `.env` at the repository root; see `.env.example` for the full
list. `.env` is gitignored.

| Variable | Default | Purpose |
| --- | --- | --- |
| `FLASK_HOST` / `FLASK_PORT` | `127.0.0.1` / `5000` | Where the backend listens |
| `FLASK_DEBUG` | `false` | Werkzeug debugger and reloader |
| `MODEL_DIR` | `models` | Where agent state is persisted |
| `MODEL_SAVE_INTERVAL` | `10` | Feedback events between saves |
| `MODEL_BACKUP_LIMIT` | `5` | Timestamped backups retained |

Leave `FLASK_DEBUG=false` for normal use. The reloader forks a second process that the
client cannot shut down cleanly, which leaves port 5000 held after exit.

## API

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/health` | Liveness check; the client polls this before use |
| `POST` | `/activity` | Report the active app; returns an intervention decision |
| `GET` | `/suggestions` | Behavioural patterns for the current context |
| `GET` | `/analytics` | Today's activity totals and top applications |
| `GET` | `/insights` | Per-action effectiveness and learning metrics |
| `POST` | `/feedback` | Report how the user responded; drives learning |
| `POST` | `/reset` | Clear agent state — requires `{"confirm": true}` |

`POST /activity` expects `app_name`, `window_title`, and `is_productive`, and returns
`distraction_risk`, `action_taken`, `intervention_message`, and `intervention_id`.
Feed that `intervention_id` back to `/feedback` so the reward is attributed correctly.

## Data and privacy

Everything stays on your machine. Nothing is sent anywhere except to the local
backend on `127.0.0.1`.

- Session history: `%LOCALAPPDATA%\FocusAssistant\focusassistant.db`
- Agent state: `Python_Backend/models/`

Window titles are sent to the local backend and stored in the local database, so the
database can contain document names, page titles, and similar. Treat it as personal data.

`.gitignore` excludes `.env`, databases, logs, and `*.pkl` model files. The JSON model
is tracked so the agent starts with useful priors instead of learning from nothing.

## Known limitations

- Windows only — activity tracking uses Win32 APIs directly.
- Backend state (`/analytics`, `/suggestions`) is in-memory and resets when the
  backend restarts. Durable history lives in the client's SQLite database.
- Productivity classification is a keyword ruleset, not a learned model.
- The Dashboard and Achievements views are placeholders; Tracking, Analytics, and
  Recommendations are the working views.
