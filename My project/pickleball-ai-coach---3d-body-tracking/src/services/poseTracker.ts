/**
 * Ultra-Low Latency MediaPipe Pose Tracker Service
 * Runs WebGL/GPU accelerated BlazePose with smooth EMA filtering
 * Extracts both Normalized 2D/3D landmarks and Real-World 3D Metric landmarks
 */

import { FilesetResolver, PoseLandmarker } from '@mediapipe/tasks-vision';
import { Landmark3D, PoseTrackerStats } from '../types';

export type PoseCallback = (
  landmarks: Landmark3D[],
  stats: PoseTrackerStats,
  worldLandmarks?: Landmark3D[]
) => void;

class PoseTrackerService {
  private landmarker: PoseLandmarker | null = null;
  private isInitializing = false;
  private isRunning = false;
  private videoElement: HTMLVideoElement | null = null;
  private animationFrameId: number | null = null;
  private lastVideoTime = -1;
  private callback: PoseCallback | null = null;

  // Smoothing filter state for normalized landmarks
  private smoothedLandmarks: Landmark3D[] = [];
  // Smoothing filter state for real-world 3D metric landmarks
  private smoothedWorldLandmarks: Landmark3D[] = [];
  private smoothingFactor = 0.65; // High responsiveness for low latency

  // Performance telemetry
  private frameCount = 0;
  private lastFpsUpdateTime = performance.now();
  private currentFps = 0;
  private lastInferenceLatency = 0;

  public async initialize(): Promise<boolean> {
    if (this.landmarker) return true;
    if (this.isInitializing) return false;

    this.isInitializing = true;
    try {
      const wasmFileset = await FilesetResolver.forVisionTasks(
        'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@latest/wasm'
      );

      // Try pose_landmarker_full.task for maximum joint accuracy on desktop/laptop webcams
      try {
        this.landmarker = await PoseLandmarker.createFromOptions(wasmFileset, {
          baseOptions: {
            modelAssetPath:
              'https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_full/float16/latest/pose_landmarker_full.task',
            delegate: 'GPU',
          },
          runningMode: 'VIDEO',
          numPoses: 1,
          minPoseDetectionConfidence: 0.5,
          minPosePresenceConfidence: 0.5,
          minTrackingConfidence: 0.5,
          outputSegmentationMasks: false,
        });
      } catch (fullGpuErr) {
        console.warn('PoseLandmarker full GPU init failed, trying pose_landmarker_lite...', fullGpuErr);
        this.landmarker = await PoseLandmarker.createFromOptions(wasmFileset, {
          baseOptions: {
            modelAssetPath:
              'https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/latest/pose_landmarker_lite.task',
            delegate: 'GPU',
          },
          runningMode: 'VIDEO',
          numPoses: 1,
          minPoseDetectionConfidence: 0.5,
          minPosePresenceConfidence: 0.5,
          minTrackingConfidence: 0.5,
          outputSegmentationMasks: false,
        });
      }

      this.isInitializing = false;
      return true;
    } catch (err) {
      console.warn('GPU PoseLandmarker init failed, retrying with CPU delegate...', err);
      try {
        const wasmFileset = await FilesetResolver.forVisionTasks(
          'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@latest/wasm'
        );
        this.landmarker = await PoseLandmarker.createFromOptions(wasmFileset, {
          baseOptions: {
            modelAssetPath:
              'https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/latest/pose_landmarker_lite.task',
            delegate: 'CPU',
          },
          runningMode: 'VIDEO',
          numPoses: 1,
          minPoseDetectionConfidence: 0.5,
          minPosePresenceConfidence: 0.5,
          minTrackingConfidence: 0.5,
        });
        this.isInitializing = false;
        return true;
      } catch (cpuErr) {
        console.error('PoseLandmarker initialization failed completely:', cpuErr);
        this.isInitializing = false;
        return false;
      }
    }
  }

  public start(video: HTMLVideoElement, onPose: PoseCallback) {
    this.videoElement = video;
    this.callback = onPose;
    this.isRunning = true;
    this.lastVideoTime = -1;
    this.frameCount = 0;
    this.lastFpsUpdateTime = performance.now();
    this.processLoop();
  }

