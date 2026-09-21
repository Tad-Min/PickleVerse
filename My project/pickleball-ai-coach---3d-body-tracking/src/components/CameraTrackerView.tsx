/**
 * Camera Viewport with 2D Skeleton HUD, Low-Latency Telemetry & Robust Multi-Camera Support
 */

import React, { useEffect, useRef, useState } from 'react';
import { Landmark3D, PoseAnalysisResult, PoseTrackerStats } from '../types';
import { poseTracker } from '../services/poseTracker';
import {
  AlertTriangle,
  Camera,
  CameraOff,
  CheckCircle2,
  ChevronDown,
  ExternalLink,
  FlipHorizontal,
  Info,
  RefreshCw,
  Smartphone,
  Sparkles,
  User,
  Zap,
} from 'lucide-react';

interface CameraTrackerViewProps {
  onPoseUpdate: (
    landmarks: Landmark3D[],
    stats: PoseTrackerStats,
    worldLandmarks?: Landmark3D[]
  ) => void;
  analysis: PoseAnalysisResult;
  isSimulating: boolean;
  onToggleSimulator: () => void;
  isRightHanded?: boolean;
  onOpenPhoneConnect?: () => void;
  targetDeviceId?: string;
  onDevicesFound?: (devices: MediaDeviceInfo[]) => void;
}

