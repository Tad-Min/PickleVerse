/**
 * Pickleball AI Coach - 3D Body Tracking & Real-Time Technique Analysis
 */

import React, { useEffect, useRef, useState } from 'react';
import { DrillStats, Landmark3D, PoseAnalysisResult, PoseTrackerStats, StrokeMode } from './types';
import { analyzePickleballPose } from './services/kinematics';
import { generateProPose } from './services/motionSimulator';
import { audioCoach } from './services/audioCoach';
import { PickleballScene3D } from './components/PickleballScene3D';
import { ErrorBoundary } from './components/ErrorBoundary';
import { CameraTrackerView } from './components/CameraTrackerView';
import { TechniqueHUD } from './components/TechniqueHUD';
import { DrillAnalyticsModal } from './components/DrillAnalyticsModal';
import { PhoneConnectModal } from './components/PhoneConnectModal';
import {
  Activity,
  Award,
  CircleHelp,
  ExternalLink,
  Gauge,
  Info,
  Maximize2,
  Smartphone,
  Sparkles,
  Trophy,
  Volume2,
  VolumeX,
  Zap,
} from 'lucide-react';

export default function App() {
  // State variables
  const [mode, setMode] = useState<StrokeMode>('FOREHAND_DRIVE');
  const [isRightHanded, setIsRightHanded] = useState<boolean>(true);
  const [showGhostPro, setShowGhostPro] = useState<boolean>(true);
  const [isSimulating, setIsSimulating] = useState<boolean>(false);
  const [isSoundMuted, setIsSoundMuted] = useState<boolean>(false);
  const [isVoiceMuted, setIsVoiceMuted] = useState<boolean>(false);
  const [isReportOpen, setIsReportOpen] = useState<boolean>(false);
  const [showGuide, setShowGuide] = useState<boolean>(false);
  const [isPhoneModalOpen, setIsPhoneModalOpen] = useState<boolean>(false);
  const [targetCameraId, setTargetCameraId] = useState<string>('');
  const [cameraDevices, setCameraDevices] = useState<MediaDeviceInfo[]>([]);

  // Pose & Analysis State
  const [landmarks, setLandmarks] = useState<Landmark3D[]>([]);
  const [worldLandmarks, setWorldLandmarks] = useState<Landmark3D[]>([]);
  const [analysis, setAnalysis] = useState<PoseAnalysisResult>({
    score: 95,
    status: 'optimal',
    statusMessage: 'Sẵn sàng phân tích tư thế Pickleball...',
    metrics: [],
    errors: [],
    kneeFlexionAngle: 135,
    elbowAngle: 140,
    wristAngle: 165,
    torsoRotation: 25,
    swingSpeedEstimate: 0,
    isKitchenViolated: false,
    isLegalServe: true,
  });

  // Drill statistics
  const [drillStats, setDrillStats] = useState<DrillStats>({
    totalSwings: 0,
    perfectSwings: 0,
    warningSwings: 0,
    errorSwings: 0,
    commonErrors: {},
    sessionDurationSec: 0,
  });

  // Velocity tracking & audio throttle refs
  const prevWristYRef = useRef<number | null>(null);
  const lastAudioFeedbackTimeRef = useRef<number>(0);
  const lastEvaluatedStatusRef = useRef<'optimal' | 'warning' | 'error' | 'idle'>('idle');
  const simTimerRef = useRef<number>(0);

  // Sound toggles
  const handleToggleSound = () => {
    const next = !isSoundMuted;
    setIsSoundMuted(next);
    audioCoach.setMuted(next);
    if (!next) {
      audioCoach.playTone('click');
    }
  };

  const handleToggleVoice = () => {
    const next = !isVoiceMuted;
    setIsVoiceMuted(next);
    audioCoach.setVoiceMuted(next);
    if (!next) {
      audioCoach.speakTip('Huấn luyện viên giọng nói đã sẵn sàng!', true);
    }
  };

  // Process and analyze pose landmarks
  const handlePoseUpdate = (
    currentLandmarks: Landmark3D[],
    stats?: PoseTrackerStats,
    currentWorldLandmarks?: Landmark3D[]
  ) => {
    if (!currentLandmarks || currentLandmarks.length < 33) return;

    setLandmarks(currentLandmarks);
    if (currentWorldLandmarks && currentWorldLandmarks.length >= 33) {
      setWorldLandmarks(currentWorldLandmarks);
    }

    // Calculate vertical wrist velocity
    const wristIndex = isRightHanded ? 16 : 15;
    const currentWristY = currentLandmarks[wristIndex]?.y || 0.5;
    let wristVelocityY = 0;
    if (prevWristYRef.current !== null) {
      wristVelocityY = currentWristY - prevWristYRef.current;
    }
    prevWristYRef.current = currentWristY;

    // Run biomechanical kinematics evaluation
    const result = analyzePickleballPose(currentLandmarks, mode, isRightHanded, wristVelocityY);
    setAnalysis(result);

    // Trigger Audio & Voice feedback with throttle
    const now = Date.now();
    if (now - lastAudioFeedbackTimeRef.current > 1400) {
      if (result.status === 'error' && result.errors.length > 0) {
        audioCoach.playTone('error');
        // Speak the first critical error in Vietnamese
        const primaryError = result.errors[0];
        if (primaryError.joint === 'knee') {
          audioCoach.speakTip('Khuỵu gối thấp xuống!');
        } else if (primaryError.joint === 'wrist') {
          audioCoach.speakTip(mode === 'LEGAL_SERVE' ? 'Hạ đầu vợt dưới cổ tay!' : 'Khóa chặt cổ tay!');
        } else if (primaryError.joint === 'footwork') {
          audioCoach.speakTip('Cẩn thận phạm vạch bếp!');
        } else if (primaryError.joint === 'paddle') {
          audioCoach.speakTip('Vung từ dưới lên trên!');
        } else {
          audioCoach.speakTip(primaryError.title);
        }
        lastAudioFeedbackTimeRef.current = now;

        // Record error in drill stats
        setDrillStats(prev => {
          const common = { ...prev.commonErrors };
          const errName = primaryError.title;
          common[errName] = (common[errName] || 0) + 1;
          return {
            ...prev,
            totalSwings: prev.totalSwings + 1,
            errorSwings: prev.errorSwings + 1,
            commonErrors: common,
          };
        });
      } else if (result.status === 'optimal' && lastEvaluatedStatusRef.current !== 'optimal') {
        audioCoach.playTone('success');
        audioCoach.speakTip('Form chuẩn rất đẹp!');
        lastAudioFeedbackTimeRef.current = now;

        setDrillStats(prev => ({
          ...prev,
          totalSwings: prev.totalSwings + 1,
          perfectSwings: prev.perfectSwings + 1,
        }));
      }

      lastEvaluatedStatusRef.current = result.status;
    }
  };

  // Run Simulator loop if active
  useEffect(() => {
    if (!isSimulating) return;

    let reqId: number;
    let t = 0;
    const loop = () => {
      t += 0.032;
      simTimerRef.current = t;
      const simLandmarks = generateProPose(mode, t, isRightHanded);
      handlePoseUpdate(simLandmarks);
      reqId = requestAnimationFrame(loop);
    };
    reqId = requestAnimationFrame(loop);

    return () => {
      cancelAnimationFrame(reqId);
    };
  }, [isSimulating, mode, isRightHanded]);

  // Reset drill statistics
  const handleResetSession = () => {
    setDrillStats({
      totalSwings: 0,
      perfectSwings: 0,
      warningSwings: 0,
      errorSwings: 0,
      commonErrors: {},
      sessionDurationSec: 0,
    });
    audioCoach.playTone('click');
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-['Plus_Jakarta_Sans',sans-serif]">
      {/* 1. Sporty Header */}
      <header className="sticky top-0 z-40 bg-slate-950/85 backdrop-blur-md border-b border-slate-800/80 px-4 py-2.5">
        <div className="max-w-7xl mx-auto flex items-center justify-between gap-4">
          {/* Logo and Brand */}
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-gradient-to-tr from-sky-500 to-emerald-400 p-0.5 shadow-md shadow-sky-500/20 flex items-center justify-center">
              <div className="w-full h-full bg-slate-950 rounded-[10px] flex items-center justify-center">
                <span className="text-lg">🏓</span>
              </div>
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h1 className="text-sm sm:text-base font-extrabold tracking-tight text-white">
                  PICKLEBALL <span className="text-sky-400">AI COACH</span>
                </h1>
                <span className="hidden sm:inline-block px-2 py-0.5 text-[10px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 rounded-full font-mono">
                  &lt;16ms LATENCY
                </span>
              </div>
              <p className="text-[11px] text-slate-400 hidden sm:block">
                Body Tracking 3D & Sửa Lỗi Vung Vợt Thời Gian Thực
              </p>
            </div>
          </div>

          {/* Quick Action Badges */}
          <div className="flex items-center gap-2">
            {/* Simulation / Camera switch indicator */}
            <button
              id="header-toggle-sim-btn"
              onClick={() => setIsSimulating(prev => !prev)}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-semibold border transition-all ${
                isSimulating
                  ? 'bg-amber-500/20 text-amber-300 border-amber-500/50 shadow-sm'
                  : 'bg-slate-900 text-slate-300 border-slate-800 hover:bg-slate-800'
              }`}
            >
              <Sparkles className="w-3.5 h-3.5 text-amber-400" />
              <span>{isSimulating ? 'Mô Phỏng 3D' : 'Camera Thật'}</span>
            </button>

            {/* Sound FX Fast Button */}
            <button
              id="header-sound-btn"
              onClick={handleToggleSound}
              className={`p-2 rounded-xl border text-xs transition-all ${
                !isSoundMuted
                  ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30'
                  : 'bg-slate-900 text-slate-400 border-slate-800'
              }`}
              title={isSoundMuted ? 'Bật âm thanh' : 'Tắt âm thanh'}
            >
              {!isSoundMuted ? <Volume2 className="w-4 h-4" /> : <VolumeX className="w-4 h-4" />}
            </button>

            {/* Open in new tab button (ensures full camera device permissions) */}
            <button
              id="header-open-tab-btn"
              onClick={() => window.open(window.location.href, '_blank')}
              className="p-2 rounded-xl bg-slate-900 text-slate-300 hover:text-white border border-slate-800 hover:bg-slate-800 transition-all hidden sm:flex"
              title="Mở trong tab mới (toàn quyền truy cập Camera & không bị giới hạn iframe)"
            >
              <ExternalLink className="w-4 h-4" />
            </button>

            {/* Connect Phone Camera Button */}
            <button
              id="header-phone-connect-btn"
              onClick={() => setIsPhoneModalOpen(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-sky-500/15 hover:bg-sky-500/25 border border-sky-500/30 text-sky-400 hover:text-sky-300 font-semibold text-xs transition-all shadow-sm"
              title="Kết nối Camera Điện Thoại (Quét QR hoặc dùng làm Webcam không dây)"
            >
              <Smartphone className="w-4 h-4" />
              <span className="hidden sm:inline">Dùng Camera ĐT</span>
            </button>

            {/* Quick Guide modal toggle */}
            <button
              id="header-guide-btn"
              onClick={() => setShowGuide(prev => !prev)}
              className="p-2 rounded-xl bg-slate-900 text-slate-300 hover:text-white border border-slate-800 hover:bg-slate-800 transition-all"
              title="Hướng dẫn sử dụng"
            >
              <CircleHelp className="w-4 h-4" />
            </button>
          </div>
        </div>
      </header>

      {/* 2. Interactive Quick Guide Drawer (collapsible) */}
      {showGuide && (
        <div className="bg-slate-900 border-b border-slate-800 px-4 py-3 animate-in slide-in-from-top-2 duration-200">
          <div className="max-w-7xl mx-auto flex flex-col md:flex-row items-start md:items-center justify-between gap-3 text-xs text-slate-300">
            <div className="space-y-1">
              <div className="flex items-center gap-1.5 font-bold text-sky-400">
                <Info className="w-4 h-4" />
                <span>Cách Hoạt Động & Yêu Cầu Kỹ Thuật Độ Trễ Thấp Nhất:</span>
              </div>
              <ul className="list-disc list-inside text-slate-300 space-y-0.5 pl-1">
                <li>
                  <strong>Body Tracking Gia Tốc GPU:</strong> Thuật toán MediaPipe BlazePose xử lý
                  trực tiếp trên thiết bị (Inference ~12-16ms, 30-60 FPS), không gửi video lên mạng.
                </li>
                <li>
                  <strong>Mô Hình 3D Thời Gian Thực:</strong> Khung xương 3D và vợt pickleball cử
                  động theo khớp cơ thể bạn; bóng mờ màu vàng (Ghost Pro) hiển thị góc vung chuẩn
                  mẫu.
                </li>
                <li>
                  <strong>Phản Hồi Âm Thanh Zero-Latency:</strong> Âm thanh còi và chimes phát qua Web
                  Audio API ngay khi phát hiện góc vung lỗi hoặc tiếp xúc chuẩn xác.
                </li>
              </ul>
            </div>
            <button
              id="close-guide-btn"
              onClick={() => setShowGuide(false)}
              className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded-lg font-semibold self-end md:self-auto"
            >
              Đã hiểu
            </button>
          </div>
        </div>
      )}

      {/* 3. Main Split Viewport */}
      <main className="flex-1 max-w-7xl w-full mx-auto p-3 sm:p-4 space-y-4">
        {/* Dual 3D & Camera View Grid */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-4 h-[440px] sm:h-[480px]">
          {/* Primary: 3D Articulated Athlete & Pickleball Court (7 cols on desktop) */}
          <div className="lg:col-span-7 h-full">
            <ErrorBoundary
              fallbackTitle="Không Gian Sân Đấu 3D Đang Khởi Động Lại"
              fallbackMessage="Trình duyệt vừa giải phóng ngữ cảnh WebGL GPU. Camera 2D Tracker và Hướng Dẫn Kỹ Thuật vẫn hoạt động bình thường."
            >
              <PickleballScene3D
                landmarks={landmarks}
                worldLandmarks={worldLandmarks}
                analysis={analysis}
                mode={mode}
                isRightHanded={isRightHanded}
                showGhostPro={showGhostPro}
                onToggleGhostPro={() => {
                  setShowGhostPro(prev => !prev);
                  audioCoach.playTone('click');
                }}
              />
            </ErrorBoundary>
          </div>

          {/* Secondary: Real-time Camera Stream with 2D Skeleton & Low-latency Telemetry (5 cols) */}
          <div className="lg:col-span-5 h-full">
            <CameraTrackerView
              onPoseUpdate={handlePoseUpdate}
              analysis={analysis}
              isSimulating={isSimulating}
              isRightHanded={isRightHanded}
              onOpenPhoneConnect={() => setIsPhoneModalOpen(true)}
              targetDeviceId={targetCameraId}
              onDevicesFound={setCameraDevices}
              onToggleSimulator={() => {
                setIsSimulating(prev => !prev);
                audioCoach.playTone('click');
              }}
            />
          </div>
        </div>

        {/* 4. Real-Time Technique Analysis HUD */}
        <TechniqueHUD
          mode={mode}
          onSelectMode={newMode => {
            setMode(newMode);
            if (isSimulating) {
              const simLandmarks = generateProPose(newMode, 0, isRightHanded);
              handlePoseUpdate(simLandmarks);
            }
          }}
          analysis={analysis}
          isRightHanded={isRightHanded}
          onToggleHandedness={() => {
            setIsRightHanded(prev => !prev);
            audioCoach.playTone('click');
          }}
          isSoundMuted={isSoundMuted}
          onToggleSound={handleToggleSound}
          isVoiceMuted={isVoiceMuted}
          onToggleVoice={handleToggleVoice}
          drillStats={drillStats}
          onOpenReport={() => setIsReportOpen(true)}
        />
      </main>

      {/* 5. Session Analytics Modal */}
      <DrillAnalyticsModal
        isOpen={isReportOpen}
        onClose={() => setIsReportOpen(false)}
        stats={drillStats}
        currentMode={mode}
        onResetSession={handleResetSession}
      />

      {/* 6. Phone Camera Connect Modal (QR Code & Wireless Webcam) */}
      <PhoneConnectModal
        isOpen={isPhoneModalOpen}
        onClose={() => setIsPhoneModalOpen(false)}
        availableDevices={cameraDevices}
        onSelectDevice={devId => {
          setTargetCameraId(devId);
        }}
      />
    </div>
  );
}
