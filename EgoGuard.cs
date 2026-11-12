using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class EgoGuard
{
    const string MutexName = "Local\\EgoGuard.SingleInstance";
    const string StopName = "Local\\EgoGuard.Stop";
    const uint WorkerDone = 0x8001;
    const uint Quit = 0x0012;
    const uint Hide = 0x8003;
    const uint Destroy = 0x8001;
    delegate void EventCallback(IntPtr hook, uint ev, IntPtr window, int obj, int child, uint thread, uint time);
    delegate bool EnumCallback(IntPtr window, IntPtr data);
    [StructLayout(LayoutKind.Sequential)]
    struct Message { public IntPtr window; public uint message; public UIntPtr wParam; public IntPtr lParam; public uint time; public int x, y; public uint reserved; }

    [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, EventCallback callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumCallback callback, IntPtr data);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window, uint command);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll", SetLastError = true)] static extern int GetMessage(out Message message, IntPtr window, uint min, uint max);
    [DllImport("user32.dll")] static extern bool PeekMessage(out Message message, IntPtr window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref Message message);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread, uint message, UIntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")] static extern uint GetTickCount();
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder text, ref int size);

    static readonly string Dir = AppDomain.CurrentDomain.BaseDirectory;
    static readonly string RequestPath = Path.Combine(Dir, "guard-request.json");
    static readonly string LogPath = Path.Combine(Dir, "guard.log");
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
    static readonly HashSet<IntPtr> KnownWindows = new HashSet<IntPtr>();
    static readonly EventCallback Callback = OnEvent;
    static bool Armed;
    static uint ThreadId;
    static int LastCount = -1;
    static Process Worker;
    static Task<string> WorkerResult;

    static void Log(string text)
    {
        if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 262144)
            File.WriteAllLines(LogPath, File.ReadAllLines(LogPath, Utf8).Reverse().Take(150).Reverse(), Utf8);
        File.AppendAllText(LogPath, DateTimeOffset.Now.ToString("o") + " " + text + Environment.NewLine, Utf8);
    }

    static bool IsEgo(IntPtr window)
    {
        if (KnownWindows.Contains(window)) return true;
        if (GetAncestor(window, 2) != window) return false;
        var text = new StringBuilder(1024);
        GetClassName(window, text, text.Capacity);
        if (text.ToString() != "Chrome_WidgetWin_1") return false;
        uint pid;
        GetWindowThreadProcessId(window, out pid);
        IntPtr process = OpenProcess(0x1000, false, pid);
        if (process == IntPtr.Zero) return false;
        try
        {
            int size = text.Capacity;
            if (!QueryFullProcessImageName(process, 0, text, ref size) ||
                !Path.GetFileName(text.ToString()).Equals("ego.exe", StringComparison.OrdinalIgnoreCase)) return false;
        }
        finally { CloseHandle(process); }
        KnownWindows.Add(window);
        return true;
    }

    static int CountWindows()
    {
        int count = 0;
        EnumWindows(delegate(IntPtr window, IntPtr data)
        {
            // Minimized windows remain visible according to IsWindowVisible.
            if (!IsEgo(window) || !IsWindowVisible(window) || GetWindow(window, 4) != IntPtr.Zero) return true;
            var title = new StringBuilder(1024);
            GetWindowText(window, title, title.Capacity);
            // Ego can retain a task name as a browser window title after that task ends.
            if (title.Length > 0) count++;
            return true;
        }, IntPtr.Zero);
        return count;
    }

    static void CancelRequest() { if (File.Exists(RequestPath)) File.Delete(RequestPath); }

    static void StartCleanup()
    {
        Armed = false;
        try
        {
            File.WriteAllText(RequestPath, "{\"id\":\"" + Guid.NewGuid().ToString("N") + "\",\"expires\":" +
                (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 60000) + "}", Utf8);
            string cli = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), @".local\share\ego\active_version_dir\ego-browser.exe");
            string url = new Uri(Path.Combine(Dir, "close-user-tabs.mjs")).AbsoluteUri;
            var start = new ProcessStartInfo(cli, "nodejs -e \"await import('" + url + "')\"");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Utf8;
            start.StandardErrorEncoding = Utf8;
            Log("Cleanup started.");
            var process = Process.Start(start);
            Worker = process;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            WorkerResult = Task.Run(delegate
            {
                process.WaitForExit();
                string output = (stdout.Result + stderr.Result).Trim();
                // Ego can report a stopped task with exit code 0; require our result too.
                string status = process.ExitCode == 0 && output.Contains("EGO_RESET_RESULT ") ? "Cleanup" : "Cleanup incomplete";
                return status + " exit=" + process.ExitCode + " " + output;
            });
            WorkerResult.ContinueWith(delegate { PostThreadMessage(ThreadId, WorkerDone, UIntPtr.Zero, IntPtr.Zero); });
        }
        catch (Exception error)
        {
            CancelRequest();
            if (Worker != null) Worker.Dispose();
            Worker = null;
            WorkerResult = null;
            Log("Cleanup launch failed: " + error.Message);
        }
    }

    static void CompleteCleanup()
    {
        try { Log(WorkerResult.Result); }
        catch (Exception error) { Log("Cleanup failed: " + error.GetBaseException().Message); }
        finally
        {
            CancelRequest();
            Worker.Dispose();
            Worker = null;
            WorkerResult = null;
        }
    }

    static void OnEvent(IntPtr hook, uint ev, IntPtr window, int obj, int child, uint thread, uint time)
    {
        if (obj != 0 || child != 0 || !IsEgo(window)) return;
        try
        {
            int count = CountWindows();
            if (count != LastCount) { Log("Visible windows: " + LastCount + " -> " + count); LastCount = count; }
            if (count > 0) { CancelRequest(); Armed = true; }
            else if ((ev == Hide || ev == Destroy) && Armed && Worker == null)
            {
                Log("Last window closed. Event delivery ms=" + unchecked(GetTickCount() - time));
                StartCleanup();
            }
        }
        catch (Exception error) { Log("Window event failed: " + error.Message); }
        finally { if (ev == Destroy) KnownWindows.Remove(window); }
    }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--probe") { Console.WriteLine("Visible windows: " + CountWindows()); return 0; }
        if (args.Length > 0 && args[0] == "--stop")
        {
            try { using (var stop = EventWaitHandle.OpenExisting(StopName)) stop.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
            return 0;
        }
        bool created;
        using (var mutex = new Mutex(true, MutexName, out created))
        {
            if (!created) return 0;
            try
            {
                using (var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopName))
                {
                    stop.Reset();
                    ThreadId = GetCurrentThreadId();
                    Message message;
                    PeekMessage(out message, IntPtr.Zero, 0, 0, 0);
                    var stopWait = ThreadPool.RegisterWaitForSingleObject(stop, delegate { PostThreadMessage(ThreadId, Quit, UIntPtr.Zero, IntPtr.Zero); }, null, Timeout.Infinite, true);
                    IntPtr objects = IntPtr.Zero, minimize = IntPtr.Zero, names = IntPtr.Zero;
                    try
                    {
                        CancelRequest();
                        LastCount = CountWindows();
                        Armed = LastCount > 0;
                        objects = SetWinEventHook(Destroy, Hide, IntPtr.Zero, Callback, 0, 0, 2);
                        minimize = SetWinEventHook(0x16, 0x17, IntPtr.Zero, Callback, 0, 0, 2);
                        // A newly created window may receive its title after its SHOW event.
                        names = SetWinEventHook(0x800C, 0x800C, IntPtr.Zero, Callback, 0, 0, 2);
                        if (objects == IntPtr.Zero || minimize == IntPtr.Zero || names == IntPtr.Zero) throw new Exception("SetWinEventHook failed");
                        Log("Started event watcher. PID=" + Process.GetCurrentProcess().Id + " visible=" + LastCount);
                        int result;
                        while ((result = GetMessage(out message, IntPtr.Zero, 0, 0)) > 0)
                        {
                            if (message.message == WorkerDone)
                            {
                                CompleteCleanup();
                                if (Armed && CountWindows() == 0) StartCleanup();
                            }
                            else { TranslateMessage(ref message); DispatchMessage(ref message); }
                        }
                        if (result < 0) throw new Exception("GetMessage failed: " + Marshal.GetLastWin32Error());
                    }
                    finally
                    {
                        CancelRequest();
                        if (objects != IntPtr.Zero) UnhookWinEvent(objects);
                        if (minimize != IntPtr.Zero) UnhookWinEvent(minimize);
                        if (names != IntPtr.Zero) UnhookWinEvent(names);
                        stopWait.Unregister(null);
                        if (WorkerResult != null && Task.WaitAny(new Task[] { WorkerResult }, 5000) == 0) CompleteCleanup();
                        Log("Stopped event watcher.");
                    }
                }
                return 0;
            }
            catch (Exception error) { Log("Watcher failed: " + error.Message); return 1; }
            finally { mutex.ReleaseMutex(); }
        }
    }
}
