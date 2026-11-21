using NUnit.Framework;
using UnityEngine;

namespace PickleVerse.LocalMatch.Tests
{
    public sealed class TouchControlsTests
    {
        [TestCase(1,0,0,1)] [TestCase(2,0,0,2)] [TestCase(4,0,0,4)]
        [TestCase(1,0,60,5)] [TestCase(2,0,60,3)] [TestCase(4,0,60,6)]
        [TestCase(1,-60,0,17)] [TestCase(2,-60,0,18)] [TestCase(4,-60,0,20)]
        [TestCase(1,60,0,9)] [TestCase(2,60,0,10)] [TestCase(4,60,0,12)]
        public void ReleaseProducesOneValidGameplayShot(int basis,float x,float y,int expected)
        {
            var touch=new TouchShotGesture();var input=new ShotInput();
            Assert.IsTrue(touch.Begin(7,basis));touch.Move(7,new Vector2(x,y));
            Assert.AreEqual(expected,touch.Mask);
            Assert.IsTrue(touch.Release(7,out int mask));Assert.AreEqual(expected,mask);
            Assert.IsFalse(input.Tick(mask,false,false,out _));
            Assert.IsTrue(input.Tick(mask&24,false,false,out _));
            Assert.AreEqual(expected,input.CommittedMask);
            Assert.IsFalse(touch.Release(7,out _));
        }
        [Test]
        public void ChangingDirectionDoesNotReleaseOrAccumulateComboBits()
        {
            var t=new TouchShotGesture();t.Begin(1,2);
            t.Move(1,Vector2.up*70);Assert.AreEqual(3,t.Mask);
            t.Move(1,Vector2.left*70);Assert.AreEqual(18,t.Mask);
            t.Move(1,Vector2.right*70);Assert.AreEqual(10,t.Mask);
            t.Move(1,Vector2.zero);Assert.AreEqual(2,t.Mask);
            Assert.IsTrue(t.Active);Assert.IsTrue(t.Release(1,out int mask));Assert.AreEqual(2,mask);
        }
        [Test]
        public void CancelAndForeignPointerCannotFireShot()
        {
            var t=new TouchShotGesture();t.Begin(3,1);
            Assert.IsFalse(t.Begin(4,2));t.Move(4,Vector2.up*70);Assert.AreEqual(1,t.Mask);
            Assert.IsFalse(t.Release(4,out _));Assert.IsTrue(t.Active);
            t.Move(3,Vector2.down*70);Assert.IsFalse(t.Release(3,out _));
            t.Begin(3,1);t.Cancel();Assert.IsFalse(t.Release(3,out _));
        }
        [Test]
        public void HysteresisStopsThumbJitterAndAllowsReturnToCenter()
        {
            var t=new TouchShotGesture();t.Begin(1,1);
            t.Move(1,Vector2.up*29);Assert.AreEqual(TouchShotDirection.Center,t.Direction);
            t.Move(1,Vector2.up*32);Assert.AreEqual(TouchShotDirection.Up,t.Direction);
            t.Move(1,Vector2.up*25);Assert.AreEqual(TouchShotDirection.Up,t.Direction);
            t.Move(1,Vector2.up*21);Assert.AreEqual(TouchShotDirection.Center,t.Direction);
        }
        [TestCase(0,0,1)] [TestCase(0,70,1)] [TestCase(-70,0,17)] [TestCase(70,0,9)]
        public void ServeGesturePreservesLegalServe(float x,float y,int expected)
        {
            var t=new TouchShotGesture();t.Begin(1,1);t.Move(1,new Vector2(x,y),true);
            Assert.IsTrue(t.Release(1,out int mask));Assert.AreEqual(expected,mask);
            var input=new ShotInput();input.Tick(mask,false,true,out _);
            Assert.IsTrue(input.Tick(mask&24,false,true,out var shot));Assert.AreEqual(ShotKind.Serve,shot.kind);
        }
        [TestCase(667,375)] [TestCase(844,390)] [TestCase(915,412)] [TestCase(1280,720)]
        [TestCase(1920,1080)] [TestCase(2400,1080)] [TestCase(1600,720)] [TestCase(1024,768)] [TestCase(390,844)]
        public void WholeWaveFitsSafeAreaAtEveryScaleAndHandedness(float width,float height)
        {
            var safe=new Rect(34,21,width-68,height-42);
            foreach(float scale in new[]{.75f,1f,1.4f})foreach(bool left in new[]{false,true})
            {
                var l=MobileTouchLayout.Calculate(safe,scale,left);float r=99*l.unit;
                foreach(var p in new[]{l.drive,l.dink,l.volley})
                {
                    Assert.GreaterOrEqual(p.x-r,safe.xMin);Assert.LessOrEqual(p.x+r,safe.xMax);
                    Assert.GreaterOrEqual(p.y-r,safe.yMin);Assert.LessOrEqual(p.y+r,safe.yMax);
                }
                Assert.Greater(Vector2.Distance(l.joystick,l.dink),165*l.unit);
                Assert.Greater(Vector2.Distance(l.drive,l.volley),90*l.unit);
                Assert.Greater(Vector2.Distance(l.run,l.drive),110*l.unit);
                Assert.GreaterOrEqual(l.run.y-56*l.unit,safe.yMin);
                Assert.LessOrEqual(l.run.x+56*l.unit,safe.xMax);
            }
        }
        [TestCase(2400,1080)][TestCase(1600,720)][TestCase(2960,1440)][TestCase(1280,720)]
        public void Mobile3DUsesAtMost720pWhileNativeScreenRemainsUnchanged(int width,int height)
        {
            float scale=PickleVerse.Settings.MobileRenderPolicy.ScaleCeiling(width,height);
            Assert.LessOrEqual(width*scale,1280.01f);Assert.LessOrEqual(height*scale,720.01f);
        }
    }
}
