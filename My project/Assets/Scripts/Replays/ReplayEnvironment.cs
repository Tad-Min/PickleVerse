using UnityEngine;
namespace PickleVerse.Replays
{
    public sealed class ReplayEnvironment : MonoBehaviour
    {
        public Material daySky, nightSky, lampsOn, lampsOff;
        public Camera viewCamera;
        public Light sun;
        public Light[] poles, roofs;
        public Renderer[] poleEmitters, roofEmitters;
        void Awake()
        {
            // Replay uses the same presentation budget as a match; overlay controls stay sharp.
            if (!GetComponent<PickleVerse.Settings.MatchRenderBudget>())
                gameObject.AddComponent<PickleVerse.Settings.MatchRenderBudget>();
        }
        public void Apply(bool night)
        {
            RenderSettings.skybox = night ? nightSky : daySky;
            if (viewCamera)
                viewCamera.clearFlags = CameraClearFlags.Skybox;
            if (sun)
                sun.enabled = !night;
            RenderSettings.sun = night ? null : sun;
            foreach (var light in poles ?? System.Array.Empty<Light>())
                if (light)
                {
                    light.enabled = true;
                    light.intensity = night ? 65 : 8;
                }
            foreach (var light in roofs ?? System.Array.Empty<Light>())
                if (light)
                {
                    light.enabled = night;
                    light.intensity = night ? 110 : 0;
                }
            foreach (var face in poleEmitters ?? System.Array.Empty<Renderer>())
                if (face && lampsOn)
                    face.sharedMaterial = lampsOn;
            foreach (var face in roofEmitters ?? System.Array.Empty<Renderer>())
                if (face && lampsOn && lampsOff)
                    face.sharedMaterial = night ? lampsOn : lampsOff;
            RenderSettings.ambientSkyColor = night ? new Color(.055f, .078f, .125f) : new Color(.52f, .63f, .76f);
            RenderSettings.ambientEquatorColor = night ? new Color(.032f, .046f, .07f) : new Color(.34f, .4f, .48f);
            RenderSettings.ambientGroundColor = night ? new Color(.018f, .025f, .033f) : new Color(.2f, .23f, .25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = night ? new Color(.025f, .04f, .07f) : new Color(.43f, .52f, .66f);
            RenderSettings.fogStartDistance = 38;
            RenderSettings.fogEndDistance = night ? 90 : 140;
        }
    }
}
