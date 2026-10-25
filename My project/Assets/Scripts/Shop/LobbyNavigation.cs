using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Profiling;
using System.Collections.Generic;

namespace PickleVerse.Shop
{
    /// <summary>Keep one suspended lobby for front-end pages and Training. Match Single loads release it.</summary>
    public sealed class LobbyNavigation : MonoBehaviour
    {
        static LobbyNavigation instance;
        Scene lobby;
        GameObject[] roots;
        Behaviour[] behaviours;
        bool[] behaviourEnabled;
        Renderer[] renderers;
        bool[] rendererEnabled;
        Collider[] colliders;
        bool[] colliderEnabled;
        GameObject previousSelection;
        bool suspended;
        AudioListener transitionAudio;
        string audioDestination;
        static readonly ProfilerMarker restoreLighting = new ProfilerMarker("Lobby.Return.Lighting");
        static readonly ProfilerMarker restoreComponents = new ProfilerMarker("Lobby.Return.Components");
        public bool Busy { get; private set; }
        public static bool IsBusy => instance && instance.Busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            instance = null;
        }
        static LobbyNavigation Ensure()
        {
            if (instance)
                return instance;
            var root = new GameObject("Lobby Navigation");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<LobbyNavigation>();
            return instance;
        }
        void Awake()
        {
            SceneManager.sceneUnloaded += SceneUnloaded;
        }
        void OnDestroy()
        {
            EndAudioHandoff();
            SceneManager.sceneUnloaded -= SceneUnloaded;
            if (instance == this)
                instance = null;
        }
        void SceneUnloaded(Scene scene)
        {
            if (scene != lobby)
                return;
            EndAudioHandoff();
            StopAllCoroutines();
            Busy = false;
            lobby = default;
            roots = null;
            behaviours = null;
            behaviourEnabled = null;
            renderers = null;
            rendererEnabled = null;
            colliders = null;
            colliderEnabled = null;
            previousSelection = null;
            suspended = false;
        }
        public static void OpenShop()
        {
            Ensure().Open("Shop");
        }
        public static void OpenCollection()
        {
            Ensure().Open("Collection");
        }
        public static void OpenModes()
        {
            Ensure().Open("ModeSelection");
        }
        public static void OpenTraining()
        {
            PickleVerse.Settings.SceneLoading.Load("Training", () => Ensure().Open("Training"), () => !IsBusy);
        }
        public static void OpenReplay()
        {
            Ensure().Open("Replay");
        }
        public static void ReturnToLobby()
        {
            var navigation = Ensure();
            if (!navigation.Busy)
                navigation.StartCoroutine(navigation.Return());
        }
        void Open(string destination)
        {
            if (Busy)
                return;
            if (!Application.CanStreamedLevelBeLoaded(destination))
            {
                Debug.LogError("Scene unavailable: " + destination);
                return;
            }
            StartCoroutine(Enter(destination));
        }
        IEnumerator Enter(string destination)
        {
            Busy = true;
            bool completed = false;
            try
            {
                var current = SceneManager.GetActiveScene();
                if (current.name != "Menu")
                {
                    if (suspended && lobby.IsValid() && lobby.isLoaded)
                    {
                        yield return ReplacePage(current, destination);
                        completed = true;
                        yield break;
                    }
                    var single = SceneManager.LoadSceneAsync(destination);
                    PickleVerse.Settings.SceneLoading.Track(single);
                    yield return single;
                    completed = true;
                    yield break;
                }
                lobby = current;
                roots = lobby.GetRootGameObjects();
                previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
                CacheComponents();
                if (EventSystem.current)
                    EventSystem.current.SetSelectedGameObject(null);
                foreach (var component in behaviours)
                    if (component is MonoBehaviour && component is not GraphicRaycaster)
                        component.enabled = false;
                // Stream assets while the existing lobby still renders. Pause its components
                // before activating the new scene, so cameras and EventSystems never overlap.
                var load = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Additive);
                PickleVerse.Settings.SceneLoading.Track(load);
                load.allowSceneActivation = false;
                while (load.progress < .9f)
                    yield return null;
                if (EventSystem.current)
                    EventSystem.current.SetSelectedGameObject(null);
                BeginAudioHandoff(destination);
                SuspendComponents();
                suspended = true;
                load.allowSceneActivation = true;
                yield return load;
                SceneManager.SetActiveScene(SceneManager.GetSceneByName(destination));
                completed = true;
            }
            finally
            {
                EndAudioHandoff();
                Busy = false;
                if (!completed && lobby.IsValid() && lobby.isLoaded && behaviours != null)
                {
                    SceneManager.SetActiveScene(lobby);
                    ResumeComponents();
                    suspended = false;
                }
            }
        }
        IEnumerator ReplacePage(Scene outgoing, string destination)
        {
            // Keep the already initialized lobby suspended while moving between modes
            // and Training. Only the outgoing page is unloaded, never the lobby.
            var load = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Additive);
            PickleVerse.Settings.SceneLoading.Track(load);
            load.allowSceneActivation = false;
            while (load.progress < .9f)
                yield return null;
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);
            BeginAudioHandoff(destination);
            foreach (var root in outgoing.GetRootGameObjects())
                root.SetActive(false);
            load.allowSceneActivation = true;
            yield return load;
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(destination));
            yield return null;
            if (outgoing.IsValid() && outgoing.isLoaded && outgoing != lobby)
                yield return SceneManager.UnloadSceneAsync(outgoing);
        }
        IEnumerator Return()
        {
            Busy = true;
            try
            {
                if (SceneManager.GetActiveScene().name == "Menu")
                    yield break;
                if (!suspended || !lobby.IsValid() || !lobby.isLoaded)
                {
                    yield return SceneManager.LoadSceneAsync("Menu");
                    yield break;
                }
                var outgoing = SceneManager.GetActiveScene();
                if (EventSystem.current)
                    EventSystem.current.SetSelectedGameObject(null);
                foreach (var root in outgoing.GetRootGameObjects())
                    root.SetActive(false);
                using (restoreLighting.Auto())
                    SceneManager.SetActiveScene(lobby);
                using (restoreComponents.Auto())
                    ResumeComponents();
                suspended = false;
                if (EventSystem.current && previousSelection && previousSelection.activeInHierarchy)
                    EventSystem.current.SetSelectedGameObject(previousSelection);
                // Render the restored lobby first. Additive unload removes the outgoing
                // UI without Single-load's global unused-asset sweep on the return frame.
                yield return null;
                if (outgoing != lobby && outgoing.IsValid() && outgoing.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(outgoing);
            }
            finally
            {
                Busy = false;
            }
        }
        void BeginAudioHandoff(string destination)
        {
            if (!transitionAudio)
            {
                var output = new GameObject("Scene Transition Audio", typeof(AudioListener));
                output.transform.SetParent(transform, false);
                transitionAudio = output.GetComponent<AudioListener>();
            }
            if (Camera.main)
                transitionAudio.transform.SetPositionAndRotation(Camera.main.transform.position, Camera.main.transform.rotation);
            audioDestination = destination;
            transitionAudio.enabled = true;
            SceneManager.sceneLoaded -= AudioSceneLoaded;
            SceneManager.sceneLoaded += AudioSceneLoaded;
        }
        void AudioSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // sceneLoaded runs after the destination listeners are enabled and before
            // its first rendered frame, so there is neither a silent gap nor two listeners.
            if (scene.name == audioDestination)
                EndAudioHandoff();
        }
        void EndAudioHandoff()
        {
            SceneManager.sceneLoaded -= AudioSceneLoaded;
            audioDestination = null;
            if (transitionAudio)
                transitionAudio.enabled = false;
        }
        void CacheComponents()
        {
            var paused = new List<Behaviour>();
            var visuals = new List<Renderer>();
            var physics = new List<Collider>();
            foreach (var root in roots)
            {
                foreach (var component in root.GetComponentsInChildren<Behaviour>(true))
                {
                    if (!component)
                        continue;
                    var ns = component.GetType().Namespace ?? "";
                    if (component is Canvas || component is Camera || component is Light || component is AudioListener ||
                       component is Animation || component is Animator || component is EventSystem || component is BaseInputModule || component is GraphicRaycaster ||
                       (component is MonoBehaviour && !ns.StartsWith("UnityEngine", StringComparison.Ordinal) && !ns.StartsWith("TMPro", StringComparison.Ordinal)))
                        paused.Add(component);
                }
                visuals.AddRange(root.GetComponentsInChildren<Renderer>(true));
                physics.AddRange(root.GetComponentsInChildren<Collider>(true));
            }
            behaviours = paused.ToArray();
            behaviourEnabled = new bool[behaviours.Length];
            for (int i = 0; i < behaviours.Length; i++)
                behaviourEnabled[i] = behaviours[i].enabled;
            renderers = visuals.ToArray();
            rendererEnabled = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                rendererEnabled[i] = renderers[i].enabled;
            colliders = physics.ToArray();
            colliderEnabled = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
                colliderEnabled[i] = colliders[i].enabled;
        }
        void SuspendComponents()
        {
            // Keep TMP/UI GameObjects alive: toggling their roots would rebuild every
            // text mesh and Canvas batch on resume. Disable only rendering, input and simulation.
            foreach (var component in behaviours)
                if (component)
                    component.enabled = false;
            foreach (var renderer in renderers)
                if (renderer)
                    renderer.enabled = false;
            foreach (var collider in colliders)
                if (collider)
                    collider.enabled = false;
        }
        void ResumeComponents()
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i])
                    renderers[i].enabled = rendererEnabled[i];
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i])
                    colliders[i].enabled = colliderEnabled[i];
            // Refresh inventory after restoring old renderer states, so an equipment
            // change made in Collection can hide/show the correct default/skin mesh.
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i])
                    behaviours[i].enabled = behaviourEnabled[i];
        }
    }
}
