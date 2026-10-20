using PickleVerse.CharacterMotion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using PickleVerse.Settings;
using PickleVerse.LocalMatch;

namespace PickleVerse.FrontEnd
{
    public sealed class HomeLobbyPresentation : MonoBehaviour, ISettingsHost
    {
        public Transform femaleVisual;
        public AnimationClip dance;
        public Transform maleVisual;
        public AnimationClip femaleBoom, maleDance, femaleIdle, maleIdle;
        public UnityEngine.UI.Button femaleButton, maleButton;
        public UnityEngine.UI.Image femaleSelection, maleSelection;
        public TMP_Text danceLabel;
        public Camera lobbyCamera;
        public Transform courtProps;
        public const string CharacterPreferenceKey = "PickleVerse.Lobby.Character.v1";
        public bool IsMaleSelected { get; private set; }
        public AnimationClip CurrentDance { get; private set; }
        public Material daySky, nightSky, blendedSky;
        public Light sun, portraitLight;
        public Light[] floodlights;
        public Renderer[] lampFaces;
        public Material lampsOff, lampsOn;
        public StadiumEnvironment stadium;
        public RectTransform safeArea;
        public TMP_Text timeLabel;
        public UnityEngine.UI.Button playButton;
        [Min(30)] public float cycleSeconds = 90;
        public bool animateAtmosphere = true;
        public float NightWeight { get; private set; }
        public float DanceTime { get; private set; }
        public float CycleElapsed { get; private set; }
        Material skyInstance;
        LobbyRig femaleRig, maleRig;
        Vector3 femaleCameraPosition, femaleLightPosition;
        Quaternion femaleCameraRotation, femaleLightRotation, propsRotation;
        float viewAngle, viewVelocity;
        Rect lastSafeArea;
        Vector2 lastResolution;
        float lastReflectionWeight = -1;

        void Awake()
        {
            if(!GetComponent<LobbyRenderBudget>())gameObject.AddComponent<LobbyRenderBudget>();
            var lobbyUi=safeArea?safeArea.GetComponentInParent<Canvas>():null;
            if(lobbyUi){lobbyUi.renderMode=RenderMode.ScreenSpaceOverlay;lobbyUi.worldCamera=null;}
            if (blendedSky)
            {
                skyInstance = new Material(blendedSky);
                RenderSettings.skybox = skyInstance;
            }
            if (femaleVisual) femaleRig = new LobbyRig(femaleVisual, true);
            if (maleVisual) maleRig = new LobbyRig(maleVisual, false);
            if (lobbyCamera) { femaleCameraPosition = lobbyCamera.transform.position; femaleCameraRotation = lobbyCamera.transform.rotation; }
            if (portraitLight) { femaleLightPosition = portraitLight.transform.position; femaleLightRotation = portraitLight.transform.rotation; }
            if (courtProps) propsRotation = courtProps.rotation;
            SetCharacter(maleRig != null && PickleVerse.Services.AccountRuntime.Ensure().Account.Profile?.IsMale == true, false);
            viewAngle = IsMaleSelected ? 180 : 0;
            ApplyView(viewAngle);
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
            float targetAngle = IsMaleSelected ? 180 : 0;
            if (Mathf.Abs(Mathf.DeltaAngle(viewAngle, targetAngle)) > .001f)
            {
                viewAngle = Mathf.SmoothDampAngle(viewAngle, targetAngle, ref viewVelocity, .26f, Mathf.Infinity, Time.unscaledDeltaTime);
                if (Mathf.Abs(Mathf.DeltaAngle(viewAngle, targetAngle)) < .01f) { viewAngle = targetAngle; viewVelocity = 0; }
                ApplyView(viewAngle);
            }
            if (animateAtmosphere) { CycleElapsed += Time.unscaledDeltaTime; EvaluateCycle(CycleElapsed); }
            ApplySafeArea();
        }

        public void SampleDance(float seconds)
        {
            LobbyRig rig = IsMaleSelected ? maleRig : femaleRig;
            AnimationClip clip = IsMaleSelected ? maleDance : dance;
            if (rig == null || !clip) return;
            AnimationClip next = clip;
            float time;
            if (!IsMaleSelected && femaleBoom)
            {
                time = Mathf.Repeat(seconds, Mathf.Max(.01f, dance.length + femaleBoom.length));
                if (time >= dance.length) { time -= dance.length; clip = femaleBoom; next = dance; }
                else next = femaleBoom;
            }
            else time = Mathf.Repeat(seconds, Mathf.Max(.01f, clip.length));
            rig.Sample(clip, time, next);
            if (CurrentDance == clip) return;
            CurrentDance = clip;
            if (danceLabel) danceLabel.text = IsMaleSelected ? "ALL NIGHT" : clip == femaleBoom ? "BOOM" : "DANCE WIN";
        }

        public void SelectFemale() { SetCharacter(false, true); }
        public void SelectMale() { if (maleRig != null) SetCharacter(true, true); }
        public void ApplyAccountCharacter(bool male) { SetCharacter(male && maleRig != null, false); }

