# Auto Continue

A lightweight Windows tray utility that clicks a fixed screen position at a
configurable interval. It uses Windows `SendInput`; it does not inspect the
screen or detect popups.

## Use

- Choose single clicks, rapid double clicks, or two clicks 1.5 seconds apart.
  Set an interval in seconds, minutes, or hours (5 minutes by default), and
  enter the Continue button's X/Y screen coordinates. Coordinates can be entered directly or captured with
  **Select Button Position**: move the pointer over the button and press
  **Ctrl+Alt+C**.
- Choose **Repeat indefinitely** or clear it and enter a run count. A run
  performs the selected click action once. With the delayed option, the
  second click happens 1.5 seconds after the first.
- Use **Test Click** to verify the position, then **Start**. The first
  automatic run occurs after the selected interval; subsequent runs repeat at
  that interval until the count is reached or automation is stopped.
- **Pause/Resume** preserves the remaining countdown. **Stop** ends automation.
- Minimizing the window hides it in the system tray and automation continues.
  Closing the window or choosing **Exit** from the tray menu stops automation
  and exits the app.
- **Start with Windows** launches this app at sign-in by adding or removing it
  from the current user's Windows startup registry key. Start automation from
  the app after sign-in.

Clicks move the physical mouse pointer to the configured screen position and
send left-click input. Keep the target visible and unobstructed. The app does
not send clicks directly to a background window.

## Build

Build and run on Windows with the .NET 8 SDK:

```powershell
dotnet build
dotnet run
```

To publish a Windows executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```
