# Focus Assistant

A Windows desktop app that watches which application you're using, classifies each
stretch of activity as productive or distracting entirely on-device, and nudges you
back on task when a distraction has genuinely run long enough to be worth
interrupting. Everything — tracking, classification, and the optional written
summaries — runs locally. Nothing leaves the machine.

## Architecture

Five layered .NET 10 projects, one process, no network calls:

```
src/
  FocusAssistant.Core/          Domain model and every abstraction. No WPF, no ONNX,
                                 no EF Core — this project can be unit tested without
                                 a desktop.
  FocusAssistant.Data/          EF Core + SQLite: repositories, migrations, the
                                 user-override store.
  FocusAssistant.Platform/      Win32 interop: foreground-window/idle polling,
                                 single-instance guard, autostart, window activation.
  FocusAssistant.Intelligence/  The on-device models: a MiniLM embedding classifier
                                 (committed, ~23MB) and an optional Phi-3.5-mini
                                 language model (downloaded on first use, ~2.6GB).
  FocusAssistant.App/           WPF shell, dependency injection, the tray icon, the
                                 intervention pipeline, and the views.
```

`FocusAssistant.Core` defines every interface the other layers implement —
`IActivityClassifier`, `ILocalLanguageModel`, `IInterventionPolicy`, and so on — so
the domain logic never depends on ONNX Runtime, EF Core, or WPF being present. Each
of those can be swapped or tested independently.

## How classification works

Activity is classified in layers, most authoritative first:

1. **A correction you made** ("This is work") — always wins.
2. **An explicit keyword rule** (`devenv.exe` is Development) — fast, high precision.
3. **The on-device embedding model** — for anything the rules don't recognise, and
   for ambiguous cases like browsers, where the process name alone says nothing.
4. **Similarity to your stated session goal**, when one is set.
5. **A flagged guess** — and the app knows it's guessing, so it won't nudge you
   over one.

Every verdict carries *why* it was made, shown live on the Focus screen — so a
misclassification is a click away from being fixed, not a black box.

## The nudge

The intervention pipeline is deliberately hard to trigger: not on a guess, not in
the first 90 seconds of a distraction, not more than a handful of times an hour or a
day, and never while you're plausibly on a call. When it does show up, it's a small
card in the corner of the screen that **never steals keyboard focus** — you can keep
typing right through it — with three options: jump back to what you were doing,
snooze five minutes, or tell it "this is work," which corrects the classifier
immediately and permanently for that app.

## Setup

**Requirements:** Windows 10/11, [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/sudeshkar/AI-Powered-Focus-Assistant.git
cd AI-Powered-Focus-Assistant
dotnet build FocusAssistant.slnx
dotnet run --project src/FocusAssistant.App/FocusAssistant.App.csproj
```

That's it — no Python, no separate backend process, no API keys. The embedding
classifier is committed to the repository and works immediately. The optional
language model (for written daily summaries and more naturally worded nudges) can
be downloaded from the Settings screen inside the app; everything else works
without it.

## Running in the background

Closing the window doesn't stop tracking — it hides to a tray icon, since a focus
tool that only works while you're staring at it isn't much of one. The tray menu
has **Open** and **Quit**; Quit is the only way to actually exit. A second launch
while it's already running just brings the existing window forward.

## Data and privacy

Everything stays on this machine:

| What | Where |
|---|---|
| Session history, activity log, nudge outcomes | `%LocalAppData%\FocusAssistant\focusassistant.db` |
| Application logs (app names only — never window titles) | `%LocalAppData%\FocusAssistant\Logs\` |
| The optional language model | `%LocalAppData%\FocusAssistant\Models\` |

Window titles can contain document names, email subjects, and page titles — treat
the database as personal data. There is currently no in-app retention limit,
pause-tracking control, or per-app exclusion list; those are the next things being
built (see Known limitations).

## Configuration

`src/FocusAssistant.App/appsettings.json` controls polling intervals, the
classifier's confidence thresholds, and the language model's idle-unload timeout.
Changing it doesn't require a rebuild — it's read at startup from the output
directory.

## Known limitations

- Windows only — tracking uses Win32 APIs directly.
- No retention limit, pause-tracking, or per-app exclusion list yet.
- No automated test suite yet, despite `FocusAssistant.Core` being built specifically
  to allow one.
- The tray icon uses a generic system icon rather than a custom one.
- Escalation beyond a toast-style nudge (a full-screen overlay) is wired up but has
  no way to be turned on yet — by design, nothing currently escalates that far.
