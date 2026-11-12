Option Explicit
Dim shell, files, assembly, dotnet
Set shell = CreateObject("WScript.Shell")
Set files = CreateObject("Scripting.FileSystemObject")
assembly = files.BuildPath(files.GetParentFolderName(WScript.ScriptFullName), "EgoGuard.dll")
dotnet = shell.ExpandEnvironmentStrings("%ProgramFiles%\dotnet\dotnet.exe")
shell.Run Chr(34) & dotnet & Chr(34) & " " & Chr(34) & assembly & Chr(34), 0, False
