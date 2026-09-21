using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class PackageSearch
    {
        const double TimeoutSeconds = 120;
        static SearchRequest _request;
        static double _deadline;

        public static void SearchSentis()
        {
            _request = Client.Search("com.unity.sentis");
            _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_request == null) return;
            if (!_request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError("[PackageSearch] Timed out.");
                    EditorApplication.Exit(2);
                }
                return;
            }
            EditorApplication.update -= Poll;

            if (_request.Status == StatusCode.Success)
            {
                foreach (var p in _request.Result.OrderBy(p => p.name))
                    Debug.Log($"[PackageSearch] {p.name}@{p.versions.latestCompatible}  {p.displayName}");
                Debug.Log($"[PackageSearch] {_request.Result.Length} packages found.");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[PackageSearch] Failed: {_request.Error?.message}");
                EditorApplication.Exit(1);
            }
        }
    }
}