  public stop() {
    this.isRunning = false;
    if (this.animationFrameId !== null) {
      cancelAnimationFrame(this.animationFrameId);
      this.animationFrameId = null;
    }
  }

  private processLoop = () => {
    if (!this.isRunning || !this.videoElement) return;

    const video = this.videoElement;

    // Only process new frames when video is ready
    if (video.readyState >= 2 && video.currentTime !== this.lastVideoTime && this.landmarker) {
      const startTime = performance.now();
      const currentVideoTime = video.currentTime;
      this.lastVideoTime = currentVideoTime;

      try {
        const result = this.landmarker.detectForVideo(video, startTime);
        const endTime = performance.now();
        this.lastInferenceLatency = Math.round(endTime - startTime);

        this.frameCount++;
        if (endTime - this.lastFpsUpdateTime >= 600) {
          this.currentFps = Math.round((this.frameCount * 1000) / (endTime - this.lastFpsUpdateTime));
          this.frameCount = 0;
          this.lastFpsUpdateTime = endTime;
        }

        if (result.landmarks && result.landmarks.length > 0) {
          const rawLandmarks = result.landmarks[0];
          // Apply low-latency Exponential Moving Average filter
          const filtered = this.applyFilter(rawLandmarks);

          // Extract real-world metric 3D landmarks (origin at hip center in meters)
          let filteredWorld: Landmark3D[] | undefined = undefined;
          if (result.worldLandmarks && result.worldLandmarks.length > 0) {
            const rawWorld = result.worldLandmarks[0];
            filteredWorld = this.applyWorldFilter(rawWorld);
          }

          if (this.callback) {
            this.callback(
              filtered,
              {
                fps: this.currentFps || 30,
                latencyMs: this.lastInferenceLatency,
                isTracking: true,
                detectedLandmarksCount: filtered.length,
              },
              filteredWorld
            );
          }
        } else {
          if (this.callback) {
            this.callback([], {
              fps: this.currentFps || 30,
              latencyMs: this.lastInferenceLatency,
              isTracking: false,
              detectedLandmarksCount: 0,
            });
          }
        }
      } catch (e) {
        // Continue tracking even if a single frame fails
      }
    }

    this.animationFrameId = requestAnimationFrame(this.processLoop);
  };

  private applyFilter(raw: Landmark3D[]): Landmark3D[] {
    if (this.smoothedLandmarks.length !== raw.length) {
      this.smoothedLandmarks = raw.map(l => ({ ...l }));
      return this.smoothedLandmarks;
    }

    const alpha = this.smoothingFactor;
    for (let i = 0; i < raw.length; i++) {
      const cur = raw[i];
      const prev = this.smoothedLandmarks[i];
      prev.x = prev.x * (1 - alpha) + cur.x * alpha;
      prev.y = prev.y * (1 - alpha) + cur.y * alpha;
      prev.z = prev.z * (1 - alpha) + cur.z * alpha;
      prev.visibility = cur.visibility;
    }

    return this.smoothedLandmarks;
  }

  private applyWorldFilter(raw: Landmark3D[]): Landmark3D[] {
    if (this.smoothedWorldLandmarks.length !== raw.length) {
      this.smoothedWorldLandmarks = raw.map(l => ({ ...l }));
      return this.smoothedWorldLandmarks;
    }

    const alpha = 0.7; // Fast responsive tracking for 3D body motion
    for (let i = 0; i < raw.length; i++) {
      const cur = raw[i];
      const prev = this.smoothedWorldLandmarks[i];
      prev.x = prev.x * (1 - alpha) + cur.x * alpha;
      prev.y = prev.y * (1 - alpha) + cur.y * alpha;
      prev.z = prev.z * (1 - alpha) + cur.z * alpha;
      prev.visibility = cur.visibility;
    }

    return this.smoothedWorldLandmarks;
  }
}

export const poseTracker = new PoseTrackerService();
