using UnityEngine;

namespace PickleVerse.LocalMatch
{
    // Small local preferences shared by the front-end and the local match only.
    public static class BotMatchLaunchSettings
    {
        const string DifficultyKey = "PKV.Bot.Difficulty";
        const string NightKey = "PKV.Bot.Night";
        const string VenueKey = "PKV.Bot.Venue";
        static bool launchRequested;
        public static int Difficulty => Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, 1), 0, 3);
        public static bool Night => PlayerPrefs.GetInt(NightKey, 0) == 1;
        public static CourtVenue Venue => CourtVenueSelection.Normalize((CourtVenue)PlayerPrefs.GetInt(VenueKey, 0));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLaunch() { launchRequested = false; }

        public static void Prepare(int difficulty, bool night)
        { Prepare(difficulty, night, Venue); }

        public static void Prepare(int difficulty, bool night, CourtVenue venue)
        {
            PlayerPrefs.SetInt(DifficultyKey, Mathf.Clamp(difficulty, 0, 3));
            PlayerPrefs.SetInt(NightKey, night ? 1 : 0);
            PlayerPrefs.SetInt(VenueKey, (int)CourtVenueSelection.Normalize(venue));
            PlayerPrefs.Save();
            launchRequested = true;
        }

        public static bool ConsumeLaunch()
        {
            bool requested = launchRequested;
            launchRequested = false;
            return requested;
        }
    }
}

