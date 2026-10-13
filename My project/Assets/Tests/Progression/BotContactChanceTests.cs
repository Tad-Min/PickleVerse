using System;
using NUnit.Framework;
using PickleVerse.Progression;

public sealed class BotContactChanceTests
{
    sealed class CountingRandom : Random
    {
        public int calls;
        readonly double value;
        public CountingRandom(double value) { this.value=value; }
        public override double NextDouble() { calls++; return value; }
    }

    [TestCase(0,25)] [TestCase(100,25)] [TestCase(220,26)]
    [TestCase(9000,99)] [TestCase(9100,100)] [TestCase(100000,100)]
    public void SeededContactDistributionUsesBotElo(int elo,int percent)
    {
        var random=new Random(20261013); var chance=new BotContactChance(); int hits=0;
        for(int stroke=0;stroke<10000;stroke++) if(chance.Resolve(random,elo,stroke)) hits++;
        Assert.That(hits,Is.InRange(percent*100-130,Math.Min(10000,percent*100+130)));
        if(percent==100) Assert.That(hits,Is.EqualTo(10000));
    }

    [TestCase(.259999,true)] [TestCase(.26,false)]
    public void ContactThresholdIsStrict(double value,bool hit)
    { Assert.That(new BotContactChance().Resolve(new CountingRandom(value),220,0),Is.EqualTo(hit)); }

    [TestCase(.1,true)] [TestCase(.8,false)]
    public void SameBallCannotRerollAcrossTicksOrRepeatedSwings(double value,bool hit)
    {
        var random=new CountingRandom(value); var chance=new BotContactChance();
        for(int tick=0;tick<1200;tick++) Assert.That(chance.Resolve(random,220,3),Is.EqualTo(hit));
        Assert.That(random.calls,Is.EqualTo(1)); Assert.That(chance.Missed(3),Is.EqualTo(!hit));
        Assert.That(chance.Missed(4),Is.False);
        chance.Resolve(random,220,4); Assert.That(random.calls,Is.EqualTo(2));
    }

    [Test] public void NewRallyCanResolveReusedStrokeNumber()
    {
        var random=new CountingRandom(.8); var chance=new BotContactChance();
        chance.Resolve(random,220,0); chance.Reset();
        Assert.That(chance.HasDecision(0),Is.False); Assert.That(chance.Missed(0),Is.False);
        chance.Resolve(random,9100,0); Assert.That(random.calls,Is.EqualTo(2)); Assert.That(chance.Missed(0),Is.False);
    }
}
