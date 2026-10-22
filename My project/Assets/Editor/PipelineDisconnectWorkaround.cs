using System;
using System.Net;
using System.Reflection;
using UnityEditor;

namespace PickleVerse.EditorTools
{
    /// <summary>
    /// Pipeline 0.7 retries an HTTP error response after a command-list client disconnects,
    /// which then throws again because the response headers have already been sent.
    /// Configure only its Editor listener to tolerate disconnected response readers.
    /// </summary>
    [InitializeOnLoad]
    internal static class PipelineDisconnectWorkaround
    {
        static readonly Type StartupType = Type.GetType(
            "Unity.Pipeline.Editor.PipelineServerStartup, Unity.Pipeline.Editor");
        static readonly PropertyInfo ServerProperty = StartupType?.GetProperty(
            "Server", BindingFlags.Public | BindingFlags.Static);
        static readonly FieldInfo ListenerField = ServerProperty?.PropertyType.BaseType?.GetField(
            "m_HttpListener", BindingFlags.NonPublic | BindingFlags.Instance);

        static double nextCheck;

        static PipelineDisconnectWorkaround()
        {
            EditorApplication.update += ApplyWhenReady;
            AssemblyReloadEvents.beforeAssemblyReload += Unsubscribe;
        }

        static void ApplyWhenReady()
        {
            if (EditorApplication.timeSinceStartup < nextCheck)
                return;
            nextCheck = EditorApplication.timeSinceStartup + 1;

            // Pipeline is optional. Keep this isolated from game/API networking and
            // tolerate its server being stopped or replaced across play-mode changes.
            if (ServerProperty == null || ListenerField == null)
                return;

            var server = ServerProperty.GetValue(null);
            if (server == null)
                return;

            try
            {
                if (ListenerField.GetValue(server) is HttpListener listener && listener.IsListening)
                    listener.IgnoreWriteExceptions = true;
            }
            catch (ObjectDisposedException)
            {
                // The Pipeline watchdog can replace the listener between the read and write.
            }
        }

        static void Unsubscribe()
        {
            EditorApplication.update -= ApplyWhenReady;
        }
    }
}
