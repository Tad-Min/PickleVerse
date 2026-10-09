using PickleVerse.CharacterMotion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using PickleVerse.Settings;

namespace PickleVerse.FrontEnd
{
    public sealed class HomeLobbyPresentation : MonoBehaviour, ISettingsHost
    {
        public Transform femaleVisual;
        public AnimationClip dance;
        public Material daySky, nightSky, blendedSky;
        public Light sun, portraitLight;
        public Light[] floodlights;
        public Renderer[] lampFaces;
        public Material lampsOff, lampsOn;
        public RectTransform safeArea;
        public TMP_Text timeLabel;
        public UnityEngine.UI.Button playButton;
        [Min(30)] public float cycleSeconds = 90;
        public bool animateAtmosphere = true;
        public float NightWeight { get; private set; }
        public float DanceTime { get; private set; }
        public float CycleElapsed { get; private set; }
        Material skyInstance;
        Transform[] bones;
        Vector3[] lastPositions, lastScales;
        Quaternion[] lastRotations;
        Vector3 restPosition, restScale;
        Quaternion restRotation;
        Rect lastSafeArea;
        Vector2 lastResolution;
        float lastReflectionWeight = -1;

        void Awake()
        {
            if (blendedSky)
            {
                skyInstance = new Material(blendedSky);
                RenderSettings.skybox = skyInstance;
            }
            if (femaleVisual && dance)
            {
                foreach (var animator in femaleVisual.GetComponentsInChildren<Animator>()) animator.enabled = false;
                foreach (var animation in femaleVisual.GetComponentsInChildren<Animation>()) animation.enabled = false;
                restPosition = femaleVisual.localPosition;
                restRotation = femaleVisual.localRotation;
                restScale = femaleVisual.localScale;
                CharacterSecondaryMotion.Attach(femaleVisual.gameObject, true);
                bones = femaleVisual.GetComponentsInChildren<Transform>(true);
                lastPositions = new Vector3[bones.Length]; lastScales = new Vector3[bones.Length]; lastRotations = new Quaternion[bones.Length];
                SampleDance(0);
            }
            EvaluateCycle(0);
            ApplySafeArea();
        }

        void Start()
        {
            if (EventSystem.current && playButton) EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }

        void Update()
        {
            DanceTime += Time.unscaledDeltaTime;
            SampleDance(DanceTime);
            if (animateAtmosphere) { CycleElapsed += Time.unscaledDeltaTime; EvaluateCycle(CycleElapsed); }
            ApplySafeArea();
        }

