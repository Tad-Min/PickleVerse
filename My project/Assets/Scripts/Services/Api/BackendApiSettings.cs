using UnityEngine;

namespace PickleVerse.Services.Api
{
    [CreateAssetMenu(menuName = "PickleVerse/Backend API Settings")]
    public sealed class BackendApiSettings : ScriptableObject
    {
        public string baseUrl = "https://tad-min.io.vn";
        [Tooltip("Allow remote HTTP in the Editor and Development Builds. Release builds still require HTTPS.")]
        public bool allowRemoteHttpInDevelopment;
        [Range(5, 120)]
        public int timeoutSeconds = 30;
    }
}