export const CameraTrackerView: React.FC<CameraTrackerViewProps> = ({
  onPoseUpdate,
  analysis,
  isSimulating,
  onToggleSimulator,
  isRightHanded = true,
  onOpenPhoneConnect,
  targetDeviceId,
  onDevicesFound,
}) => {
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);

  const [hasCameraAccess, setHasCameraAccess] = useState<boolean | null>(null);
  const [cameraError, setCameraError] = useState<string | null>(null);
  const [facingMode, setFacingMode] = useState<'user' | 'environment'>('user');
  const [isConnecting, setIsConnecting] = useState<boolean>(false);
  const [showTroubleshoot, setShowTroubleshoot] = useState<boolean>(false);
  const [trackingMode, setTrackingMode] = useState<'upper' | 'full'>('upper');

  // Available camera devices
  const [availableDevices, setAvailableDevices] = useState<MediaDeviceInfo[]>([]);
  const [selectedDeviceId, setSelectedDeviceId] = useState<string>('');
  const [activeCameraLabel, setActiveCameraLabel] = useState<string>('');

  const [stats, setStats] = useState<PoseTrackerStats>({
    fps: 0,
    latencyMs: 0,
    isTracking: false,
    detectedLandmarksCount: 0,
  });

  const streamRef = useRef<MediaStream | null>(null);
  const isComponentMounted = useRef(true);

  // Parse errors into clear, friendly Vietnamese instructions
  const interpretCameraError = (err: unknown): { message: string; type: string } => {
    if (!err) return { message: 'Không thể kết nối camera máy tính.', type: 'unknown' };

    const errorObj = err as { name?: string; message?: string };
    const name = errorObj.name || '';
    const rawMsg = errorObj.message || '';

    if (name === 'NotAllowedError' || name === 'PermissionDeniedError') {
      return {
        type: 'permission',
        message:
          'Trình duyệt chưa được cấp quyền truy cập Camera. Hãy bấm vào biểu tượng Ổ khóa 🔒 hoặc Camera trên thanh địa chỉ của trình duyệt, chọn "Cho phép" (Allow) rồi thử lại.',
      };
    }
    if (name === 'NotFoundError' || name === 'DevicesNotFoundError') {
      return {
        type: 'not_found',
        message:
          'Không tìm thấy thiết bị webcam nào trên máy tính. Hãy kiểm tra lại kết nối cáp USB camera hoặc webcam tích hợp của máy.',
      };
    }
    if (name === 'NotReadableError' || name === 'TrackStartError') {
      return {
        type: 'occupied',
        message:
          'Camera đang bị ứng dụng khác (Zoom, Google Meet, Teams, OBS, Zalo...) sử dụng hoặc phím gạt che camera trên máy tính đang khóa. Vui lòng tắt các app đó và kiểm tra nắp che webcam.',
      };
    }
    if (name === 'OverconstrainedError') {
      return {
        type: 'constraint',
        message:
          'Webcam không hỗ trợ độ phân giải hoặc định dạng này. Hệ thống đang tự động chuyển về chế độ tương thích an toàn.',
      };
    }
    if (name === 'SecurityError') {
      return {
        type: 'security',
        message:
          'Bảo mật trình duyệt chặn mở camera trong khung nhúng (iframe). Bạn hãy nhấn nút "Mở trong tab mới" bên dưới để mở toàn quyền.',
      };
    }

    return {
      type: 'generic',
      message: rawMsg || 'Lỗi không xác định khi kết nối với camera máy tính.',
    };
  };

  // Enumerate all available camera devices
  const refreshDevicesList = async () => {
    try {
      if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices) {
        const devices = await navigator.mediaDevices.enumerateDevices();
        const videoInputs = devices.filter(d => d.kind === 'videoinput');
        setAvailableDevices(videoInputs);
        onDevicesFound?.(videoInputs);
      }
    } catch (e) {
      console.warn('Cannot enumerate media devices:', e);
    }
  };

  // Sync external device selection (e.g. from Phone Connect Modal)
  useEffect(() => {
    if (targetDeviceId && targetDeviceId !== selectedDeviceId) {
      setSelectedDeviceId(targetDeviceId);
      startCamera(targetDeviceId);
    }
  }, [targetDeviceId]);

  // Robust multi-tier stream fetcher that never fails on overconstrained desktop cameras
  const requestMediaStream = async (
    targetDeviceId?: string,
    targetFacingMode?: 'user' | 'environment'
  ): Promise<MediaStream> => {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      throw new Error(
        'Trình duyệt hoặc môi trường hiện tại không hỗ trợ WebRTC MediaDevices API.'
      );
    }

    // Tier 1: Ideal 640x480 for lowest latency MediaPipe inference
    try {
      const constraintsTier1: MediaStreamConstraints = {
        audio: false,
        video: {
          ...(targetDeviceId
            ? { deviceId: { exact: targetDeviceId } }
            : { facingMode: { ideal: targetFacingMode || 'user' } }),
          width: { ideal: 640 },
          height: { ideal: 480 },
          frameRate: { ideal: 30, max: 60 },
        },
      };
      return await navigator.mediaDevices.getUserMedia(constraintsTier1);
    } catch (err1) {
      console.warn('Camera Tier 1 constraint failed, trying Tier 2 (relaxed resolution)...', err1);
    }

    // Tier 2: Relaxed resolution without strict bounds
    try {
      const constraintsTier2: MediaStreamConstraints = {
        audio: false,
        video: targetDeviceId
          ? { deviceId: { exact: targetDeviceId } }
          : { width: { ideal: 640 } },
      };
      return await navigator.mediaDevices.getUserMedia(constraintsTier2);
    } catch (err2) {
      console.warn('Camera Tier 2 constraint failed, trying Tier 3 (universal safe fallback)...', err2);
    }

    // Tier 3: Universal fallback: simply request video without ANY constraints
    return await navigator.mediaDevices.getUserMedia({
      audio: false,
      video: true,
    });
  };

  // Initialize camera and tracker
  const startCamera = async (deviceIdToUse?: string) => {
    setIsConnecting(true);
    setCameraError(null);

    try {
      if (streamRef.current) {
        streamRef.current.getTracks().forEach(t => t.stop());
        streamRef.current = null;
      }

      const deviceId = deviceIdToUse || selectedDeviceId;
      const stream = await requestMediaStream(deviceId, facingMode);
      streamRef.current = stream;

      const activeVideoTrack = stream.getVideoTracks()[0];
      if (activeVideoTrack) {
        const settings = activeVideoTrack.getSettings();
        setActiveCameraLabel(activeVideoTrack.label || 'Webcam Máy Tính');
        if (settings.deviceId && !selectedDeviceId) {
          setSelectedDeviceId(settings.deviceId);
        }
      }

      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        try {
          await videoRef.current.play();
        } catch (playErr) {
          console.warn('Video auto-play warning:', playErr);
        }
      }

      setHasCameraAccess(true);
      await refreshDevicesList();

      // Initialize MediaPipe PoseLandmarker with full precision
      const isReady = await poseTracker.initialize();
      if (isReady && videoRef.current && isComponentMounted.current) {
        poseTracker.start(videoRef.current, (landmarks, trackerStats, worldLandmarks) => {
          if (!isComponentMounted.current) return;

          // Check if legs are visible in frame
          const hasAnkles =
            landmarks[27] &&
            landmarks[28] &&
            (landmarks[27].visibility ?? 0) > 0.45 &&
            (landmarks[28].visibility ?? 0) > 0.45 &&
            landmarks[27].y < 0.95 &&
            landmarks[28].y < 0.95;

          setTrackingMode(hasAnkles ? 'full' : 'upper');
          setStats(trackerStats);
          onPoseUpdate(landmarks, trackerStats, worldLandmarks);
          drawSkeletonHUD(landmarks);
        });
      }
    } catch (err: unknown) {
      console.warn('Camera access denied or unavailable:', err);
      const parsed = interpretCameraError(err);
      setCameraError(parsed.message);
      setHasCameraAccess(false);
    } finally {
      setIsConnecting(false);
    }
  };

  useEffect(() => {
    isComponentMounted.current = true;
    if (!isSimulating) {
      startCamera();
    }

    return () => {
      isComponentMounted.current = false;
      poseTracker.stop();
      if (streamRef.current) {
        streamRef.current.getTracks().forEach(t => t.stop());
        streamRef.current = null;
      }
    };
  }, [facingMode, isSimulating]);

  const handleDeviceChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const newDeviceId = e.target.value;
    setSelectedDeviceId(newDeviceId);
    startCamera(newDeviceId);
  };

  const toggleFacingMode = () => {
    setFacingMode(prev => (prev === 'user' ? 'environment' : 'user'));
  };

  // 2D Skeleton Overlay on Camera Canvas (Clean, Athlete-focused HUD)
  const drawSkeletonHUD = (landmarks: Landmark3D[]) => {
    const canvas = canvasRef.current;
    const video = videoRef.current;
    if (!canvas || !video) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const w = canvas.width;
    const h = canvas.height;
    ctx.clearRect(0, 0, w, h);

    if (!landmarks || landmarks.length < 33) return;

    // Helper: convert raw landmark to mirrored canvas coordinates
    const toCanvas = (idx: number) => {
      const lm = landmarks[idx];
      if (!lm) return null;
      return {
        x: (1.0 - lm.x) * w,
        y: lm.y * h,
        vis: lm.visibility ?? 1,
      };
    };

    // Main Torso & Arm Bones
    const mainBones: [number, number][] = [
      // Shoulders
      [11, 12],
      // Spine / Torso sides
      [11, 23],
      [12, 24],
      [23, 24],
      // Left Arm
      [11, 13],
      [13, 15],
      // Right Arm
      [12, 14],
      [14, 16],
    ];

    // Articulated Hand Digit Bones (Wrists -> Thumb, Index, Pinky, Metacarpals)
    const handBones: [number, number][] = [
      // Left hand
      [15, 21], // Wrist to Thumb
      [15, 19], // Wrist to Index
      [15, 17], // Wrist to Pinky
      [19, 17], // Palm arch
      [19, 21], // Thumb-Index web
      // Right hand
      [16, 22], // Wrist to Thumb
      [16, 20], // Wrist to Index
      [16, 18], // Wrist to Pinky
      [20, 18], // Palm arch
      [20, 22], // Thumb-Index web
    ];

    // Articulated Leg Bones
    const legBones: [number, number][] = [
      [23, 25],
      [25, 27],
      [24, 26],
      [26, 28],
    ];

    // Articulated Foot & Toe Digit Bones (Ankle -> Heel, Metatarsal & Toe)
    const footBones: [number, number][] = [
      // Left foot
      [27, 29], // Ankle to Heel (Calcaneus)
      [27, 31], // Ankle to Toe / Foot Index (Metatarsal)
      [29, 31], // Sole base
      // Right foot
      [28, 30], // Ankle to Heel (Calcaneus)
      [28, 32], // Ankle to Toe / Foot Index (Metatarsal)
      [30, 32], // Sole base
    ];

    const boneColor =
      analysis.score >= 85 ? '#10b981' : analysis.score >= 60 ? '#f59e0b' : '#ef4444';

    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';

    // 1. Draw Under-glow / Outline Shadow for high contrast
    ctx.lineWidth = 8.5;
    ctx.strokeStyle = 'rgba(15, 23, 42, 0.75)';
    mainBones.concat(legBones).forEach(([i1, i2]) => {
      const p1 = toCanvas(i1);
      const p2 = toCanvas(i2);
      if (p1 && p2 && p1.vis > 0.35 && p2.vis > 0.35) {
        ctx.beginPath();
        ctx.moveTo(p1.x, p1.y);
        ctx.lineTo(p2.x, p2.y);
        ctx.stroke();
      }
    });

    // 2. Draw Main Torso & Limb Bones (Tăng kích thước khối xương dày dặn 5.5px)
    ctx.lineWidth = 5.5;
    ctx.strokeStyle = boneColor;
    mainBones.forEach(([i1, i2]) => {
      const p1 = toCanvas(i1);
      const p2 = toCanvas(i2);
      if (p1 && p2 && p1.vis > 0.35 && p2.vis > 0.35) {
        ctx.beginPath();
        ctx.moveTo(p1.x, p1.y);
        ctx.lineTo(p2.x, p2.y);
        ctx.stroke();
      }
    });

    // Draw leg bones if visible (Khối xương chân lực lưỡng)
    legBones.forEach(([i1, i2]) => {
      const p1 = toCanvas(i1);
      const p2 = toCanvas(i2);
      if (p1 && p2 && p1.vis > 0.35 && p2.vis > 0.35) {
        ctx.beginPath();
        ctx.moveTo(p1.x, p1.y);
        ctx.lineTo(p2.x, p2.y);
        ctx.stroke();
      }
    });

    // 3. Draw Articulated Foot & Toe Bones (Đốt xương bàn chân & gót chân)
    ctx.lineWidth = 4.0;
    ctx.strokeStyle = '#0ea5e9';
    footBones.forEach(([i1, i2]) => {
      const p1 = toCanvas(i1);
      const p2 = toCanvas(i2);
      if (p1 && p2 && p1.vis > 0.35 && p2.vis > 0.35) {
        ctx.beginPath();
        ctx.moveTo(p1.x, p1.y);
        ctx.lineTo(p2.x, p2.y);
        ctx.stroke();
      }
    });

    // 4. Draw Articulated Hand & Finger Bones (Đốt xương bàn tay & ngón tay)
    ctx.lineWidth = 3.5;
    ctx.strokeStyle = '#38bdf8';
    handBones.forEach(([i1, i2]) => {
      const p1 = toCanvas(i1);
      const p2 = toCanvas(i2);
      if (p1 && p2 && p1.vis > 0.35 && p2.vis > 0.35) {
        ctx.beginPath();
        ctx.moveTo(p1.x, p1.y);
        ctx.lineTo(p2.x, p2.y);
        ctx.stroke();
      }
    });

    // Draw Neck line from midpoint of shoulders to Nose
    const s1 = toCanvas(11);
    const s2 = toCanvas(12);
    const nose = toCanvas(0);
    if (s1 && s2 && nose && nose.vis > 0.4) {
      const midShoulderX = (s1.x + s2.x) / 2;
      const midShoulderY = (s1.y + s2.y) / 2;
      ctx.beginPath();
      ctx.moveTo(midShoulderX, midShoulderY);
      ctx.lineTo(nose.x, nose.y);
      ctx.lineWidth = 4.5;
      ctx.strokeStyle = boneColor;
      ctx.stroke();

      // Clean head circle around nose
      ctx.beginPath();
      ctx.arc(nose.x, nose.y, 16, 0, Math.PI * 2);
      ctx.fillStyle = 'rgba(56, 189, 248, 0.25)';
      ctx.fill();
      ctx.strokeStyle = '#38bdf8';
      ctx.lineWidth = 2.5;
      ctx.stroke();
    }

    // Draw Major Joint Nodes (Shoulders, elbows, wrists, hips, knees, ankles)
    const keyJoints = [11, 12, 13, 14, 15, 16, 23, 24, 25, 26, 27, 28];
    keyJoints.forEach(idx => {
      const p = toCanvas(idx);
      if (p && p.vis > 0.35) {
        ctx.beginPath();
        ctx.arc(p.x, p.y, 7, 0, Math.PI * 2);
        ctx.fillStyle = idx === 16 || idx === 15 ? '#38bdf8' : '#ffffff';
        ctx.fill();
        ctx.strokeStyle = '#0f172a';
        ctx.lineWidth = 2.0;
        ctx.stroke();
      }
    });

    // Draw Hand Digit Joint Nodes (Pinky 17/18, Index 19/20, Thumb 21/22)
    const handJoints = [17, 18, 19, 20, 21, 22];
    handJoints.forEach(idx => {
      const p = toCanvas(idx);
      if (p && p.vis > 0.35) {
        ctx.beginPath();
        ctx.arc(p.x, p.y, 4.5, 0, Math.PI * 2);
        ctx.fillStyle = '#38bdf8';
        ctx.fill();
        ctx.strokeStyle = '#0f172a';
        ctx.lineWidth = 1.5;
        ctx.stroke();
      }
    });

    // Draw Foot Digit Joint Nodes (Heel 29/30, Toe / Foot Index 31/32)
    const footJoints = [29, 30, 31, 32];
    footJoints.forEach(idx => {
      const p = toCanvas(idx);
      if (p && p.vis > 0.35) {
        ctx.beginPath();
        ctx.arc(p.x, p.y, 5.5, 0, Math.PI * 2);
        ctx.fillStyle = '#06b6d4';
        ctx.fill();
        ctx.strokeStyle = '#0f172a';
        ctx.lineWidth = 1.5;
        ctx.stroke();
      }
    });

    // Draw Paddle Racket on Dominant Wrist
    const paddleWristIdx = isRightHanded ? 16 : 15;
    const paddleElbowIdx = isRightHanded ? 14 : 13;
    const pw = toCanvas(paddleWristIdx);
    const pe = toCanvas(paddleElbowIdx);
    if (pw && pe && pw.vis > 0.4) {
      const dx = pw.x - pe.x;
      const dy = pw.y - pe.y;
      const angle = Math.atan2(dy, dx);

      ctx.save();
      ctx.translate(pw.x, pw.y);
      ctx.rotate(angle);

      // Draw paddle handle
      ctx.fillStyle = '#0f172a';
      ctx.fillRect(0, -3, 18, 6);

      // Draw paddle head (oblong)
      ctx.fillStyle = '#0284c7';
      ctx.strokeStyle = '#38bdf8';
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.roundRect(16, -14, 28, 28, 7);
      ctx.fill();
      ctx.stroke();

      ctx.restore();
    }

    // Draw Elbow Angle Badge
    const domElbow = isRightHanded ? toCanvas(14) : toCanvas(13);
    if (domElbow && domElbow.vis > 0.4) {
      ctx.fillStyle = 'rgba(15, 23, 42, 0.85)';
      ctx.fillRect(domElbow.x + 8, domElbow.y - 12, 54, 22);
      ctx.fillStyle = analysis.elbowAngle < 110 ? '#ef4444' : '#10b981';
      ctx.font = 'bold 11px JetBrains Mono, monospace';
      ctx.fillText(`${analysis.elbowAngle}° Tay`, domElbow.x + 12, domElbow.y + 3);
    }
  };

  return (
    <div className="relative w-full h-full min-h-[300px] bg-slate-950 rounded-2xl overflow-hidden border border-slate-800 shadow-xl flex flex-col justify-center items-center">
      {isSimulating ? (
        // Simulator Banner
        <div className="flex flex-col items-center justify-center p-6 text-center z-10 space-y-3">
          <div className="p-3 bg-amber-500/10 rounded-2xl border border-amber-500/30">
            <Sparkles className="w-8 h-8 text-amber-400 animate-pulse" />
          </div>
          <div>
            <h4 className="text-base font-bold text-slate-100">
              Đang Chạy Chế Độ Mô Phỏng Pro (Simulator)
            </h4>
            <p className="text-xs text-slate-400 max-w-xs mt-1">
              Model 3D và thuật toán phân tích kỹ thuật đang tự động chuyển động theo form mẫu chuẩn
              Pickleball.
            </p>
          </div>
          <button
            id="switch-to-camera-btn"
            onClick={onToggleSimulator}
            className="flex items-center gap-2 px-4 py-2 bg-sky-500 hover:bg-sky-400 text-slate-950 font-bold text-xs rounded-xl shadow-lg transition-all"
          >
            <Camera className="w-4 h-4" />
            Bật Camera Quét Cơ Thể Thật
          </button>
        </div>
      ) : hasCameraAccess === false ? (
        // Camera Error / Diagnosis & Auto-Recovery View
        <div className="flex flex-col items-center justify-center p-5 sm:p-6 text-center z-10 max-w-md w-full space-y-3.5">
          <div className="p-3 bg-rose-500/15 rounded-2xl border border-rose-500/30 text-rose-400 shadow-inner">
            <CameraOff className="w-8 h-8" />
          </div>

          <div className="space-y-1.5">
            <h4 className="text-sm sm:text-base font-bold text-rose-200 flex items-center justify-center gap-1.5">
              <AlertTriangle className="w-4 h-4 text-rose-400 inline" />
              Không Thể Kết Nối Camera Máy Tính
            </h4>
            <p className="text-xs text-slate-300 leading-relaxed bg-slate-900/80 p-3 rounded-xl border border-slate-800 text-left">
              {cameraError}
            </p>
          </div>

          {/* Action Buttons */}
          <div className="flex flex-wrap items-center justify-center gap-2 pt-1 w-full">
            <button
              id="retry-camera-btn"
              onClick={() => startCamera()}
              disabled={isConnecting}
              className="flex items-center gap-1.5 px-3.5 py-2 bg-sky-500 hover:bg-sky-400 disabled:opacity-50 text-slate-950 text-xs font-bold rounded-xl shadow-lg transition-all"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${isConnecting ? 'animate-spin' : ''}`} />
              {isConnecting ? 'Đang thử kết nối...' : 'Thử lại kết nối'}
            </button>

            {/* If in iframe, open in new tab to resolve permission restrictions */}
            <button
              id="open-new-tab-btn"
              onClick={() => window.open(window.location.href, '_blank')}
              className="flex items-center gap-1.5 px-3 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-xl border border-slate-700 transition-all"
              title="Mở ứng dụng trên tab riêng để trình duyệt cấp toàn quyền Camera"
            >
              <ExternalLink className="w-3.5 h-3.5 text-sky-400" />
              Mở trong Tab Mới
            </button>

            <button
              id="use-simulator-fallback-btn"
              onClick={onToggleSimulator}
              className="flex items-center gap-1.5 px-3 py-2 bg-amber-500 hover:bg-amber-400 text-slate-950 text-xs font-bold rounded-xl shadow-md transition-all"
            >
              <Sparkles className="w-3.5 h-3.5" />
              Dùng Mô Phỏng Pro
            </button>

            {/* Connect Phone Camera Button */}
            {onOpenPhoneConnect && (
              <button
                id="connect-phone-fallback-btn"
                onClick={onOpenPhoneConnect}
                className="flex items-center gap-1.5 px-3.5 py-2 bg-emerald-500 hover:bg-emerald-400 text-slate-950 text-xs font-bold rounded-xl shadow-md transition-all"
                title="Dùng camera điện thoại thông minh (quét mã QR hoặc kết nối webcam)"
              >
                <Smartphone className="w-3.5 h-3.5" />
                Dùng Camera Điện Thoại
              </button>
            )}
          </div>

          {/* Troubleshooting Accordion */}
          <div className="w-full text-left pt-1">
            <button
              id="toggle-camera-troubleshoot-btn"
              onClick={() => setShowTroubleshoot(prev => !prev)}
              className="text-[11px] text-slate-400 hover:text-sky-300 flex items-center justify-between w-full p-2 bg-slate-900/60 hover:bg-slate-900 rounded-lg border border-slate-800/80 transition-colors"
            >
              <span className="flex items-center gap-1">
                <Info className="w-3 h-3 text-sky-400" />
                Mẹo khắc phục lỗi webcam máy tính
              </span>
              <ChevronDown
                className={`w-3 h-3 transition-transform ${showTroubleshoot ? 'rotate-180' : ''}`}
              />
            </button>

            {showTroubleshoot && (
              <div className="mt-2 p-2.5 bg-slate-900/90 rounded-lg border border-slate-800 text-[11px] text-slate-300 space-y-1.5">
                <p className="font-semibold text-slate-200">Các bước kiểm tra nhanh:</p>
                <ul className="list-disc list-inside space-y-1 text-slate-400 pl-1">
                  <li>
                    <strong className="text-slate-300">1. Quyền trình duyệt:</strong> Nhấp vào biểu
                    tượng Ổ khóa 🔒 bên trái thanh địa chỉ URL, bật &quot;Camera&quot; sang &quot;Cho
                    phép&quot; (Allow).
                  </li>
                  <li>
                    <strong className="text-slate-300">2. Nắp che webcam:</strong> Kiểm tra xem phím
                    gạt cơ học hoặc phím Fn trên bàn phím laptop (F8/F10) có đang tắt webcam không.
                  </li>
                  <li>
                    <strong className="text-slate-300">3. Ứng dụng xung đột:</strong> Đóng Zoom,
                    Google Meet, Microsoft Teams, OBS Studio hoặc Zalo đang chạy ngầm.
                  </li>
                  <li>
                    <strong className="text-slate-300">4. Mở tab mới:</strong> Nếu đang xem trong
                    khung xem trước của trình duyệt, hãy bấm nút &quot;Mở trong Tab Mới&quot; ở trên.
                  </li>
                </ul>
              </div>
            )}
          </div>
        </div>
      ) : (
        // Live Video & 2D Canvas View
        <>
          <video
            ref={videoRef}
            playsInline
            muted
            autoPlay
            className="w-full h-full object-cover transform scale-x-[-1]"
            onLoadedMetadata={() => {
              if (canvasRef.current && videoRef.current) {
                canvasRef.current.width = videoRef.current.videoWidth || 640;
                canvasRef.current.height = videoRef.current.videoHeight || 480;
              }
            }}
          />
          <canvas
            ref={canvasRef}
            className="absolute inset-0 w-full h-full object-cover pointer-events-none"
          />
        </>
      )}

      {/* Top Left: Low-Latency Telemetry, Active Camera & Body Coverage Status */}
      <div className="absolute top-3 left-3 flex flex-wrap items-center gap-2 z-20">
        <div className="flex items-center gap-2 bg-slate-950/85 backdrop-blur-md px-3 py-1.5 rounded-full border border-slate-800 text-xs text-slate-200 shadow-lg">
          <Zap className="w-3.5 h-3.5 text-amber-400 animate-pulse" />
          <span className="font-mono text-emerald-400 font-bold">
            {stats.latencyMs ? `${stats.latencyMs} ms` : '<16 ms'}
          </span>
          <span className="text-slate-500">|</span>
          <span className="font-mono text-sky-400 font-bold">
            {stats.fps ? `${stats.fps} FPS` : '30-60 FPS'}
          </span>
        </div>

        {/* Tracking Mode Badge: Upper Body (Desk) vs Full Body */}
        {!isSimulating && hasCameraAccess && (
          <div
            className={`flex items-center gap-1.5 px-2.5 py-1.5 rounded-full border text-[11px] backdrop-blur-md shadow-md ${
              trackingMode === 'full'
                ? 'bg-emerald-500/15 border-emerald-500/40 text-emerald-300'
                : 'bg-sky-500/15 border-sky-500/40 text-sky-300'
            }`}
          >
            <User className="w-3 h-3" />
            <span>{trackingMode === 'full' ? 'Toàn Thân' : 'Nửa Thân Trên (Bàn Làm Việc)'}</span>
          </div>
        )}

        {/* Active Camera Device Badge */}
        {!isSimulating && hasCameraAccess && activeCameraLabel && (
          <div className="hidden md:flex items-center gap-1.5 bg-slate-950/85 backdrop-blur-md px-2.5 py-1.5 rounded-full border border-slate-800 text-[11px] text-slate-300 shadow-md">
            <CheckCircle2 className="w-3 h-3 text-emerald-400" />
            <span className="max-w-[120px] truncate">{activeCameraLabel}</span>
          </div>
        )}
      </div>

      {/* Top Right Controls (Device Selector, Flip Camera & Toggle Mode) */}
      <div className="absolute top-3 right-3 flex items-center gap-1.5 z-20">
        {/* Device Switcher Dropdown (for multiple laptop webcams / external USB cameras) */}
        {!isSimulating && availableDevices.length > 1 && (
          <div className="relative">
            <select
              id="camera-device-select"
              value={selectedDeviceId}
              onChange={handleDeviceChange}
              className="appearance-none bg-slate-950/90 text-slate-200 text-xs py-1.5 pl-2.5 pr-6 rounded-full border border-slate-700 hover:border-slate-600 focus:outline-none focus:ring-1 focus:ring-sky-500 transition-all cursor-pointer backdrop-blur-md"
              title="Chọn thiết bị camera máy tính"
            >
              {availableDevices.map((device, idx) => (
                <option key={device.deviceId || idx} value={device.deviceId}>
                  {device.label || `Camera ${idx + 1}`}
                </option>
              ))}
            </select>
            <ChevronDown className="w-3 h-3 text-slate-400 absolute right-2 top-2 pointer-events-none" />
          </div>
        )}

        {/* Phone Connect Shortcut */}
        {onOpenPhoneConnect && (
          <button
            id="camera-open-phone-modal-btn"
            onClick={onOpenPhoneConnect}
            className="flex items-center gap-1 px-2.5 py-1.5 bg-sky-500/15 hover:bg-sky-500/25 text-sky-300 border border-sky-500/30 rounded-full backdrop-blur-md transition-all text-xs font-semibold shadow-sm"
            title="Kết nối Camera Điện Thoại (Quét QR hoặc Webcam không dây)"
          >
            <Smartphone className="w-3.5 h-3.5 text-sky-400" />
            <span className="hidden sm:inline">Dùng ĐT</span>
          </button>
        )}

        {/* Simulator Toggle */}
        <button
          id="toggle-simulator-btn"
          onClick={onToggleSimulator}
          className={`p-2 rounded-full border backdrop-blur-md transition-all ${
            isSimulating
              ? 'bg-amber-500/20 text-amber-400 border-amber-500/50'
              : 'bg-slate-950/80 text-slate-300 border-slate-800 hover:bg-slate-800'
          }`}
          title={isSimulating ? 'Chuyển sang Camera Thật' : 'Chuyển sang Mô Phỏng Mẫu (Simulator)'}
        >
          <Sparkles className="w-4 h-4" />
        </button>

        {/* Flip Camera Facing Mode */}
        {!isSimulating && (
          <button
            id="flip-camera-btn"
            onClick={toggleFacingMode}
            className="p-2 bg-slate-950/80 text-slate-300 hover:text-white border border-slate-800 rounded-full backdrop-blur-md transition-all hover:bg-slate-800"
            title="Đổi Camera Trước / Sau"
          >
            <FlipHorizontal className="w-4 h-4" />
          </button>
        )}
      </div>

      {/* Target Zone Framing Guide */}
      {!isSimulating && hasCameraAccess && (
        <div className="absolute inset-5 border-2 border-dashed border-sky-500/20 rounded-2xl pointer-events-none flex flex-col justify-between p-2">
          <span className="text-[10px] text-sky-400/60 font-mono tracking-wider self-start">
            [ AI VISION TRACKING ]
          </span>
          <span className="text-[10px] text-slate-300/80 self-center bg-slate-950/80 px-2.5 py-1 rounded-full border border-slate-800 backdrop-blur-md">
            {trackingMode === 'upper'
              ? 'Đang theo dõi nửa thân trên & vung vợt 1:1'
              : 'Đang theo dõi toàn thân & bộ chân Pickleball'}
          </span>
          <span className="text-[10px] text-sky-400/60 font-mono tracking-wider self-end">
            [ REALTIME 3D RIG SYNC ]
          </span>
        </div>
      )}
    </div>
  );
};
