using PickleVerse.Progression;
using UnityEngine;

namespace PickleVerse.LocalMatch
{
    public sealed class ActorTelemetry : MonoBehaviour
    {
        CoachingSession session;
        MatchActor actor;
        bool prepared;
        ShotTelemetry pending, lastContact, lastVolley;
        int inputStroke = -1, currentStroke, currentRally, currentBounces;
        public static ActorTelemetry Bind(MatchActor actor, CoachingSession session)
        {
            var recorder = actor.GetComponent<ActorTelemetry>() ?? actor.gameObject.AddComponent<ActorTelemetry>();
            if (recorder.session != session) { recorder.pending=null; recorder.lastContact=null; recorder.lastVolley=null; recorder.prepared=false; recorder.inputStroke=-1; }
            recorder.actor = actor; recorder.session = session;
            return recorder;
        }
        public void Input(ShotIntent intent)
        {
            if (prepared) { prepared = false; return; }
            pending = session?.Attempt(intent.kind.ToString(), intent.kind == ShotKind.Serve ? "Serve" : intent.kind.ToString(),
                "client-observed", Time.unscaledTimeAsDouble);
            if (pending != null) { pending.rally = currentRally; pending.bounces = currentBounces; }
            inputStroke = currentStroke;
        }
        public void Trial(string kind, string technique, string referenceId = null)
        {
            pending = session?.Attempt(kind, technique, "training-trial", Time.unscaledTimeAsDouble);
            lastContact = lastVolley = null;
            if (pending != null) pending.referenceId = referenceId;
            prepared = true;
        }
        public void RawInput(ShotInput input, ShotIntent? intent, float? timingError)
        {
            if (pending == null) return;
            pending.rawPressedAt = input.PressedAt;
            pending.rawReleasedAt = input.ReleasedAt;
            pending.rawInputMask = input.ObservedMask;
            pending.rawPosition = Vector(actor.transform.position);
            pending.rawTimingError = timingError;
            pending.rawTechnique = intent?.kind.ToString();
            pending.rawCrossCourt = intent?.cross;
            pending.rawPower = intent?.power;
        }
        public void Contact(Vector3 point, bool volley, bool success)
        {
            var shot = pending;
            if (shot == null) return;
            lastContact = shot;
            if (volley) lastVolley=shot;
            shot.contact = Vector(point); shot.leftFoot = Point(actor.Feet(true)); shot.rightFoot = Point(actor.Feet(false));
            shot.timingError = actor.SwingTime - actor.ContactDelay;
            shot.assisted = true; shot.engineAimed = true;
            shot.volley = volley; shot.kitchen = actor.InKitchen; shot.outcome = success ? "Contact" : "Fault";
            shot.bounces = shot.kind == "Serve" || volley ? 0 : 1;
        }
        public void Outcome(string outcome)
        {
            var shot = outcome == "Momentum" ? lastVolley ?? lastContact ?? pending : outcome == "Out" || outcome == "Net" ? lastContact ?? pending : pending;
            if (shot == null) return;
            shot.outcome = outcome;
            if (outcome == "Kitchen") { shot.volley = true; shot.kitchen = true; }
            if (outcome == "Momentum") { shot.volley = true; shot.momentum = true; }
        }
        public void Recovery(bool recovered)
        {
            if (pending != null) pending.recovered = recovered;
        }
        public void Landing(Vector3 point) { if (lastContact != null) lastContact.landing = Vector(point); }
        public void NetClearance(float height) { if (lastContact != null) lastContact.netClearance = height; }
        public void Context(int stroke, int rally, int bounces)
        {
            currentStroke=stroke; currentRally=rally; currentBounces=bounces;
        }
        public void MissedOpportunity(bool serve, int stroke, int rally)
        {
            if (pending == null || inputStroke != stroke || pending.rally != rally)
                pending=session?.Attempt(serve ? "Serve" : "Unknown",serve ? "Serve" : "Unknown","missed-opportunity",Time.unscaledTimeAsDouble);
            if (pending != null) { pending.rally=rally; pending.outcome=serve ? "LateServe" : "Miss"; }
        }
        public void Checkpoint() { if (session != null) CoachingRuntime.Instance?.Checkpoint(session); }
        static float[] Vector(Vector3 v) => new[] { v.x, v.y, v.z };
        static float[] Point(PickleVerse.Rules.CourtPoint p) => new[] { p.x, p.y, p.z };
    }
}
