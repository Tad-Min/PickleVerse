using UnityEngine;

namespace PickleVerse.LocalMatch
{
    // Only the active venue is instantiated. Court physics always use the shared rules plane.
    [DefaultExecutionOrder(-400)]
    public sealed class CourtVenueSelection : MonoBehaviour
    {
        public StadiumEnvironment current;
        public CourtVenue selected;
        public bool useBotPreference;
        public bool includeAudience;
        public CourtVenue Selected => selected;
        public static CourtVenue Normalize(CourtVenue venue) => venue == CourtVenue.LowPoly ? venue : CourtVenue.Professional;
        public static string DisplayName(CourtVenue venue) => Normalize(venue) == CourtVenue.LowPoly ? "Sân ngoài trời" : "Sân chuyên nghiệp";
        public static string ResourcePath(CourtVenue venue) => "Courts/" + (Normalize(venue) == CourtVenue.LowPoly ? "LowPoly" : "Professional");

        void Awake()
        {
            Select(useBotPreference ? BotMatchLaunchSettings.Venue : selected);
        }

        public bool Select(CourtVenue venue)
        {
            venue = Normalize(venue);
            if (!current || selected != venue)
            {
                var prefab = Resources.Load<GameObject>(ResourcePath(venue));
                if (!prefab)
                {
                    Debug.LogError("Missing court: " + ResourcePath(venue), this);
                    return false;
                }
                var replacement = Instantiate(prefab).GetComponent<StadiumEnvironment>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(replacement.gameObject, gameObject.scene);
                var previous = current;
                current = replacement;
                selected = venue;
                if (previous)
                {
                    previous.gameObject.SetActive(false);
                    Destroy(previous.gameObject);
                }
            }
            Bind();
            return true;
        }

        void Bind()
        {
            if (includeAudience)
                StadiumAudience.Ensure(current, selected);
            var lighting = GetComponent<MatchEnvironmentLighting>();
            if (lighting)
            {
                lighting.stadium = current;
                lighting.Apply(lighting.selectedNight);
            }
            var roots = gameObject.scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                foreach (var intro in root.GetComponentsInChildren<BotMatchIntro>(true))
                {
                    intro.playerEntrance = current.nearEntrance;
                    intro.opponentEntrance = current.farEntrance;
                }
                foreach (var result in root.GetComponentsInChildren<BotMatchVictory>(true))
                {
                    result.nearPresentation = current.nearResult;
                    result.farPresentation = current.farResult;
                }
            }
        }
    }
}
