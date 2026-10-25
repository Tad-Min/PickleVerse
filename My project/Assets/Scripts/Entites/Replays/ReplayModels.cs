using System;
using System.Collections.Generic;
namespace PickleVerse.Replays
{
    [Serializable]
    public sealed class ReplayActorInfo
    {
        public string id, name, visualId, paddlePath, paddleId, skinName;
        public int team;
        public string[] bones;
    }
    [Serializable]
    public sealed class ReplayMetadata
    {
        public int version = 1, sampleRate = 30;
        public string matchId, playerId;
        public bool complete;
        public double duration;
        public long startedUtcTicks;
        public bool night;
        public List<ReplayActorInfo> actors = new List<ReplayActorInfo>();
        public List<ReplayChunkInfo> chunks = new List<ReplayChunkInfo>();
    }
    [Serializable]
    public sealed class ReplayChunkInfo { public string file; public double start, end; }
    [Serializable]
    public sealed class ReplayActorPose
    {
        public float[] root, bones;
        public bool paddleVisible;
    }
    [Serializable]
    public sealed class ReplayFrame
    {
        public double time;
        public float[] ball;
        public float[] ballRotation, ballScale;
        public ReplayActorPose[] actors;
        public int scoreA, scoreB, gamesA, gamesB;
        public string status;
        public bool discontinuity;
    }
    [Serializable]
    public sealed class ReplayEvent { public double time; public string kind, detail; }
    [Serializable]
    public sealed class ReplayChunk
    {
        public List<ReplayFrame> frames = new List<ReplayFrame>();
        public List<ReplayEvent> events = new List<ReplayEvent>();
    }
}
