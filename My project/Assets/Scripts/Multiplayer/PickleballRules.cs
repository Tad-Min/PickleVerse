using System;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    /// <summary>Pure host-side rally/scoring state. World court: X width, Z length, net at Z=0.</summary>
    [Serializable] public sealed class PickleballRules
    {
        public const float HalfWidth = 3.048f, HalfLength = 6.7056f, Kitchen = 2.1336f, BallRadius = .037f;
        public PlayMode mode;
        public int[] score = new int[2];
        public int servingTeam, serverIndex, serverNumber = 2, receiverIndex;
        public bool waitingServe = true, live, complete, serveFlight, awaitingServeReturn;
        public int winner = -1, lastHitTeam = -1, lastHitPlayer = -1, bounceCount, strokeCount;
        public bool[] openingBounce = new bool[2];
        // courtRight[t] identifies the teammate occupying the right service court before a serve.
        public int[] courtRight = new int[2];
        public bool[] volleyMomentum = new bool[4];
        public string reason = "Sẵn sàng giao bóng";
        public float serverWorldX;
        public int pendingFault = -1;

        public PickleballRules(PlayMode mode) { this.mode = mode; NewRally(); }
        public static int Side(float z) => z < 0 ? 0 : 1;
        public static float Facing(int team) => team == 0 ? 1 : -1;
        public static bool InKitchen(Vector3 feet, float radius = .13f) => Mathf.Abs(feet.z) <= Kitchen + radius && Mathf.Abs(feet.x) <= HalfWidth + radius;
        public bool RightServe => mode==PlayMode.Doubles?serverIndex==courtRight[servingTeam]:score[servingTeam]%2==0;
        public string ScoreCall => mode == PlayMode.Doubles ? $"{score[servingTeam]}–{score[1-servingTeam]}–{serverNumber}" : $"{score[servingTeam]}–{score[1-servingTeam]}";
        public void NewRally()
        {
            if (complete) return;
            waitingServe = true; live = serveFlight = awaitingServeReturn = false; pendingFault = -1;
            lastHitTeam = lastHitPlayer = -1; bounceCount = strokeCount = 0;
            openingBounce[0] = openingBounce[1] = false;
            Array.Clear(volleyMomentum, 0, volleyMomentum.Length);
            if (mode == PlayMode.Doubles)
            {
                // Teammates switch courts only after scoring. The second server stays
                // on their existing court; after side-out the player on the right serves first.
                receiverIndex = RightServe ? courtRight[1-servingTeam] : 1 - courtRight[1-servingTeam];
            }
            else receiverIndex = serverIndex = 0;
            serverWorldX = (RightServe ? 1 : -1) * Facing(servingTeam) * HalfWidth * .5f;
            reason = "Giao bóng: " + ScoreCall;
        }
        public bool Serve(int team, int index, Vector3 feet, Vector3 contact, Vector3 paddleHead, Vector3 wrist, Vector3 paddleVelocity)
        {
            if (!waitingServe || complete || team != servingTeam || index != serverIndex) return false;
            bool right = feet.x * Facing(team) > 0;
            if (Side(feet.z) != team || Mathf.Abs(feet.z) < HalfLength + .13f || Mathf.Abs(feet.x) > HalfWidth - .13f || Mathf.Abs(feet.x) < .13f || right != RightServe)
            { Fault(team, "Lỗi chân giao bóng"); return false; }
            if (contact.y > feet.y + 1.05f || paddleHead.y > wrist.y + .06f || paddleVelocity.y <= 0)
            { Fault(team, "Giao bóng phải đánh từ thấp lên, dưới eo"); return false; }
            waitingServe = false; live = serveFlight = awaitingServeReturn = true;
            lastHitTeam = team; lastHitPlayer = team * 2 + index; strokeCount = 1; bounceCount = 0;
            reason = "Bóng đang chơi"; return true;
        }
        public bool Hit(int team, int index, Vector3 feet, Vector3 ball, ShotKind kind)
        {
            if (!live || complete) return false;
            bool volley = bounceCount == 0;
            if (team == lastHitTeam) { Fault(team, "Đồng đội chạm bóng liên tiếp"); return false; }
            bool returningAcrossNet=bounceCount>0 && team==1-lastHitTeam;
            if (ball.z * Facing(team) > -BallRadius && !returningAcrossNet) { Fault(team, "Chạm bóng trước khi qua lưới hoàn toàn"); return false; }
            if (awaitingServeReturn && index != receiverIndex) { Fault(team, "Sai người nhận giao bóng"); return false; }
            if (volley && !openingBounce[team]) { Fault(team, "Phải để bóng nảy lần đầu mỗi bên sân"); return false; }
            if (volley && InKitchen(feet)) { Fault(team, "Volley trong kitchen hoặc chạm vạch"); return false; }
            // A requested technique never silently changes into a different legal shot.
            if ((kind == ShotKind.Volley || kind == ShotKind.Punch || kind == ShotKind.ResetVolley) && !volley) return false;
            if ((kind == ShotKind.Groundstroke || kind == ShotKind.Dink || kind == ShotKind.Drop || kind == ShotKind.SpeedUp) && volley) return false;
            if ((kind == ShotKind.Dink || kind == ShotKind.SpeedUp) && Mathf.Abs(feet.z) > Kitchen + 1.5f) return false;
            volleyMomentum[team * 2 + index] = volley;
            lastHitTeam = team; lastHitPlayer = team * 2 + index;
            bounceCount = 0; strokeCount++; serveFlight = awaitingServeReturn = false;
            return true;
        }
        public void Bounce(Vector3 contact)
        {
            if (!live || complete) return;
            // After a legal first bounce, any second bounce loses the rally for the receiver,
            // including a bounce outside the court. The physics contact point decides line calls.
            if(bounceCount>=1) { Fault(1-lastHitTeam,"Bóng nảy hai lần"); return; }
            bool outside = Mathf.Abs(contact.x) > HalfWidth + .001f || Mathf.Abs(contact.z) > HalfLength + .001f;
            if (outside) { Fault(lastHitTeam, "Bóng ngoài sân"); return; }
            int side = Side(contact.z);
            if (side == lastHitTeam) { Fault(lastHitTeam, "Bóng không qua sân đối phương"); return; }
            if (serveFlight)
            {
                if (Mathf.Abs(contact.z) <= Kitchen + .001f || contact.x * serverWorldX >= 0)
                { Fault(servingTeam, "Giao bóng sai ô hoặc chạm kitchen"); return; }
                serveFlight = false;
            }
            bounceCount++;
            openingBounce[side] = true;
            if (bounceCount >= 2) Fault(side, "Bóng nảy hai lần");
        }
        public void CheckMomentum(int team, int index, Vector3 feet, Vector3 velocity)
        {
            int slot = team * 2 + index;
            if (!volleyMomentum[slot] || complete) return;
            if (InKitchen(feet))
            {
                // Momentum faults can override the provisional dead-ball outcome before scoring.
                live = waitingServe = false; pendingFault = team; reason = "Đà volley đưa người vào kitchen";
                volleyMomentum[slot] = false; return;
            }
            // Balance is restored only once motion towards the NVZ has stopped.
            if (velocity.z * Facing(team) <= .05f) volleyMomentum[slot] = false;
        }
        public void BodyContact(int team) { if (live) Fault(team, "Bóng chạm người"); }
        public void Fault(int losingTeam, string message)
        {
            if (complete || pendingFault >= 0 || losingTeam < 0 || losingTeam > 1 || (!live && !waitingServe)) return;
            live = waitingServe = false;
            pendingFault = losingTeam; reason = message;
        }
        public void SettleRally()
        {
            if (pendingFault < 0 || HasMomentum() || complete) return;
            int losingTeam = pendingFault; pendingFault = -1;
            int winningTeam = 1 - losingTeam;
            if (winningTeam == servingTeam)
            {
                score[servingTeam]++;
                if (mode == PlayMode.Doubles) courtRight[servingTeam] = 1 - courtRight[servingTeam];
                if (score[servingTeam] >= 11 && score[servingTeam] - score[1-servingTeam] >= 2)
                { complete = true; winner = servingTeam; }
            }
            else if (mode == PlayMode.Doubles && serverNumber == 1)
            { serverNumber = 2; serverIndex = 1 - serverIndex; }
            else
            {
                servingTeam = winningTeam;
                serverNumber = mode == PlayMode.Doubles ? 1 : 2;
                serverIndex = mode == PlayMode.Doubles ? courtRight[servingTeam] : 0;
            }
        }
        bool HasMomentum() => Array.Exists(volleyMomentum, m => m);
        public void Forfeit(int team, string message) { if (!complete) { complete = true; live = waitingServe = false; winner = 1-team; reason = message; } }
    }
}
