# ego-lite-close-reset

[English](README.md)

关闭 ego lite 后，自动关闭所有标签页。

ego lite 仍在后台运行，AI（ego-browser）的任务不受影响。

本项目为非官方工具。

## 功能

- 关闭最后一个窗口后，关闭用户打开的所有标签页。
- 最小化窗口，或只关闭多个窗口中的一个，不会关闭标签页。
- 保留 AI 正在使用的任务空间。
- 开机登录后自动运行。

## 工作方式

最后一个窗口关闭后，通过 ego-browser 关闭用户的所有标签页，并结束对应的任务空间。下次打开 ego lite 时，只有一个新标签页。

只处理用户自己的任务空间（`ownership = user`），AI 使用中的任务空间不会动。

关闭过程中重新打开窗口，会停止后续关闭；已经关闭的标签页不会恢复。

运行记录写在目录下的 `guard.log`。

## 环境要求

- Windows x64
- [ego lite](https://github.com/citrolabs/ego-lite)，包含 ego-browser
- [.NET 10 x64 运行时](https://dotnet.microsoft.com/download)，安装在默认的 `%ProgramFiles%\dotnet` 目录

已测试 ego lite 0.6.2.7。

## 安装

1. 下载项目并解压到一个固定、可写入的目录。
2. 在该目录打开 PowerShell。
3. 执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

脚本会编译并启动程序，同时设为登录后自动运行。

安装后请保留该目录。自动启动会引用其中的文件；如需移动目录，请先卸载，再从新位置安装。

## 卸载

在项目目录打开 PowerShell，执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\uninstall.ps1
```

脚本会停止程序、取消登录后自动运行，并删除本工具的文件。目录里其他文件不会动，剩下的空目录可以手动删除。

## 已知限制

- 依赖 ego-browser 的接口和 ego lite 的窗口结构，ego lite 更新后可能失效。

## 许可证

[MIT](LICENSE)
