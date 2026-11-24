using System;
using UnityEngine;

namespace PickleVerse.LocalMatch
{
    /// <summary>
    /// Presentation-only hooks for cosmetic ball effects (paddle-skin trails, Assets/VFX). Match modes register the ball
    /// they render; <see cref="MatchActor"/> reports successful contacts. Nothing here feeds back into rules, physics,
    /// networking or replays.
    /// </summary>
    public static class BallPresentationRegistry
    {
        /// <summary>A rendered match ball and its trail (the trail may be null).</summary>
        public static event Action<Transform, TrailRenderer> BallRegistered;
        /// <summary>An actor's paddle made a successful contact at a world position.</summary>
        public static event Action<MatchActor, Vector3> SuccessfulContact;

        public static void Register(Transform ball, TrailRenderer trail)
        {
            if (ball)
                BallRegistered?.Invoke(ball, trail);
        }

        internal static void ReportContact(MatchActor actor, Vector3 point)
        {
            try { SuccessfulContact?.Invoke(actor, point); }
            catch (Exception error) { Debug.LogException(error); }   // a cosmetic listener must never break a rally
        }
    }
}
