#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PickleVerse.FrontEnd
{
    /// <summary>Native Windows open-file dialog for player builds, where EditorUtility does not exist.</summary>
    static class WindowsFilePicker
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OpenFileName
        {
            public int structSize;
            public IntPtr owner, instance, filter, customFilter;
            public int maxCustomFilter, filterIndex;
            public IntPtr file;
            public int maxFile;
            public IntPtr fileTitle;
            public int maxFileTitle;
            public IntPtr initialDirectory, title;
            public int flags;
            public short fileOffset, fileExtension;
            public IntPtr defaultExtension, customData, hook, templateName, reserved;
            public int reservedValue, flagsEx;
        }
        [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode)]
        static extern bool GetOpenFileName(ref OpenFileName dialog);
        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();
        const int FileMustExist = 0x1000, PathMustExist = 0x800, NoChangeDirectory = 0x8, Explorer = 0x80000;
        const int PathCapacity = 4096;

        /// <summary>Returns the chosen path, or null when the player cancels.</summary>
        public static Task<string> PickAsync(string title, string description, string extension)
        {
            // The dialog runs on its own STA thread so Unity keeps rendering and networking;
            // the game window stays its owner, which keeps the dialog in front of it.
            IntPtr owner = GetActiveWindow();
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                IntPtr file = IntPtr.Zero, filter = IntPtr.Zero, caption = IntPtr.Zero;
                try
                {
                    file = Marshal.AllocHGlobal(PathCapacity * 2);
                    Marshal.WriteInt16(file, 0);
                    // Filter pairs are separated by NUL and the list ends with a double NUL.
                    filter = Marshal.StringToHGlobalUni($"{description} (*.{extension})\0*.{extension}\0\0");
                    caption = Marshal.StringToHGlobalUni(title);
                    var dialog = new OpenFileName
                    {
                        structSize = Marshal.SizeOf<OpenFileName>(), owner = owner, filter = filter, filterIndex = 1,
                        file = file, maxFile = PathCapacity, title = caption,
                        flags = FileMustExist | PathMustExist | NoChangeDirectory | Explorer
                    };
                    completion.TrySetResult(GetOpenFileName(ref dialog) ? Marshal.PtrToStringUni(file) : null);
                }
                catch (Exception error)
                {
                    completion.TrySetException(error);
                }
                finally
                {
                    if (file != IntPtr.Zero) Marshal.FreeHGlobal(file);
                    if (filter != IntPtr.Zero) Marshal.FreeHGlobal(filter);
                    if (caption != IntPtr.Zero) Marshal.FreeHGlobal(caption);
                }
            }) { IsBackground = true, Name = "PickleVerse file picker" };
            try { thread.SetApartmentState(ApartmentState.STA); }
            catch (Exception) { }
            thread.Start();
            return completion.Task;
        }
    }
}
#endif
