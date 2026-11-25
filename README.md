# Auto Continue

A lightweight Windows tray utility that clicks a fixed screen position at a
configurable interval. It uses Windows `SendInput`; it does not inspect the
screen or detect popups.

## Use

- Set the interval in minutes (5 by default) and the Continue button's X/Y
  screen coordinates. Coordinates can be entered directly or captured with
  **Select Button Position**: move the pointer over the button and press
  **Ctrl+Alt+C**.
- Use **Test Click** to verify the position, then **Start**. The first
  automatic click occurs after the selected interval; subsequent clicks repeat
  at that interval.
- **Pause/Resume** preserves the remaining countdown. **Stop** ends automation.
- Closing or minimizing the window hides it in the system tray; the timer keeps
  running until stopped or the app is exited from the tray menu.
- **Start with Windows** launches this app at sign-in by adding or removing it
  from the current user's Windows startup registry key. Start automation from
  the app after sign-in.

Clicks move the physical mouse pointer to the configured screen position and
send a left click. Keep the target visible and unobstructed. The app does not
send clicks directly to a background window.

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
