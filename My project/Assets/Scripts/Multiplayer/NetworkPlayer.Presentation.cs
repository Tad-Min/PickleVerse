using System;
using System.Collections.Generic;
using PickleVerse.LocalMatch;
using UnityEngine;

namespace PickleVerse.Multiplayer
{
    public sealed partial class NetworkPlayer
    {
        readonly Queue<(double time, Action<double> present)> poseEvents = new();
        Vector3 visualCorrection;
        public Vector3 PresentedPosition => Actor ? Actor.transform.position : transform.position;

        void RenderClientPose()
        {
            if (!Actor || IsServer) return;
            var match = NetworkMatch.Instance;
            if (MultiplayerCourtUI.Instance && MultiplayerCourtUI.Instance.cinematic && MultiplayerCourtUI.Instance.cinematic.OwnsPose) return;
            bool owner = IsOwner;
            var frame = Frame;
            if (owner)
            {
                visualCorrection *= Mathf.Exp(-Time.unscaledDeltaTime / .1f);
                if (visualCorrection.sqrMagnitude < .000001f) visualCorrection = Vector3.zero;
                Actor.transform.localPosition = transform.InverseTransformVector(visualCorrection);
            }
            else
            {
                transform.position = frame.position;
                if (TryGetSnapshotsBuffers(out var fromBuffer, out var toBuffer, out float alpha))
                {
                    var (from, to) = GetPropertyReader<PlayerFrame>(nameof(Frame)).Read(fromBuffer, toBuffer);
                    frame = to;
                    if (from.motionEpoch == to.motionEpoch)
                    {
                        frame = alpha < 1f ? from : to;
                        frame.position = Vector3.Lerp(from.position, to.position, alpha);
                        frame.velocity = Vector3.Lerp(from.velocity, to.velocity, alpha);
                    }
                }
                Actor.transform.localPosition = transform.InverseTransformVector(frame.position - transform.position);
            }

            if (!match || match.Paused) return;
            Actor.RenderNetworkPose(owner ? predictedVelocity : frame.velocity, owner ? LocalRun() : frame.running,
                Time.deltaTime, CanMove(), (IsLocalHuman ? LocalAxes() : frame.movement) * -CourtEnd);
            // Swing and contact are authoritative events, including for the local player's predicted root.
            // Present them on the same timeline as the interpolated ball.
            PresentPoseEvents(Runner.RemoteRenderTime);
        }

        void PresentPoseEvents(double renderTime)
        {
            while (poseEvents.Count > 0 && poseEvents.Peek().time <= renderTime)
                poseEvents.Dequeue().present(renderTime);
        }

        void ScheduleSwing(int kind, bool attack, bool crossCourt, Vector3 predictedBall, float delay, double started)
        {
            var intent = BotMatchGameplayService.ToLocal(new ShotIntent { kind = (ShotKind)kind, attack = attack, crossCourt = crossCourt });
            poseEvents.Enqueue((started, renderTime =>
            {
                Actor.RenderNetworkSwing(intent, predictedBall, delay, (float)(renderTime - started));
                SetActiveShotKeys();
            }));
        }

        void ScheduleContact(bool volley, Vector3 position, bool successful, Vector3 velocity, double time, byte grade)
        {
            poseEvents.Enqueue((time, _ =>
            {
                Actor.ConsumeContact(volley, position, successful, (HitGrade)grade, preserveSwingTiming: true);
                if (successful)
                    NetworkMatch.Instance?.PresentContact(position, velocity, time);
            }));
        }
    }
}
