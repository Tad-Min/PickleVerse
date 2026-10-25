using UnityEngine;
using PickleVerse.Rules;
using System.Linq;

namespace PickleVerse.LocalMatch
{
    /// <summary>The imported stadium is calibrated to the shared metre-based rules plane.</summary>
    public sealed class StadiumEnvironment : MonoBehaviour
    {
        public const string SourceAsset = "Assets/Models/Stadium_Enhanced/Stadium_Enhanced.glb";
        public Light[] poleLights, roofLights;
        // All scanned lamp anchors remain available. One source per Y cluster and two per roof bank
        // illuminate the same fixture group; the other lamp faces use the shared emissive material.
        public Light[] poleIllumination, roofIllumination, standIllumination;
        public float roofNightIntensity = 80, poleNightIntensity = 45, standNightIntensity = 8;
        public Renderer[] poleEmitters, roofEmitters, decorEmitters;
        public Material lightsOn, lightsOff;
        public MeshFilter competitionSurface, kitchenSurface, courtLines, netStructure;
        public Transform nearEntrance, farEntrance, nearResult, farResult;
        public Vector2 courtsideCameraLimits;
        public Vector3 KeepCameraInside(Vector3 position)
        {
            if (position.y < 4 && courtsideCameraLimits.x > 0 && courtsideCameraLimits.y > 0)
            {
                position.x = Mathf.Clamp(position.x, -courtsideCameraLimits.x, courtsideCameraLimits.x);
                position.z = Mathf.Clamp(position.z, -courtsideCameraLimits.y, courtsideCameraLimits.y);
            }
            return position;
        }
        [Range(0, 1)]
        public float nightWeight;
        float applied = -1, appliedMinimum = -1; bool previousRoof;
        /// <summary>Authoring utility: retain the scanned anchors, group closely spaced lamp heads.</summary>
        public void ConfigureFixtureGroups()
        {
            var poleGroups = poleLights.GroupBy(l => (Mathf.Abs(l.transform.localPosition.z) < 11 ? "near" : "far") + "_" + Mathf.Sign(l.transform.localPosition.x) + "_" + Mathf.Sign(l.transform.localPosition.z)).ToArray();
            poleIllumination = poleGroups.Select(g => g.OrderByDescending(l => l.shadows != LightShadows.None).ThenBy(l => l.transform.localPosition.sqrMagnitude).First()).ToArray();
            standIllumination = poleGroups.Select(g => g.Where(l => !poleIllumination.Contains(l)).OrderByDescending(l => Mathf.Abs(l.transform.localPosition.x)).First()).ToArray();
            foreach (var light in standIllumination)
            {
                var p = light.transform.position;
                bool corner = Mathf.Abs(p.z) > 11;
                light.transform.LookAt(new Vector3(Mathf.Sign(p.x) * (corner ? 13.8f : 15.6f), 2.4f, corner ? Mathf.Sign(p.z) * 17.5f : p.z));
                light.range = 16;
                light.spotAngle = 105;
                light.innerSpotAngle = 60;
                light.shadows = LightShadows.None;
            }
            roofIllumination = roofLights.GroupBy(l =>
            {
                var p = l.transform.localPosition;
                if (Mathf.Abs(p.z) < 11)
                    return "side" + Mathf.Sign(p.x) + "_" + Mathf.Sign(p.z);
                if (Mathf.Abs(p.x) < 5.5f)
                    return "end" + Mathf.Sign(p.z);
                return "corner" + Mathf.Sign(p.x) + "_" + Mathf.Sign(p.z);
            }).SelectMany(g =>
            {
                var row = g.OrderBy(l => l.transform.localPosition.x).ThenBy(l => l.transform.localPosition.z).ToArray();
                return g.Key.StartsWith("corner") ? new[] { row[row.Length / 2] } : new[] { row[row.Length / 3], row[row.Length * 2 / 3] };
            }).ToArray();
            foreach (var light in roofIllumination)
            {
                var p = light.transform.position;
                light.transform.LookAt(new Vector3(Mathf.Clamp(p.x * .08f, -1.6f, 1.6f), 0, Mathf.Clamp(p.z * .25f, -4.8f, 4.8f)));
                light.range = 32;
                light.spotAngle = 50;
                light.innerSpotAngle = 32.5f;
                light.shadows = LightShadows.None;
            }
            applied = -1;
            foreach (var light in poleLights.Concat(roofLights))
                if (light)
                {
                    light.color = Color.white;
                    light.useColorTemperature = false;
                    light.colorTemperature = 6500;
                }
        }
        public void ApplyNight(float night, bool force = false, float minimumCourtIllumination = 0)
        {
            if (force)
                applied = -1;
            night = Mathf.Clamp01(night);
            minimumCourtIllumination = Mathf.Clamp01(minimumCourtIllumination);
            if (applied >= 0 && Mathf.Abs(applied - night) < .003f && Mathf.Abs(appliedMinimum - minimumCourtIllumination) < .003f)
                return;
            bool first = applied < 0;
            nightWeight = night;
            applied = night;
            appliedMinimum = minimumCourtIllumination;
            float courtLight = Mathf.Max(night, minimumCourtIllumination);
            bool roof = courtLight > .025f, nightStands = night > .025f;
            foreach (var l in poleLights)
                if (l)
                    l.enabled = false;
            foreach (var l in roofLights)
                if (l)
                    l.enabled = false;
            var poles = poleIllumination != null && poleIllumination.Length > 0 ? poleIllumination : poleLights;
            var roofs = roofIllumination != null && roofIllumination.Length > 0 ? roofIllumination : roofLights;
            foreach (var l in poles)
                if (l)
                {
                    l.enabled = true;
                    l.intensity = Mathf.Lerp(8f, poleNightIntensity, courtLight);
                }
            foreach (var l in roofs)
                if (l)
                {
                    l.enabled = roof;
                    l.intensity = roofNightIntensity * courtLight;
                }
            if (standIllumination != null)
                foreach (var l in standIllumination)
                    if (l)
                    {
                        l.enabled = nightStands;
                        l.intensity = standNightIntensity * night;
                    }
            if (lightsOn && lightsOff)
            {
                if (first)
                    foreach (var r in poleEmitters)
                        if (r)
                            r.sharedMaterial = lightsOn;
                if (first || previousRoof != roof)
                {
                    foreach (var r in roofEmitters)
                        if (r)
                            r.sharedMaterial = roof ? lightsOn : lightsOff;
                    foreach (var r in decorEmitters)
                        if (r)
                            r.sharedMaterial = roof ? lightsOn : lightsOff;
                }
            }
            previousRoof = roof;
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.up * .015f, new Vector3(Court.HalfWidth * 2, .03f, Court.HalfLength * 2));
            Gizmos.color = Color.yellow;
            foreach (int end in new[] { -1, 1 })
                Gizmos.DrawWireCube(new Vector3(0, .02f, end * Court.Kitchen * .5f), new Vector3(Court.HalfWidth * 2, .03f, Court.Kitchen));
            Gizmos.color = Color.white;
            for (int i = 0; i < 32; i++)
            {
                float a = Mathf.Lerp(-Court.NetHalfSpan, Court.NetHalfSpan, i / 32f), b = Mathf.Lerp(-Court.NetHalfSpan, Court.NetHalfSpan, (i + 1) / 32f);
                Gizmos.DrawLine(new Vector3(a, Court.NetHeight(a), 0), new Vector3(b, Court.NetHeight(b), 0));
            }
        }
    }
}
