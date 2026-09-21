# Offline tracking demo

Open Assets/Scenes/Training.unity and enter Play Mode on Windows with a webcam.
Stand facing the camera with shoulders and hips visible for two seconds.
Use Recalibrate after repositioning the camera. The preview and skeleton mirror
the image; detection runs on the original image to preserve anatomical right-hand labels.

The yellow ball resets every 12 seconds or when it leaves the court bounds.
When a body is tracked, it drops 65 cm above the right wrist so that it can be hit.
The paddle uses a kinematic Rigidbody; its face is local XZ, normal local Y,
and handle points along local Z. Grip/rotation offsets are Inspector settings.

Paddle tracking fix: `Local Grip Point` is (0, 0, 0.05715), the centre of the
generated handle. Position is solved from the filtered wrist and filtered rotation,
not by smoothing the paddle origin independently. Losing hand landmarks holds
rotation while body wrist tracking continues; losing the wrist holds the last target.
Samples older than 250 ms are rejected and reacquisition is speed-limited.

No extra grip calibration is required. The default closed-grip basis maps the
pinky-to-index knuckle direction to paddle +Z, and the palm normal to paddle +Y.
`Grip Rotation Offset` on PickleballPaddle is the only mounting-angle adjustment;
zero means this canonical grip, not a measured fit to every player's real paddle.
If a different grip is used, adjust this Inspector value (in Edit Mode to persist).
The HUD reports Wrist and Rotation independently as tracking/holding.

The supplied ONNX graph links `conv_landmarks` to Identity, `conv_handflag` to
Identity_1 and `conv_handedness` to Identity_2. Only Identity_1 gates presence.
The hand input remains NCHW RGB [0,1], with a square crop measured in source pixels,
sized from the forearm and black-padded outside the image. Mirroring is applied
once to output geometry, never to the model input or anatomical joint indices.

Run `PaddleTrackingValidation.Run()` in Play Mode through MCP for synthetic tests
of horizontal/vertical/mirrored axes, invalid landmarks, edge crops, grip anchoring,
hand/wrist loss, stale samples and bounded reacquisition. These do not validate
the real grip angle: test horizontal, vertical and twist motions on the demo camera.

All inference is local. GeminiAIApi remains as unused source for compatibility;
the training HUD does not instantiate it or send requests.

Editor setup: Tools/PickleVerse/Setup Offline Tracking Training. Run only in
Edit Mode with Training active. It removes duplicate named paddle instances and
PoseTrackerMock, wires model assets, saves the ball prefab and saves Training.
Original tracked assets can be recovered from source control.

Automated validation: in Play Mode, evaluate OfflineTrainingValidation.Run()
through Unity Pipeline. It creates and removes a separate physics scene, checking
calibration, root mapping, reset, paddle contact, outgoing direction and fast CCD.

Limitations: monocular depth is an estimate based on shoulder width, so turning
sideways changes the estimate. Body joints remain planar relative to the root.
Hand presence uses Identity_1 in the supplied hand_landmark model; if replacing
the model, verify that output and the NCHW 224x224 / pixel-coordinate contract.
Actual hand tracking, occlusion recovery, fast real swings and sustained frame
rate must be checked on the target camera and machine. Synthetic physics tests
do not establish real-camera accuracy or a sustained 30 FPS performance result.
