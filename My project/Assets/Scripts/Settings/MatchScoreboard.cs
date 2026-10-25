using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PickleVerse.Settings
{
    public enum CourtClockMode { Waiting, Serve, Rally, Timeout, Break, Finished }

    // Shared broadcast score display. Rules and networking stay in their own controllers.
    public sealed class MatchScoreboard : MonoBehaviour
    {
        public TMP_Text context, gameCaption, game, firstName, secondName, phase, clock, serviceDetail;
        public HudScoreDigit firstPoints, secondPoints, firstGames, secondGames;
        public GameObject firstServing, secondServing;
        public Image clockFill;
        int gameNumber = -1, serving = -2, tenth = -1; CourtClockMode clockMode = (CourtClockMode)(-1);
        bool bounced;
        public int Serving => serving;
        public CourtClockMode ClockMode => clockMode;
        public void SetTeams(string first, string second, string description)
        {
            Set(firstName, first);
            Set(secondName, second);
            Set(context, description);
        }
        public void SetScores(int a, int b, int gamesA, int gamesB, int number, int server)
        {
            firstPoints.SetValue(a);
            secondPoints.SetValue(b);
            firstGames.SetValue(gamesA);
            secondGames.SetValue(gamesB);
            if (gameNumber != number)
            {
                gameNumber = number;
                game.text = number.ToString();
            }
            if (serving != server)
            {
                serving = server;
                firstServing.SetActive(server == 0);
                secondServing.SetActive(server == 1);
            }
        }
        public void SetClock(CourtClockMode mode, float seconds = 0, bool hasBounced = false, string detail = "")
        {
            bool changed = mode != clockMode || hasBounced != bounced;
            clockMode = mode;
            bounced = hasBounced;
            if (changed)
            {
                Set(phase, mode == CourtClockMode.Serve ? "GIAO BÓNG" : mode == CourtClockMode.Rally ? (hasBounced ? "BÓNG ĐÃ NẢY" : "CHỜ BÓNG NẢY") : mode == CourtClockMode.Timeout ? "HỘI Ý" : mode == CourtClockMode.Break ? "NGHỈ GIỮA GAME" : mode == CourtClockMode.Finished ? "KẾT THÚC" : "CHUẨN BỊ");
                tenth = -1;
            }
            Set(serviceDetail, detail);
            bool countdown = mode == CourtClockMode.Serve || mode == CourtClockMode.Timeout;
            int tick = countdown ? Mathf.CeilToInt(Mathf.Max(0, seconds) * 10) : -2;
            if (tick == tenth)
                return;
            tenth = tick;
            if (countdown)
            {
                clock.text = (tick / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s";
                Color color = seconds <= 3 ? (Color)new Color32(255, 153, 119, 255) : ScreenUiTheme.Gold;
                clock.color = color;
                clockFill.color = color;
                clockFill.fillAmount = Mathf.Clamp01(seconds / (mode == CourtClockMode.Timeout ? 60 : 10));
            }
            else
            {
                Set(clock, "");
                clockFill.fillAmount = 0;
            }
        }
        static void Set(TMP_Text label, string value)
        {
            if (label && label.text != value)
                label.text = value;
        }
    }
}
