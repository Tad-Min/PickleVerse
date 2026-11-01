using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PickleVerse.LocalMatch
{
    public enum AudienceEmotion { Cheering, Pointing, Disappointed, Angry }

    /// <summary>Seated, generic-rig animation. No gameplay physics or global random state.</summary>
    public sealed class AudienceSpectator : MonoBehaviour
    {
        public Transform visual, pelvis;
        public Animator animator;
        public AnimationClip cheering, pointing, disappointed, angry;
        public int supportedTeam;
        public int variation;
        public bool showOnMobile = true;
        public AudienceEmotion Emotion { get; private set; }
        public bool IsAnimating => graph.IsValid();
        public bool Visible { get { foreach (var r in renderers) if (r && r.isVisible) return true; return false; } }

        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] players = new AnimationClipPlayable[2];
        readonly double[] times = new double[2];
        Renderer[] renderers = new Renderer[0];
        int current;
        float fade = 1, delay = -1, speed;
        bool terminal;
        AudienceEmotion pending;
        const float BlendSeconds = .24f;

        public void Initialize()
        {
            DisposeGraph();
            renderers = GetComponentsInChildren<Renderer>(true);
            if (!animator || !visual || !pelvis || !cheering || !pointing || !disappointed || !angry)
                return;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Seated spectator");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            AnimationPlayableOutput.Create(graph, "Audience pose", animator).SetSourcePlayable(mixer);
            speed = .91f + Fraction(variation * 13 + 7) * .18f;
            current = 0; fade = 1; delay = -1; terminal = false; Emotion = AudienceEmotion.Cheering;
            players[0] = Create(cheering);
            times[0] = Fraction(variation * 7 + 17) * cheering.length;
            graph.Connect(players[0], 0, mixer, 0); mixer.SetInputWeight(0, 1);
            graph.Play(); EvaluatePose();
        }

        static float Fraction(int value) => ((uint)(value * 747796405 + 2891336453) % 10007) / 10007f;
        AnimationClipPlayable Create(AnimationClip clip)
        {
            var player = AnimationClipPlayable.Create(graph, clip);
            player.SetApplyFootIK(false); player.SetApplyPlayableIK(false); player.SetSpeed(0);
            return player;
        }
        AnimationClip Clip(AudienceEmotion mood) => mood == AudienceEmotion.Pointing ? pointing :
            mood == AudienceEmotion.Disappointed ? disappointed : mood == AudienceEmotion.Angry ? angry : cheering;

        public void React(int winningTeam, bool pointAwarded, bool matchFinished)
        {
            if (winningTeam < 0 || winningTeam > 1 || (!pointAwarded && !matchFinished)) return;
            pending = supportedTeam == winningTeam ? (pointAwarded || matchFinished ? AudienceEmotion.Pointing : AudienceEmotion.Cheering) :
                (variation % 3 == 0 ? AudienceEmotion.Angry : AudienceEmotion.Disappointed);
            terminal = matchFinished && supportedTeam != winningTeam;
            delay = .08f + Fraction(variation * 19 + 31) * .48f;
        }

        void Change(AudienceEmotion mood)
        {
            int next = 1 - current;
            if (players[next].IsValid())
            {
                graph.Disconnect(mixer, next);
                graph.DestroyPlayable(players[next]);
            }
            players[next] = Create(Clip(mood)); times[next] = 0;
            graph.Connect(players[next], 0, mixer, next);
            mixer.SetInputWeight(next, 0); mixer.SetInputWeight(current, 1);
            current = next; fade = 0; Emotion = mood;
        }

        public void Step(float dt, bool evaluate)
        {
            if (!graph.IsValid()) return;
            if (delay >= 0 && (delay -= dt) <= 0) { delay = -1; Change(pending); }
            for (int i = 0; i < 2; i++) if (players[i].IsValid()) times[i] += dt * speed;
            if (delay < 0 && Emotion != AudienceEmotion.Cheering && !terminal && times[current] >= Clip(Emotion).length - .05f)
                Change(AudienceEmotion.Cheering);
            fade = Mathf.Min(1, fade + dt / BlendSeconds);
            float weight = Mathf.SmoothStep(0, 1, fade);
            mixer.SetInputWeight(current, weight); mixer.SetInputWeight(1 - current, 1 - weight);
            if (fade >= 1 && players[1-current].IsValid())
            {
                graph.Disconnect(mixer, 1-current); graph.DestroyPlayable(players[1-current]);
            }
            if (evaluate) EvaluatePose();
        }

        void EvaluatePose()
        {
            for (int i = 0; i < 2; i++) if (players[i].IsValid())
            {
                double length = players[i].GetAnimationClip().length;
                players[i].SetTime(times[i] % length);
            }
            graph.Evaluate(0);
            // The clips contain pelvis translation. Keep the seated contact on the
            // actual chair instead of allowing a gesture to slide into the aisle.
            visual.position += transform.TransformPoint(new Vector3(0, .095f, 0)) - pelvis.position;
        }
        public void DisposeGraph() { if (graph.IsValid()) graph.Destroy(); }
        void OnDisable() => DisposeGraph();
        void OnDestroy() => DisposeGraph();
    }
}
