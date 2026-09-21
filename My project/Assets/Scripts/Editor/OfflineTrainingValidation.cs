using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OfflineTrainingValidation
{
    // Local physics scene: no edits or simulation steps are applied to the user's court.
    public static string Run()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
        var scene = SceneManager.CreateScene("TrainingValidation", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        try
        {
            var calibrationObject = new GameObject("CalibrationTest");
            SceneManager.MoveGameObjectToScene(calibrationObject, scene);
            var calibration = calibrationObject.AddComponent<CalibrationProfile>();
            calibration.BeginCalibration();
            for (int i = 0; i < 21; i++) calibration.ProcessSample(Vector2.zero, .2f, .1f);
            Require(calibration.IsCalibrated, "Calibration did not complete");
            Vector3 origin = calibration.MapRootPosition(Vector2.zero, .2f);
            Require(calibration.MapRootPosition(new Vector2(.1f, .1f), .2f).x > origin.x, "Horizontal mapping");
            float sideline = PickleballCourtSpec.HalfCourtWidth - .2f;
            Require(Mathf.Abs(calibration.MapRootPosition(new Vector2(-.4f, 0), .2f).x + sideline) < .001f, "Webcam left edge maps to left sideline");
            Require(Mathf.Abs(calibration.MapRootPosition(new Vector2(.4f, 0), .2f).x - sideline) < .001f, "Webcam right edge maps to right sideline");
            Require(Mathf.Abs(calibration.MapRootPosition(new Vector2(.5f, 0), .2f).x - sideline) < .001f, "Horizontal movement clamps at sideline");
            Vector3 nearCamera = calibration.MapRootPosition(Vector2.zero, .4f);
            Vector3 farCamera = calibration.MapRootPosition(Vector2.zero, .1f);
            Require(nearCamera.z > origin.z && Mathf.Abs(nearCamera.z) < Mathf.Abs(origin.z), "Near camera moves toward net at Z=0");
            Require(farCamera.z < origin.z && Mathf.Abs(farCamera.z) > Mathf.Abs(origin.z), "Far camera moves away from net");
            Require(Mathf.Abs(calibration.MapRootPosition(Vector2.zero, .2f).z - origin.z) < .001f, "Reference distance preserves root");
            Require(Mathf.Abs(calibration.MapRootPosition(Vector2.zero, .001f).z - origin.z) <= 2.001f, "Depth remains bounded");
            calibration.BeginCalibration();
            Require(!calibration.IsCalibrated, "Recalibration reset");
            for (int i = 0; i < 21; i++) calibration.ProcessSample(new Vector2(.12f, 0), .2f, .1f);
            Require(Mathf.Abs(calibration.MapRootPosition(new Vector2(.12f, 0), .2f).x) < .001f, "Off-centre calibration preserves neutral position");
            Require(Mathf.Abs(calibration.MapRootPosition(new Vector2(-.4f, 0), .2f).x + sideline) < .001f, "Off-centre calibration preserves left sideline");
            Require(Mathf.Abs(calibration.MapRootPosition(new Vector2(.4f, 0), .2f).x - sideline) < .001f, "Off-centre calibration preserves right sideline");

            var paddleObject = new GameObject("TestPaddle");
            SceneManager.MoveGameObjectToScene(paddleObject, scene);
            var paddle = paddleObject.AddComponent<PaddleKinematicController>();
            paddle.enabled = false;
            var box = paddleObject.GetComponent<BoxCollider>();
            box.size = new Vector3(2, .1f, 2);
            var ballObject = new GameObject("TestBall");
            SceneManager.MoveGameObjectToScene(ballObject, scene);
            var ball = ballObject.AddComponent<BallContactController>();
            ball.enabled = false;
            ballObject.GetComponent<SphereCollider>().radius = .037f;
            var body = ballObject.GetComponent<Rigidbody>();
            body.useGravity = false;
            ball.ResetBall();
            body.position = new Vector3(0, 1, 0);
            body.linearVelocity = new Vector3(0, -5, 0);
            Physics.SyncTransforms();
            var physics = scene.GetPhysicsScene();
            for (int i = 0; i < 20 && ball.HitCount == 0; i++)
            {
                ball.SendMessage("FixedUpdate");
                physics.Simulate(.02f);
            }
            Require(ball.HitCount > 0, "Ball/paddle collision callback");
            Require(body.linearVelocity.y > 0, "Ball must leave paddle upward");
            Require(body.linearVelocity.magnitude <= 18.01f, "Maximum outgoing speed");
            int previousHits = ball.HitCount;
            ball.ResetBall();
            body.position = new Vector3(0, 1, 0);
            body.linearVelocity = new Vector3(0, -18, 0);
            Physics.SyncTransforms();
            for (int i = 0; i < 10 && ball.HitCount == previousHits; i++)
            {
                ball.SendMessage("FixedUpdate");
                physics.Simulate(.02f);
            }
            Require(ball.HitCount > previousHits && body.linearVelocity.y > 0, "Fast CCD hit");
            return "PASS: calibration, translation/depth mapping, recalibration, ball-paddle collision, outgoing direction, speed limit";
        }
        finally
        {
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            SceneManager.UnloadSceneAsync(scene);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Validation failed: " + message);
    }
}
