using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PaddleTrackingValidation
{
    public static string Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run in Play Mode");
        var scene = SceneManager.CreateScene("PaddleTrackingValidation", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        try
        {
            Vector3 wrist = Vector3.zero, index = new Vector3(.2f, -.2f, 0),
                middle = new Vector3(0, -.3f, 0), pinky = new Vector3(-.2f, -.2f, 0);
            Require(HandTrackingGeometry.TryRotation(wrist, index, middle, pinky, false, out var horizontal), "horizontal grip basis");
            Require(Vector3.Dot(horizontal * Vector3.forward, Vector3.right) > .999f, "head points across knuckles, not along fingers");
            Require(HandTrackingGeometry.TryRotation(wrist, index, middle, pinky, true, out var mirrored), "mirror basis");
            Require(Vector3.Dot(mirrored * Vector3.forward, Vector3.left) > .999f, "mirror head direction");
            Require(Mathf.Abs(Vector3.Dot(mirrored * Vector3.forward, mirrored * Vector3.up)) < .001f, "orthogonal basis");
            var turn = Quaternion.Euler(0, 0, 90);
            Require(HandTrackingGeometry.TryRotation(wrist, turn * index, turn * middle, turn * pinky, false, out var vertical), "vertical grip basis");
            Require(Vector3.Dot(vertical * Vector3.forward, Vector3.down) > .999f, "vertical head direction");
            Require(!HandTrackingGeometry.TryRotation(wrist, wrist, wrist, wrist, false, out _), "reject degenerate landmarks");
            Require(!HandTrackingGeometry.TryRotation(wrist, new Vector3(float.NaN, 0, 0), middle, pinky, false, out _), "reject NaN");
            Rect crop = HandTrackingGeometry.Crop(new Vector2(.99f, .5f), new Vector2(.8f, .5f), 640, 480);
            Require(Mathf.Abs(crop.width - crop.height) < .001f && crop.xMax > 640, "square pixel crop retains edge padding");

            var go = new GameObject("PaddleTrackingTest");
            SceneManager.MoveGameObjectToScene(go, scene);
            var paddle = go.AddComponent<PaddleKinematicController>();
            paddle.enabled = false;
            var body = go.GetComponent<Rigidbody>();
            var physics = scene.GetPhysicsScene();
            var frame = new PoseFrame { LocalJoints = new Vector3[17], WristTracked = true,
                WristTimestamp = Time.time, HandTimestamp = Time.time };
            frame.LocalJoints[10] = new Vector3(.5f, 1, 0);
            for (int i = 0; i < 100; i++) Step(paddle, frame, physics);
            Require(Vector3.Distance(paddle.GripWorldPosition, frame.RightWristWorldPosition) < .002f, "wrist follows without hand orientation");
            Require(Quaternion.Angle(body.rotation, Quaternion.identity) < .1f, "initial rotation preserved");
            frame.HandTracked = true;
            frame.HandForward = horizontal * Vector3.forward;
            frame.HandUp = horizontal * Vector3.up;
            for (int i = 0; i < 100; i++) Step(paddle, frame, physics);
            Require(Vector3.Distance(paddle.GripWorldPosition, frame.RightWristWorldPosition) < .002f, "grip stays on wrist while rotating");
            var heldRotation = body.rotation;
            frame.HandTracked = false;
            frame.LocalJoints[10] += Vector3.right;
            for (int i = 0; i < 100; i++) Step(paddle, frame, physics);
            Require(Vector3.Distance(paddle.GripWorldPosition, frame.RightWristWorldPosition) < .002f, "lost hand still follows wrist");
            Require(Quaternion.Angle(body.rotation, heldRotation) < .1f, "lost hand holds rotation");
            var heldPosition = body.position;
            frame.WristTracked = false;
            frame.LocalJoints[10] += Vector3.one * 100;
            Step(paddle, frame, physics);
            Require(Vector3.Distance(body.position, heldPosition) < .002f, "lost wrist cannot consume invalid position");
            frame.WristTracked = true;
            frame.WristTimestamp = Time.time - 1;
            Step(paddle, frame, physics);
            Require(Vector3.Distance(body.position, heldPosition) < .002f, "stale wrist rejected");
            frame.WristTimestamp = Time.time;
            Step(paddle, frame, physics);
            Require(Vector3.Distance(body.position, heldPosition) <= 14 * Time.fixedDeltaTime + .001f, "reacquisition speed capped");
            Require(paddle.LinearVelocity.magnitude <= 14.01f, "reported velocity capped");
            return "PASS: horizontal/vertical/mirror axes, degenerate/NaN rejection, aspect/edge crop, initial rotation, grip anchoring, hand loss, wrist loss, stale sample, bounded reacquisition";
        }
        finally
        {
            foreach (var go in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(go);
            SceneManager.UnloadSceneAsync(scene);
        }
    }

    private static void Step(PaddleKinematicController paddle, PoseFrame frame, PhysicsScene physics)
    {
        paddle.SetTrackingTarget(frame, Time.time);
        paddle.SendMessage("FixedUpdate");
        physics.Simulate(Time.fixedDeltaTime);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Paddle validation failed: " + message);
    }
}
