using System;
using System.Collections.Generic;
using System.Linq;

namespace PickleVerse.Progression
{
    public enum TrainingSkill { Serve, Drive, Dink, Volley, Modifiers, Drop, Reset, Smash, Footwork, Approach, DoublesCover, DoublesTransition }
    public enum TrainingError { WrongInput, Early, Late, Kitchen, Momentum, Net, Out, Target, ServeFeet, Movement, Spacing }
    [Serializable] public sealed class TrainingDrill
    {
        public string id, title, explanation, coaching;
        public TrainingSkill skill;
        public int tier, trials, required, reward, xp;
        public int[] masks;
        public string[] prerequisites;
        public bool Doubles => skill == TrainingSkill.DoublesCover || skill == TrainingSkill.DoublesTransition;
        public string TierName => new[]{"Cơ bản","Kiểm soát","Nâng cao","Đánh đôi"}[tier];
        public int Mask(int trial) => masks[trial % masks.Length];
    }
    [Serializable] public sealed class TrainingErrorCount {public TrainingError error;public int count;}
    [Serializable] public sealed class TrainingAttempt
    {
        public string id,lessonId,playerId;
        public int trials,successes,contacts;
        public double seconds,timingTotal;
        public bool completed,passed,review;
        public int stars,rewardTokenpad,rewardXp;
        public List<TrainingErrorCount> errors=new List<TrainingErrorCount>();
        public double Accuracy=>trials==0?0:(double)successes/trials;
        public double Timing=>contacts==0?1:timingTotal/contacts;
        public int Errors=>errors.Sum(e=>e.count);
        public void Error(TrainingError error){var row=errors.Find(e=>e.error==error);if(row==null){row=new TrainingErrorCount{error=error};errors.Add(row);}row.count++;}
    }
    [Serializable] public sealed class TrainingLessonRecord
    {
        public string id;
        public int attempts,retries,completions,bestStars,trials,successes;
        public double seconds,bestAccuracy;
        public List<TrainingErrorCount> errors=new List<TrainingErrorCount>();
        public List<float> accuracyHistory=new List<float>();
    }
    [Serializable] public sealed class TrainingProfile
    {
        public int version=1;
        public string lastLesson="serve";
        public TrainingAttempt active;
        public List<TrainingLessonRecord> lessons=new List<TrainingLessonRecord>();
        public List<string> completedAttempts=new List<string>();
    }
}
