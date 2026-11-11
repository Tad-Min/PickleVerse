using UnityEngine;

namespace PickleVerse.LocalMatch
{
    /// <summary>
    /// Applies the character chosen in the menu (male or female) to the local player's actor.
    /// Local scenes contain one female and one male body; when the choice differs from the
    /// authored body, the two actors exchange bodies (mesh, bones, paddle and clips) before
    /// they are initialised. Roles, court ends and rule indexes stay unchanged.
    /// </summary>
    public static class ActorCharacter
    {
        public static bool LocalPlayerIsMale
        {
            get
            {
                var profile = PickleVerse.Services.AccountRuntime.Ensure().Account?.Profile;
                if (profile != null)
                    return profile.IsMale;
                var runtime = PickleVerse.Progression.ProgressionRuntime.Instance;
                return runtime != null && runtime.Snapshot != null && runtime.Snapshot.isMale;
            }
        }

        /// <summary>Gives <paramref name="player"/> the chosen body, taking it from the first actor that has it.</summary>
        public static void ApplyLocalChoice(MatchActor player, params MatchActor[] others)
        {
            if (!player || !player.visual)
                return;
            bool female = !LocalPlayerIsMale;
            if (player.IsFemale == female)
                return;
            foreach (var other in others)
                if (other && other != player && other.visual && other.IsFemale == female)
                {
                    ExchangeBodies(player, other);
                    return;
                }
        }

        /// <summary>Swaps everything that belongs to the body. Must run before MatchActor.Initialize.</summary>
        public static void ExchangeBodies(MatchActor a, MatchActor b)
        {
            bool aFemale = a.IsFemale, bFemale = b.IsFemale;
            Transform aRoot = a.transform, bRoot = b.transform;
            Move(a.visual, aRoot, bRoot);
            Move(b.visual, bRoot, aRoot);
            if (a.paddle && !a.paddle.transform.IsChildOf(a.visual))
                Move(a.paddle.transform, aRoot, bRoot);
            if (b.paddle && !b.paddle.transform.IsChildOf(b.visual))
                Move(b.paddle.transform, bRoot, aRoot);
            (a.visual, b.visual) = (b.visual, a.visual);
            (a.paddle, b.paddle) = (b.paddle, a.paddle);
            (a.paddleFace, b.paddleFace) = (b.paddleFace, a.paddleFace);
            (a.leftFoot, b.leftFoot) = (b.leftFoot, a.leftFoot);
            (a.rightFoot, b.rightFoot) = (b.rightFoot, a.rightFoot);
            (a.upperArm, b.upperArm) = (b.upperArm, a.upperArm);
            (a.forearm, b.forearm) = (b.forearm, a.forearm);
            (a.hand, b.hand) = (b.hand, a.hand);
            (a.leftHand, b.leftHand) = (b.leftHand, a.leftHand);
            (a.heldBallOffset, b.heldBallOffset) = (b.heldBallOffset, a.heldBallOffset);
            (a.clips, b.clips) = (b.clips, a.clips);
            a.IsFemale = bFemale;
            b.IsFemale = aFemale;
        }

        static void Move(Transform part, Transform from, Transform to)
        {
            if (!part || !part.IsChildOf(from))
                return;
            // Keep the authored offset of the body relative to its actor root.
            Vector3 position = part.localPosition;
            Quaternion rotation = part.localRotation;
            Vector3 scale = part.localScale;
            part.SetParent(to, false);
            part.localPosition = position;
            part.localRotation = rotation;
            part.localScale = scale;
        }
    }
}
