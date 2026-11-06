using NUnit.Framework;
using PickleVerse.FrontEnd;
using PickleVerse.Settings;
using UnityEngine;

namespace PickleVerse.Tests.FrontEnd
{
    public sealed class PlayerProgressionMenuLifecycleTests
    {
        GameObject host;
        PlayerProgressionMenu menu;

        [SetUp]
        public void SetUp()
        {
            // Match BeforeSceneLoad: serialized bindings exist, but menu Awake has not run.
            host=new GameObject("Menu awaiting Awake");
            host.SetActive(false);
            menu=host.AddComponent<PlayerProgressionMenu>();
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(host); }

        [Test]
        public void AccountGateCanCloseBeforeAnyModalExists()
        {
            Assert.DoesNotThrow(menu.Close);
            Assert.That(menu.IsOpen,Is.False);
        }

        [Test]
        public void AccountGateCanCloseAuthoredModalBeforePresentationIsAttached()
        {
            menu.modal=new GameObject("ProgressionModal",typeof(RectTransform)).GetComponent<RectTransform>();
            menu.modal.SetParent(host.transform,false);

            Assert.DoesNotThrow(menu.Close);
            Assert.That(menu.modal.gameObject.activeSelf,Is.False);
            Assert.That(menu.IsOpen,Is.False);
            Assert.DoesNotThrow(menu.Close);
        }

        [Test]
        public void ClosingInitializedModalDisablesItsRenderingAndInput()
        {
            menu.modal=new GameObject("ProgressionModal",typeof(RectTransform)).GetComponent<RectTransform>();
            menu.modal.SetParent(host.transform,false);
            UiModalPresentation.Attach(menu.modal,false).Show();

            menu.Close();

            Assert.That(menu.modal.GetComponent<Canvas>().enabled,Is.False);
            Assert.That(menu.modal.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
            Assert.That(menu.modal.GetComponent<CanvasGroup>().interactable,Is.False);
        }

        [Test]
        public void OpaqueModalKeepsAClearOnlyCameraAndRestoresTheSceneCamera()
        {
            var prior=Camera.main;
            bool wasEnabled=prior&&prior.enabled;
            if(prior)prior.enabled=false;
            var cameraObject=new GameObject("Scene Camera",typeof(Camera));
            cameraObject.tag="MainCamera";
            var camera=cameraObject.GetComponent<Camera>();
            var modalObject=new GameObject("Opaque Modal",typeof(RectTransform));
            try
            {
                var presentation=UiModalPresentation.Attach(modalObject.GetComponent<RectTransform>(),true);
                presentation.Show();
                var backdrop=modalObject.GetComponentInChildren<Camera>();
                Assert.That(camera.enabled,Is.False);
                Assert.That(backdrop,Is.Not.Null);
                Assert.That(backdrop.enabled,Is.True);
                Assert.That(backdrop.cullingMask,Is.Zero);

                presentation.Hide();
                Assert.That(camera.enabled,Is.True);
                Assert.That(backdrop.enabled,Is.False);
                presentation.Show();
                Assert.That(modalObject.GetComponentsInChildren<Camera>().Length,Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(modalObject);
                Object.DestroyImmediate(cameraObject);
                if(prior)prior.enabled=wasEnabled;
            }
        }
    }
}