        public void SampleDance(float seconds)
        {
            if (!dance || !femaleVisual || bones == null) return;
            float time = Mathf.Repeat(seconds, Mathf.Max(.01f, dance.length));
            dance.SampleAnimation(femaleVisual.gameObject, time);
            // Blend back into the opening pose at the end of this authored celebration.
            const float seam = .35f;
            if (time > dance.length - seam)
            {
                for (int i = 0; i < bones.Length; i++)
                {
                    lastPositions[i] = bones[i].localPosition; lastRotations[i] = bones[i].localRotation; lastScales[i] = bones[i].localScale;
                }
                dance.SampleAnimation(femaleVisual.gameObject, 0);
                float weight = Mathf.SmoothStep(0, 1, (time - dance.length + seam) / seam);
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i].localPosition = Vector3.Lerp(lastPositions[i], bones[i].localPosition, weight);
                    bones[i].localRotation = Quaternion.Slerp(lastRotations[i], bones[i].localRotation, weight);
                    bones[i].localScale = Vector3.Lerp(lastScales[i], bones[i].localScale, weight);
                }
            }
            femaleVisual.SetLocalPositionAndRotation(restPosition, restRotation);
            femaleVisual.localScale = restScale;
        }

        public void EvaluateCycle(float seconds)
        {
            float phase = Mathf.Repeat(seconds / Mathf.Max(30, cycleSeconds), 1);
            float weight = phase < .22f ? 0 : phase < .42f ? Mathf.SmoothStep(0, 1, (phase - .22f) / .20f) : phase < .72f ? 1 : phase < .92f ? 1 - Mathf.SmoothStep(0, 1, (phase - .72f) / .20f) : 0;
            ApplyAtmosphere(weight);
            if (timeLabel) timeLabel.text = phase < .22f || phase >= .92f ? "BAN NGÀY" : phase < .42f ? "HOÀNG HÔN" : phase < .72f ? "BAN ĐÊM" : "BÌNH MINH";
        }

        public void ApplyAtmosphere(float night)
        {
            NightWeight = Mathf.Clamp01(night);
            var material = skyInstance ? skyInstance : blendedSky;
            if (material) { material.SetFloat("_NightBlend", NightWeight); RenderSettings.skybox = material; }
            if (sun)
            {
                sun.enabled = NightWeight < .995f;
                sun.intensity = 1.1f * (1 - NightWeight);
                sun.color = Color.Lerp(new Color(1,.93f,.82f), new Color(1,.68f,.43f), Mathf.Sin(NightWeight * Mathf.PI) * .4f);
                sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(28,8,NightWeight),-35,0);
            }
            RenderSettings.sun = sun && sun.enabled ? sun : null;
            if (floodlights != null) foreach (var light in floodlights) if (light) { light.enabled = NightWeight > .01f; light.intensity = 40 * NightWeight; }
            if (portraitLight) portraitLight.intensity = Mathf.Lerp(2.5f, 5.5f, NightWeight);
            if (lampFaces != null) foreach (var face in lampFaces) if (face && lampsOn && lampsOff) face.sharedMaterial = NightWeight > .35f ? lampsOn : lampsOff;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.52f,.63f,.76f),new Color(.055f,.078f,.125f),NightWeight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.34f,.40f,.48f),new Color(.032f,.046f,.070f),NightWeight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(.20f,.23f,.25f),new Color(.018f,.025f,.033f),NightWeight);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.reflectionIntensity = Mathf.Lerp(.65f,.20f,NightWeight);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 38;
            RenderSettings.fogEndDistance = Mathf.Lerp(140,90,NightWeight);
            RenderSettings.fogColor = Color.Lerp(new Color(.43f,.52f,.66f),new Color(.025f,.040f,.070f),NightWeight);
            if (Application.isPlaying && Mathf.Abs(lastReflectionWeight - NightWeight) >= .25f)
            {
                lastReflectionWeight = NightWeight;
                DynamicGI.UpdateEnvironment();
            }
        }

        void ApplySafeArea()
        {
            if (!safeArea || Screen.width <= 0 || Screen.height <= 0) return;
            var area = Screen.safeArea; var resolution = new Vector2(Screen.width, Screen.height);
            if (area == lastSafeArea && resolution == lastResolution) return;
            lastSafeArea = area; lastResolution = resolution;
            safeArea.anchorMin = new Vector2(area.xMin / resolution.x, area.yMin / resolution.y);
            safeArea.anchorMax = new Vector2(area.xMax / resolution.x, area.yMax / resolution.y);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        void OnDestroy()
        {
            if (!skyInstance) return;
            if (RenderSettings.skybox == skyInstance) RenderSettings.skybox = daySky;
            Destroy(skyInstance);
        }
        public bool IsGameplay => false;
        public bool IsPaused => false;
        public bool CanSignOut => true;
        public void SetPaused(bool paused) { }
        public void LeaveToMenu() { }
        public async System.Threading.Tasks.Task SignOutAsync()
        {
            await PickleVerse.Multiplayer.PlayerSignOut.FromLobbyAsync();
            var texts=safeArea.GetComponentsInChildren<TMP_Text>(true);
            foreach(var text in texts)if(text.name=="TxtName")text.text="Khách";else if(text.name=="TxtLevel")text.text="CHƯA ĐĂNG NHẬP";
        }
    }
}
