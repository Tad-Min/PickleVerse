using NUnit.Framework;
using UnityEngine;
using PickleVerse.LocalMatch;

namespace PickleVerse.Tests
{
    public sealed class PracticeMovementTests
    {
        [TestCase(-1)][TestCase(1)]
        public void HoldingIntoNetStopsBodyAndVelocity(int end)
        {
            var area=MatchMovement.PracticeBounds(end);Vector3 p=new Vector3(0,0,end*2),v=Vector3.zero;
            for(int i=0;i<400;i++)MatchMovement.Step(ref p,ref v,new Vector2(0,-end),true,false,end,1f/60,bounds:area);
            Assert.That(p.z,Is.EqualTo(end*.85f).Within(.0001));
            Assert.That(v.sqrMagnitude,Is.LessThan(.00001));
        }
        [TestCase(.2f)][TestCase(.05f)][TestCase(.01666667f)][TestCase(.00694444f)]
        public void DiagonalInputSlidesAlongBoundaryWithoutOutwardVelocity(float dt)
        {
            var area=MatchMovement.PracticeBounds(-1);Vector3 p=new Vector3(area.xMax,0,-4),v=Vector3.zero;
            MatchMovement.Step(ref p,ref v,Vector2.one.normalized,true,false,-1,dt,bounds:area);
            Assert.That(p.x,Is.EqualTo(area.xMax).Within(.0001));
            Assert.That(v.x,Is.Zero.Within(.0001));Assert.That(v.z,Is.GreaterThan(0));
        }
        [Test] public void ReversingAwayFromNetImmediatelyRestoresMovement()
        {
            var p=new Vector3(0,0,-.85f);var v=Vector3.zero;var area=MatchMovement.PracticeBounds(-1);
            MatchMovement.Step(ref p,ref v,Vector2.up,true,false,-1,1f/60,bounds:area);
            MatchMovement.Step(ref p,ref v,Vector2.down,true,false,-1,1f/60,bounds:area);
            Assert.That(v.z,Is.LessThan(0));Assert.That(p.z,Is.LessThan(-.85f));
        }
        [Test] public void ReleasingRunDeceleratesAndSettles()
        {
            var p=new Vector3(0,0,-4);var v=Vector3.right*5.8f;
            for(int i=0;i<30;i++)MatchMovement.Step(ref p,ref v,Vector2.zero,false,false,-1,1f/60,bounds:MatchMovement.PracticeBounds(-1));
            Assert.That(v.sqrMagnitude,Is.LessThan(.00001));
        }
        [Test] public void MatchModesKeepExistingNetFaultBoundary()
        {
            var p=new Vector3(0,0,-1);var v=Vector3.zero;
            for(int i=0;i<120;i++)MatchMovement.Step(ref p,ref v,Vector2.up,true,false,-1,1f/60);
            Assert.That(p.z,Is.EqualTo(-.30f).Within(.0001));
        }
    }
}
