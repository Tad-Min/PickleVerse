#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using PickleVerse.Replays;

namespace PickleVerse.FrontEnd
{
    // Per-user/per-backend broker. An URI launch forwards to the first running game;
    // normal manual launches remain available for multiplayer testing.
    public sealed class WindowsReplayBridge : IDisposable
    {
        readonly Mutex marker;
        readonly string pipeName;
        readonly string userSid;
        readonly Action<string> receive;
        readonly ManualResetEventSlim initialized = new ManualResetEventSlim();
        readonly Thread broker;
        volatile bool stopped;
        volatile bool primary;
        NamedPipeServerStream listening;
        public bool IsPrimary => primary;
        public string Failure { get; private set; }
        public WindowsReplayBridge(string backend, Action<string> receive)
        {
            this.receive = receive;
            userSid = WindowsReplayPipe.CurrentUserSid();
            string origin = new Uri(backend).GetLeftPart(UriPartial.Authority).ToLowerInvariant();
            using (var process = Process.GetCurrentProcess())
            using (var sha = SHA256.Create())
                pipeName = "PickleVerse.Replay." + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(
                    userSid + "|" + process.SessionId + "|" + origin))).Replace("-", "").Substring(0, 24);
            marker = new Mutex(false, "Local\\" + pipeName);
            broker = new Thread(Listen) { IsBackground = true, Name = "PickleVerse replay links" };
            broker.Start();
            if (!initialized.Wait(2000)) throw new IOException("Replay link channel did not start.");
        }
        static byte[] Read(Stream stream, int length)
        {
            var bytes = new byte[length];
            for (int offset = 0; offset < length;)
            {
                var task = stream.ReadAsync(bytes, offset, length - offset);
                if (!task.Wait(2000) || task.Result == 0) throw new IOException("Replay link channel timed out.");
                offset += task.Result;
            }
            return bytes;
        }
        void Listen()
        {
            bool owned = false;
            try
            {
                try { owned = marker.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                primary = owned;
                initialized.Set();
                // A manually opened second game can take over the broker after
                // the first game exits. Mutex ownership stays on this thread.
                while (!owned && !stopped)
                    try { owned = marker.WaitOne(100); } catch (AbandonedMutexException) { owned = true; }
                primary = owned;
                while (owned && !stopped)
                {
                    try
                    {
                        using (var pipe = WindowsReplayPipe.CreateServer(pipeName, userSid))
                        {
                            listening = pipe;
                            if (stopped) break;
                            pipe.WaitForConnection();
                            int length = BitConverter.ToInt32(Read(pipe, 4), 0);
                            if (length < 1 || length > 512) continue;
                            string uri = Encoding.UTF8.GetString(Read(pipe, length));
                            if (!ReplayWebLink.TryParse(uri, out _)) continue;
                            if (stopped) break;
                            receive(uri);
                            pipe.WriteByte(1);
                        }
                    }
                    catch (Exception error) when (error is IOException || error is ObjectDisposedException || error is AggregateException || error is UnauthorizedAccessException)
                    { if (!stopped) Thread.Sleep(100); }
                    finally { listening = null; }
                }
            }
            catch (Exception error)
            {
                Failure = error is Win32Exception native ? $"Win32 {native.NativeErrorCode}" : error.GetType().Name;
            }
            finally
            {
                primary = false; initialized.Set();
                if (owned) marker.ReleaseMutex();
                marker.Dispose();
            }
        }
        public bool Forward(string uri)
        {
            if (IsPrimary || !ReplayWebLink.TryParse(uri, out _)) return false;
            try
            {
                using (var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
                    PipeOptions.Asynchronous, TokenImpersonationLevel.Anonymous))
                {
                    pipe.Connect(2000);
                    if (!WindowsReplayPipe.HasOwner(pipe.SafePipeHandle, userSid) ||
                        !GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint pid) || IsPrimary) return false;
                    AllowSetForegroundWindow(pid);
                    byte[] payload = Encoding.UTF8.GetBytes(uri), length = BitConverter.GetBytes(payload.Length);
                    pipe.Write(length, 0, length.Length); pipe.Write(payload, 0, payload.Length); pipe.Flush();
                    return Read(pipe, 1)[0] == 1;
                }
            }
            catch (Exception error) when (error is IOException || error is TimeoutException || error is UnauthorizedAccessException || error is AggregateException)
            { return false; }
        }
        public static string Command(string executable)
        {
            if (string.IsNullOrEmpty(executable) || executable.IndexOf('"') >= 0 || !Path.IsPathRooted(executable) ||
                !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Invalid game executable.");
            return "\"" + executable + "\" \"%1\"";
        }
        public static void Register(string executable)
        {
            string command = Command(executable);
            const string root = "Software\\Classes\\pickleverse";
            SetRegistry(root, "", "URL:PickleVerse Replay"); SetRegistry(root, "URL Protocol", "");
            SetRegistry(root + "\\DefaultIcon", "", "\"" + executable + "\",0");
            SetRegistry(root + "\\shell\\open\\command", "", command);
        }
        // Win32 keeps this compatible with Unity's .NET Standard profile; no
        // global API compatibility change or administrator privileges required.
        static void SetRegistry(string path, string name, string value)
        {
            int error = RegCreateKeyEx(new IntPtr(unchecked((int)0x80000001)), path, 0, null, 0, 2, IntPtr.Zero, out var key, out _);
            if (error != 0) throw new Win32Exception(error);
            try
            {
                byte[] bytes = Encoding.Unicode.GetBytes(value + "\0");
                error = RegSetValueEx(key, name, 0, 1, bytes, bytes.Length);
                if (error != 0) throw new Win32Exception(error);
            }
            finally { RegCloseKey(key); }
        }
        public static void Foreground()
        {
            var window = Process.GetCurrentProcess().MainWindowHandle;
            if (window == IntPtr.Zero) window = GetActiveWindow();
            if (window != IntPtr.Zero) { ShowWindowAsync(window, 9); SetForegroundWindow(window); }
        }
        public void Dispose()
        {
            stopped = true;
            listening?.Dispose();
            broker.Join(250);
        }
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegCreateKeyExW")] static extern int RegCreateKeyEx(IntPtr root, string path, int reserved, string type, int options, int access, IntPtr security, out IntPtr key, out int disposition);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegSetValueExW")] static extern int RegSetValueEx(IntPtr key, string name, int reserved, int type, byte[] data, int length);
        [DllImport("advapi32.dll")] static extern int RegCloseKey(IntPtr key);
        [DllImport("kernel32.dll")] static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint processId);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window, int command);
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
    }

    // Unity Mono does not implement the WindowsIdentity properties used by
    // PipeOptions.CurrentUserOnly. Apply the same account restriction in Win32.
    static class WindowsReplayPipe
    {
        const uint TokenQuery = 0x0008;
        const int TokenUser = 1;
        const uint SddlRevision = 1;
        const uint DuplexOverlapped = 0x40000003;
        const uint RejectRemoteClients = 0x00000008;
        const int KernelObject = 6;
        const uint OwnerSecurityInformation = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        struct SecurityAttributes
        {
            public int Length;
            public IntPtr Descriptor;
            public int InheritHandle;
        }

        public static string CurrentUserSid()
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out var token)) throw LastError();
            using (token)
            {
                GetTokenInformation(token, TokenUser, IntPtr.Zero, 0, out int size);
                if (size <= 0) throw LastError();
                IntPtr info = Marshal.AllocHGlobal(size);
                try
                {
                    if (!GetTokenInformation(token, TokenUser, info, size, out _)) throw LastError();
                    return SidString(Marshal.ReadIntPtr(info));
                }
                finally { Marshal.FreeHGlobal(info); }
            }
        }

        public static NamedPipeServerStream CreateServer(string name, string userSid)
        {
            string permissions = $"O:{userSid}D:P(A;;GA;;;{userSid})";
            if (!ConvertStringSecurityDescriptor(permissions, SddlRevision, out IntPtr descriptor, IntPtr.Zero))
                throw LastError();
            try
            {
                var attributes = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = descriptor };
                var handle = CreateNamedPipe(@"\\.\pipe\" + name, DuplexOverlapped, RejectRemoteClients, 1, 1024, 1024, 0, ref attributes);
                if (handle.IsInvalid)
                {
                    var error = LastError();
                    handle.Dispose();
                    throw error;
                }
                try { return new NamedPipeServerStream(PipeDirection.InOut, true, false, handle); }
                catch { handle.Dispose(); throw; }
            }
            finally { LocalFree(descriptor); }
        }

        public static bool HasOwner(SafePipeHandle pipe, string userSid)
        {
            int result = GetSecurityInfo(pipe, KernelObject, OwnerSecurityInformation, out IntPtr owner,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out IntPtr descriptor);
            try { return result == 0 && owner != IntPtr.Zero && SidString(owner) == userSid; }
            finally { if (descriptor != IntPtr.Zero) LocalFree(descriptor); }
        }

        static string SidString(IntPtr sid)
        {
            if (!ConvertSidToStringSid(sid, out IntPtr text)) throw LastError();
            try { return Marshal.PtrToStringUni(text); }
            finally { LocalFree(text); }
        }

        static Win32Exception LastError() => new Win32Exception(Marshal.GetLastWin32Error());

        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr process, uint access, out SafeFileHandle token);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool GetTokenInformation(SafeFileHandle token, int kind, IntPtr info, int size, out int required);
        [DllImport("advapi32.dll", EntryPoint = "ConvertSidToStringSidW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern bool ConvertSidToStringSid(IntPtr sid, out IntPtr text);
        [DllImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern bool ConvertStringSecurityDescriptor(string text, uint revision, out IntPtr descriptor, IntPtr size);
        [DllImport("advapi32.dll")]
        static extern int GetSecurityInfo(SafePipeHandle handle, int type, uint information, out IntPtr owner, IntPtr group,
            IntPtr dacl, IntPtr sacl, out IntPtr descriptor);
        [DllImport("kernel32.dll", EntryPoint = "CreateNamedPipeW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        static extern SafePipeHandle CreateNamedPipe(string name, uint openMode, uint pipeMode, uint instances,
            uint outputSize, uint inputSize, uint timeout, ref SecurityAttributes attributes);
    }
}
#endif
