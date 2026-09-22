# Offline Coach (demo)

Runtime uses the existing local pose model and deterministic sequence rules, not a validated pickleball technique model. No runtime network client is added. Do not enable the legacy GeminiAIApi component.

## Unity
UI is stored in **Assets/Scenes/TrainingUI.unity**, separate from gameplay in Training.
Open TrainingUI additively beside Training to edit Canvas objects in Edit Mode.
Training loads TrainingUI additively on Play; an already-open UI scene is reused.
The UI contains the HUD, Coach panel and the single EventSystem. Tracking, physics,
Coach analysis and AudioSource remain in Training. Cross-scene gameplay references
are resolved at runtime, not serialized into either scene.
When UI is opened alone, gameplay controls are disabled; loading Training reconnects them.
Layout builders run only in the Editor and do not overwrite an existing UI hierarchy.
Use **PickleVerse > UI > Separate Training UI scene** to install/reconnect if needed.
Both scenes are enabled in Build Settings; the existing startup scene order is preserved.

Open Training. Run **PickleVerse > Coach > Install or refresh in Training** (Edit Mode).
Settings: Assets/OfflineCoach/CoachSettings.asset.
HUD controls are independent: disabling voice does not disable analysis.
Recalibrate calls ResetSession and clears history, pending speech and feedback.
Unknown movements display text only. Four supported feedback messages map to four fixed MP3 clips (WAV fallback supported).
Missing clips are explicitly shown in the HUD; there is no online fallback.

## Current voice assets: Edge TTS
Four real MP3 clips are included in Assets/OfflineCoach/Voice and assigned in Training.
Voice: vi-VN-HoaiMyNeural (female), 24 kHz mono. Runtime playback is fully offline.
Only the four fixed sentences were sent during generation; no player data was used.
The generator is outside Assets and is never called by the game.

To regenerate from PowerShell at the project root:

```powershell
python -m venv Library/CoachTTS
./Library/CoachTTS/Scripts/python.exe -m pip install -r Tools/requirements-coach-tts.txt
./Library/CoachTTS/Scripts/python.exe Tools/GenerateCoachVoiceEdge.py --dry-run
./Library/CoachTTS/Scripts/python.exe Tools/GenerateCoachVoiceEdge.py
```

Existing files are kept unless --force is supplied. In Unity run the Coach install/refresh
menu to reassign assets. MP3 is preferred over WAV when both exist.
Edge TTS is a community client for an online service, not an official guaranteed API.
Review the service terms before commercial distribution: https://github.com/rany2/edge-tts
Listen to all four clips on the target machine before signing off pronunciation/audio QA.

## Optional alternative: Google Cloud voice generation
This alternative is not required for the included Edge TTS clips.

1. Use your Google Cloud project with billing and Text-to-Speech API enabled.
2. Configure local Application Default Credentials using Google Cloud CLI:
   gcloud auth application-default login
3. Set GOOGLE_CLOUD_PROJECT locally to your project id.
4. In PowerShell 7, run:
   pwsh -File Tools/GenerateCoachVoice.ps1 -DryRun
   pwsh -File Tools/GenerateCoachVoice.ps1
5. In Unity run the install/refresh menu again. It assigns the four WAV assets.
6. Listen to each sentence and test the volume/mute controls while Internet is disconnected.

Alternatively the script supports an existing local GOOGLE_API_KEY environment variable. Never put keys, ADC JSON files, tokens or credentials in Assets, source control, scenes or builds. Do not paste them into chat. The generator sends only the four fixed strings; it does not read webcam frames or player poses. Normal TLS certificate checks remain enabled. Existing audio files are retained unless -Force is explicitly supplied.

Output: Assets/OfflineCoach/Voice/{InsufficientData,StraightKnees,ShortFinish,Recorded}.wav.
Voice: vi-VN-Standard-A, female, 24 kHz LINEAR16/WAV.
The tool stays outside Assets and is not included in the player.

Official references:
- https://docs.cloud.google.com/text-to-speech/docs/list-voices-and-types
- https://docs.cloud.google.com/text-to-speech/docs/create-audio-text-client-libraries
- https://docs.cloud.google.com/text-to-speech/docs/reference/rest/v1/text/synthesize

## Verification
Run **PickleVerse > Coach > Run deterministic validation** or OfflineCoachValidation.Run() via Unity CLI.
In Play Mode, OfflineCoachValidation.RunRuntime() exercises the actual button callbacks and AudioSource using a temporary silent test clip (not a substitute for Vietnamese speech).
Covers translation/scale, mirror, forehand/unknown, rule priority, optional legs, missing/duplicate observations, buffer ownership, contact association, zero managed allocation per input sample, speech interval/repeat/expiry.
Existing physics tests remain PaddleTrackingValidation.Run() and OfflineTrainingValidation.Run() in Play Mode.

Manual acceptance still required: real forehand sequences, Vietnamese clarity, camera unplug/occlusion, coach and voice buttons, volume, and a Windows offline build. A webcam pose system cannot infer accurate 3D hip rotation, actual force or real paddle contact from these 2D landmarks. Teaching claims require a coach/instructor to validate the thresholds.

### Verified in Editor, 2026-09-21
- 34 deterministic assertions passed, including zero managed allocation over 1,000 pose samples.
- Runtime button/volume/mute-analysis/reset/stop-speech checks passed.
- Existing PaddleTrackingValidation and OfflineTrainingValidation passed.
- One EventSystem, one Coach panel, preview hidden by default, no GeminiAIApi scene component.
- Vietnamese glyphs and panel layout inspected at 1280x720; Console reported no compile/runtime errors.
- Voice generator dry-run validated all four fixed texts. No TTS request made: local credentials absent.
- Real Vietnamese WAV assets, listening QA, disconnected-network Windows build and instructor validation remain pending.

### Voice integration verified, 2026-09-22
- Generated and assigned all four Edge TTS MP3 clips in Training; no credentials required.
- Unity decoded each as 24 kHz mono with a nonzero signal; lengths: 5.592, 3.144, 2.592 and 4.248 seconds.
- CoachVoicePlayer successfully started Recorded through its normal feedback queue.
- Source mute=false, volume=0.8; AudioListener pause=false, volume=1.
- 34 deterministic assertions and runtime button/volume/mute tests passed again.
- Final Console check found no compile/runtime errors; all clip references remain assigned in Edit Mode.
- Human listening QA, disconnected-network Windows build and instructor validation remain pending.
