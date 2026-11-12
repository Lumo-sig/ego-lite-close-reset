# ego-lite-close-reset

[中文](README.zh-CN.md)

Closes all tabs after you close ego lite.

ego lite keeps running in the background, and AI (ego-browser) tasks are not affected.

This is an unofficial tool.

## Features

- Closes every tab the user opened once the last window is closed.
- Minimizing a window, or closing one of several windows, does not close tabs.
- Keeps the task spaces the AI is using.
- Runs automatically after you log in.

## How it works

After the last window closes, the tool uses ego-browser to close all of the user's tabs and end their task spaces. The next time you open ego lite, there is only a new tab.

Only the user's own task spaces (`ownership = user`) are touched. Task spaces in use by the AI are left alone.

Reopening a window while tabs are being closed stops the remaining work. Tabs already closed are not restored.

Activity is logged to `guard.log` in the tool's directory.

## Requirements

- Windows x64
- [ego lite](https://github.com/citrolabs/ego-lite), which includes ego-browser
- [.NET 10 x64 runtime](https://dotnet.microsoft.com/download), installed in the default `%ProgramFiles%\dotnet` directory

Tested with ego lite 0.6.2.7.

## Install

1. Download the project and extract it to a permanent, writable directory.
2. Open PowerShell in that directory.
3. Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

The script compiles and starts the program, and registers it to run after login.

Keep the directory after installing. The startup entry refers to files in it. To move the directory, uninstall first, then install again from the new location.

## Uninstall

Open PowerShell in the project directory and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\uninstall.ps1
```

The script stops the program, removes the startup entry, and deletes the tool's files. Other files in the directory are left alone. The empty directory can be deleted by hand.

## Known limitations

- Relies on the ego-browser API and ego lite's window structure. Updates to ego lite may break it.

## License

[MIT](LICENSE)
