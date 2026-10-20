using UnityEngine;

namespace PickleVerse.Services.Api
{
    [CreateAssetMenu(menuName = "PickleVerse/Backend API Settings")]
    public sealed class BackendApiSettings : ScriptableObject
    {
        public string baseUrl = "http://localhost:8080";
        [Range(5, 120)] public int timeoutSeconds = 30;
    }
}
