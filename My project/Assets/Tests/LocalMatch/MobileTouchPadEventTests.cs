using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PickleVerse.LocalMatch.Tests
{
    public sealed class MobileTouchPadEventTests
    {
        sealed class Receiver : IMatchTouchShotReceiver
        {
            public int TouchShotMask {get;set;}
            public Vector2 TouchMovement {get;set;}
            public bool TouchRun {get;set;}
            public bool TouchShotsEnabled {get;set;}=true;
            public bool TouchServing {get;set;}
            public int count,mask;
            public void CommitTouchShot(int value){mask=value;count++;}
        }
        GameObject canvasObject,eventObject;
        Receiver receiver;
        MobileTouchPad pad;
        [SetUp] public void Create()
        {
            eventObject=new GameObject("Touch test event system",typeof(EventSystem));
            canvasObject=new GameObject("Touch test canvas",typeof(Canvas),typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var root=new GameObject("TouchControls",typeof(RectTransform));root.transform.SetParent(canvasObject.transform,false);
            var r=(RectTransform)root.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
            foreach(int mask in new[]{0,1,2,4,3,5,6,8,16})
            {
                var go=new GameObject("Control "+mask,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(root.transform,false);
                var c=go.AddComponent<TouchControl>();c.joystick=mask==0;c.shotMask=mask;c.modifier=mask>=8;
            }
            receiver=new Receiver();pad=MobileTouchPad.Install(root,receiver);
            Canvas.ForceUpdateCanvases();pad.Layout();
        }
        [TearDown] public void Dispose(){Object.DestroyImmediate(canvasObject);Object.DestroyImmediate(eventObject);}
        PointerEventData Event(TouchControl c,int pointer,Vector2 delta=default)
        {return new PointerEventData(eventObject.GetComponent<EventSystem>()){pointerId=pointer,position=RectTransformUtility.WorldToScreenPoint(null,c.transform.position)+delta*pad.Unit};}
        [Test] public void QuickTapIsSynchronousAndCannotBeDispatchedTwice()
        {
            var e=Event(pad.Drive,1);pad.Drive.OnPointerDown(e);
            Assert.AreEqual(0,receiver.count);Assert.AreEqual(0,receiver.TouchShotMask);
            pad.Drive.OnPointerUp(e);Assert.AreEqual(1,receiver.count);Assert.AreEqual(1,receiver.mask);
            pad.Drive.OnEndDrag(e);pad.Drive.OnPointerUp(e);Assert.AreEqual(1,receiver.count);
        }
        [Test] public void JoystickAndShotUseIndependentFingers()
        {
            var shot=Event(pad.Dink,1);pad.Dink.OnPointerDown(shot);
            var movement=Event(pad.Joystick,2,new Vector2(40,0));pad.Joystick.OnPointerDown(movement);
            Assert.Greater(receiver.TouchMovement.x,0);
            var foreign=Event(pad.Volley,3);pad.Volley.OnPointerDown(foreign);pad.Volley.OnPointerUp(foreign);
            Assert.AreEqual(0,receiver.count);Assert.IsTrue(pad.Gesture.Active);
            shot.position+=Vector2.up*65*pad.Unit;pad.Dink.OnDrag(shot);pad.Dink.OnPointerUp(shot);
            Assert.AreEqual(3,receiver.mask);Assert.Greater(receiver.TouchMovement.x,0);
            pad.Joystick.OnPointerUp(movement);Assert.AreEqual(Vector2.zero,receiver.TouchMovement);
        }
        [Test] public void DragCancelAndDisableCannotFireStaleShot()
        {
            var e=Event(pad.Drive,1);pad.Drive.OnPointerDown(e);e.position+=Vector2.down*70*pad.Unit;
            pad.Drive.OnDrag(e);pad.Drive.OnPointerUp(e);Assert.AreEqual(0,receiver.count);
            e=Event(pad.Drive,1);pad.Drive.OnPointerDown(e);pad.CancelAll();pad.Drive.OnPointerUp(e);Assert.AreEqual(0,receiver.count);
            pad.Drive.OnPointerDown(e);receiver.TouchShotsEnabled=false;pad.Drive.OnPointerUp(e);Assert.AreEqual(0,receiver.count);
        }
        [Test] public void OnlyFourControlsRemainAndJoystickIsRecessed()
        {
            int count=0;foreach(var c in pad.GetComponentsInChildren<TouchControl>(true))if(c.gameObject.activeSelf)count++;
            Assert.AreEqual(4,count);Assert.Less(pad.Joystick.GetComponent<CanvasGroup>().alpha,.5f);
        }
        [Test] public void ReleasePositionIsUsedEvenWithoutLastDragCallback()
        {
            var e=Event(pad.Volley,1);pad.Volley.OnPointerDown(e);
            e.position+=Vector2.left*60*pad.Unit;pad.Volley.OnPointerUp(e);
            Assert.AreEqual(20,receiver.mask);
        }
        [Test] public void WaveHasRendererAndVisibleGeometry()
        {
            var e=Event(pad.Drive,1);pad.Drive.OnPointerDown(e);e.position+=Vector2.down*70*pad.Unit;pad.Drive.OnDrag(e);
            Canvas.ForceUpdateCanvases();var wave=pad.GetComponentInChildren<TouchWaveGraphic>();
            Assert.IsNotNull(wave.GetComponent<CanvasRenderer>());
            Assert.Greater(wave.canvasRenderer.GetMesh().vertexCount,100);
        }
        [Test] public void RoundTargetsAcceptCentersAndIgnoreTransparentCorners()
        {
            foreach(var c in new[]{pad.Joystick,pad.Drive,pad.Dink,pad.Volley})
            {
                var e=Event(c,1);
                Assert.IsTrue(c.GetComponent<UnityEngine.UI.Image>().Raycast(e.position,null));
                float half=((RectTransform)c.transform).rect.width*.5f;
                Vector2 corner=RectTransformUtility.WorldToScreenPoint(null,c.transform.TransformPoint(new Vector3(half,half,0)));
                Assert.IsFalse(c.IsRaycastLocationValid(corner,null));
            }
            var down=Event(pad.Drive,1);pad.Drive.OnPointerDown(down);down.position+=Vector2.up*60*pad.Unit;pad.Drive.OnDrag(down);
            Assert.IsFalse(pad.GetComponentInChildren<TouchWaveGraphic>().raycastTarget);
        }
        [Test] public void DragStartsWithoutInputModulesDefaultPixelDelay()
        {
            var e=Event(pad.Drive,1);e.useDragThreshold=true;pad.Drive.OnInitializePotentialDrag(e);Assert.IsFalse(e.useDragThreshold);
        }
    }
}