        void SetCharacter(bool male, bool save)
        {
            if (save && IsMaleSelected == male) return;
            IsMaleSelected = male;
            DanceTime = 0;
            CurrentDance = null;
            femaleRig?.SetActive(!male, femaleIdle);
            maleRig?.SetActive(male, maleIdle);
            SampleDance(0);
            if (femaleSelection) femaleSelection.gameObject.SetActive(!male);
            if (maleSelection) maleSelection.gameObject.SetActive(male);
            SetButtonState(femaleButton, !male);
            SetButtonState(maleButton, male);
            // Both actors stay on their own half; the floor props follow the selected view.
            if (courtProps) courtProps.rotation = Quaternion.Euler(0, male ? 180 : 0, 0) * propsRotation;
            // AccountScreen persists character selection through the backend profile API.
        }

        static void SetButtonState(UnityEngine.UI.Button button, bool selected)
        {
            if (!button) return;
            var text = button.GetComponentInChildren<TMP_Text>();
            if (text) text.color = selected ? new Color(.91f, .80f, .63f) : new Color(.65f, .70f, .80f);
            var motion = button.GetComponent<UiButtonMotion>();
            if (motion) motion.keepFocus = selected;
        }

        void ApplyView(float angle)
        {
            Quaternion orbit = Quaternion.Euler(0, angle, 0);
            if (lobbyCamera) lobbyCamera.transform.SetPositionAndRotation(orbit * femaleCameraPosition, orbit * femaleCameraRotation);
            if (portraitLight) portraitLight.transform.SetPositionAndRotation(orbit * femaleLightPosition, orbit * femaleLightRotation);
        }
        public void RefreshView(){ApplyView(viewAngle);}

        // One sampled rig per frame. The other holds a static idle pose, with no animation
        // evaluation or secondary simulation while the lobby is suspended on another page.
        sealed class LobbyRig
        {
            readonly Transform visual;
            readonly Transform[] bones;
            readonly Vector3[] lastPositions, lastScales;
            readonly Quaternion[] lastRotations;
            readonly Vector3 restPosition, restScale;
            readonly Quaternion restRotation;
            readonly CharacterSecondaryMotion secondary;

            public LobbyRig(Transform root, bool female)
            {
                visual = root;
                foreach (var animator in root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var animation in root.GetComponentsInChildren<Animation>(true)) animation.enabled = false;
                restPosition = root.localPosition; restRotation = root.localRotation; restScale = root.localScale;
                secondary = CharacterSecondaryMotion.Attach(root.gameObject, female);
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = false;
                bones = root.GetComponentsInChildren<Transform>(true);
                lastPositions = new Vector3[bones.Length]; lastScales = new Vector3[bones.Length]; lastRotations = new Quaternion[bones.Length];
            }

            public void SetActive(bool active, AnimationClip idle)
            {
                if (!active && idle) Sample(idle, 0, null);
                secondary.ResetMotion();
                secondary.enabled = active;
            }

            public void Sample(AnimationClip clip, float time, AnimationClip next)
            {
                clip.SampleAnimation(visual.gameObject, time);
                float seam = Mathf.Min(.35f, clip.length * .2f);
                if (next && time > clip.length - seam)
                {
                    for (int i = 0; i < bones.Length; i++)
                    {
                        lastPositions[i] = bones[i].localPosition; lastRotations[i] = bones[i].localRotation; lastScales[i] = bones[i].localScale;
                    }
                    next.SampleAnimation(visual.gameObject, 0);
                    float weight = Mathf.SmoothStep(0, 1, (time - clip.length + seam) / seam);
                    for (int i = 0; i < bones.Length; i++)
                    {
                        bones[i].localPosition = Vector3.Lerp(lastPositions[i], bones[i].localPosition, weight);
                        bones[i].localRotation = Quaternion.Slerp(lastRotations[i], bones[i].localRotation, weight);
                        bones[i].localScale = Vector3.Lerp(lastScales[i], bones[i].localScale, weight);
                    }
                }
                visual.SetLocalPositionAndRotation(restPosition, restRotation);
                visual.localScale = restScale;
            }
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
                sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(stadium?55:28,stadium?18:8,NightWeight),-35,0);
            }
            RenderSettings.sun = sun && sun.enabled ? sun : null;
            if (floodlights != null) foreach (var light in floodlights) if (light) { light.enabled = NightWeight > .01f; light.intensity = 40 * NightWeight; }
            if (portraitLight) {portraitLight.intensity = Mathf.Lerp(2.5f, 4f, NightWeight);portraitLight.color=Color.white;portraitLight.useColorTemperature=false;}
            if (lampFaces != null) foreach (var face in lampFaces) if (face && lampsOn && lampsOff) face.sharedMaterial = NightWeight > .35f ? lampsOn : lampsOff;
            if(stadium)stadium.ApplyNight(NightWeight);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.52f,.63f,.76f),MatchEnvironmentLighting.NightAmbientSky,NightWeight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.34f,.40f,.48f),MatchEnvironmentLighting.NightAmbientEquator,NightWeight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(.20f,.23f,.25f),MatchEnvironmentLighting.NightAmbientGround,NightWeight);
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
            FindAnyObjectByType<PlayerProgressionMenu>()?.Refresh();
        }
    }
}
