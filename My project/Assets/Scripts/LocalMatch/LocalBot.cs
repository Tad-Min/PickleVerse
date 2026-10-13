using UnityEngine;

namespace PickleVerse.LocalMatch
{
    // Fixed difficulty baseline. Inputs pass through the same actor/rules as the human.
    public sealed class LocalBot
    {
        public int difficulty = 1;
        float reaction, cooldown;
        int seenStroke = -1;
        readonly System.Random random;
        public LocalBot(int seed) { random = new System.Random(seed); }
        public void Reset() { seenStroke = -1; reaction = cooldown = 0; }
        public void Step(BotMatch match, MatchActor actor, float dt, out Vector2 move, out bool run)
        {
            move = Vector2.zero; run = false; cooldown = Mathf.Max(0, cooldown - dt);
            if (match.Phase == MatchPhase.PreServe)
            {
                if (!match.BotMissedContact(actor) && match.Rules.Server == actor.playerId && match.ServeElapsed > 1.1f && !actor.Swinging)
                    match.RequestShot(actor.playerId, new ShotIntent(ShotKind.Serve, difficulty == 2, true));
                return;
            }
            if (match.Phase != MatchPhase.Rally || !match.Rules.Live) return;
            bool incoming = match.Rules.ExpectedHitter == actor.playerId;
            if (seenStroke != match.Rules.Stroke) { seenStroke = match.Rules.Stroke; reaction = difficulty == 0 ? .28f : difficulty == 1 ? .15f : .08f; }
            reaction -= dt;
            Vector3 target = new Vector3(0, 0, actor.end * (match.Rules.Stroke<=2 && match.Rules.Server==actor.playerId?6.4f:3.05f));
            if (incoming)
            {
                bool mustBounce = match.Rules.Stroke <= 2;
                // Predict the first playable point following the mandatory bounce.
                bool chooseVolley = !mustBounce && Mathf.Abs(actor.transform.position.z) < 3.7f && match.BallPosition.y > .6f;
                if (match.PredictContact(actor.end, !chooseVolley, out var point, out float delay))
                {
                    ShotKind kind = chooseVolley ? (point.y > 1.7f ? ShotKind.Smash : random.NextDouble() < .23 ? ShotKind.Reset : ShotKind.Volley) : match.Rules.Stroke == 2 ? ShotKind.Drop : Mathf.Abs(point.z) < 3.5f ? ShotKind.Dink : ShotKind.Drive;
                    var intent = new ShotIntent(kind, difficulty == 2 && kind == ShotKind.Drive, random.NextDouble() < .55);
                    bool backhand=kind==ShotKind.Drop;
                    Vector3 profile=actor.ContactOffset(intent,backhand);
                    // Step around the ball to use the lower backhand drop contact.
                    if(backhand && profile.x>-.18f)profile.x=-.22f;
                    if(match.PredictContact(actor.end,!chooseVolley,out var corrected,out var correctedDelay,profile.y)) {point=corrected;delay=correctedDelay;}
                    target = point - actor.transform.TransformDirection(profile); target.y = 0;
                    if (chooseVolley) target.z = actor.end * Mathf.Max(Mathf.Abs(target.z), 2.85f);
                    Vector3 delta = target - actor.transform.position; delta.y = 0;
                    float arrival = delta.magnitude / (actor.runSpeed * (actor.Swinging ? .6f : 1));
                    if (!match.BotMissedContact(actor) && reaction <= 0 && cooldown <= 0 && !actor.Swinging && delay <= intent.ContactDelay + .025f && arrival < intent.ContactDelay + .10f)
                    {
                        if (match.RequestShot(actor.playerId, intent)) cooldown = intent.Duration;
                    }
                }
            }
            Vector3 direction = target - actor.transform.position; direction.y = 0;
            if (direction.magnitude > .08f && reaction <= 0) { direction = Vector3.ClampMagnitude(direction * 3f, 1); move = new Vector2(direction.x, direction.z); run = incoming && Vector3.Distance(target, actor.transform.position) > .55f; }
        }
    }
}
