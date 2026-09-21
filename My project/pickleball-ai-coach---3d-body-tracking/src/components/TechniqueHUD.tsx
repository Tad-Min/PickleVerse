/**
 * Real-Time Technique Analysis HUD & Live Biomechanical Metrics
 */

import React from 'react';
import { DrillStats, PoseAnalysisResult, StrokeMode } from '../types';
import {
  AlertCircle,
  AlertTriangle,
  Award,
  CheckCircle2,
  ChevronRight,
  Flame,
  Hand,
  Layers,
  RotateCcw,
  Sparkles,
  Volume2,
  VolumeX,
} from 'lucide-react';
import { audioCoach } from '../services/audioCoach';

interface TechniqueHUDProps {
  mode: StrokeMode;
  onSelectMode: (mode: StrokeMode) => void;
  analysis: PoseAnalysisResult;
  isRightHanded: boolean;
  onToggleHandedness: () => void;
  isSoundMuted: boolean;
  onToggleSound: () => void;
  isVoiceMuted: boolean;
  onToggleVoice: () => void;
  drillStats: DrillStats;
  onOpenReport: () => void;
}

const STROKE_MODES: { id: StrokeMode; label: string; icon: string; desc: string }[] = [
  {
    id: 'FOREHAND_DRIVE',
    label: 'Forehand Drive',
    icon: '🎾',
    desc: 'Vung thuận tay từ dưới lên (Low-to-High), xoay vai & hông.',
  },
  {
    id: 'DINK_KITCHEN',
    label: 'Dink & Vạch Bếp',
    icon: '🎯',
    desc: 'Khuỵu gối sâu, cố định cổ tay, đánh bóng bằng vai tại NVZ.',
  },
  {
    id: 'LEGAL_SERVE',
    label: 'Giao Bóng (Luật 4.A)',
    icon: '⚖️',
    desc: 'Đầu vợt dưới cổ tay, tiếp xúc dưới thắt lưng/rốn.',
  },
  {
    id: 'OVERHEAD_SMASH',
    label: 'Overhead Smash',
    icon: '⚡',
    desc: 'Vươn thẳng tay tối đa ở điểm cao nhất, quất bóng cắm sân.',
  },
  {
    id: 'FOOTWORK_NVZ',
    label: 'Bộ Chân & Vạch Bếp',
    icon: '👟',
    desc: 'Bắt lỗi giẫm vạch Non-Volley Zone & giữ chân rộng cân bằng.',
  },
];

export const TechniqueHUD: React.FC<TechniqueHUDProps> = ({
  mode,
  onSelectMode,
  analysis,
  isRightHanded,
  onToggleHandedness,
  isSoundMuted,
  onToggleSound,
  isVoiceMuted,
  onToggleVoice,
  drillStats,
  onOpenReport,
}) => {
  const getScoreColor = (score: number) => {
    if (score >= 85) return 'text-emerald-400 border-emerald-500/50 bg-emerald-500/10';
    if (score >= 60) return 'text-amber-400 border-amber-500/50 bg-amber-500/10';
    return 'text-rose-400 border-rose-500/50 bg-rose-500/10';
  };

  const getStatusBadge = () => {
    if (analysis.status === 'optimal') {
      return (
        <span className="flex items-center gap-1.5 text-xs font-bold text-emerald-400 bg-emerald-950/60 border border-emerald-500/30 px-3 py-1 rounded-full">
          <CheckCircle2 className="w-3.5 h-3.5" /> CHUẨN FORM PRO
        </span>
      );
    }
    if (analysis.status === 'warning') {
      return (
        <span className="flex items-center gap-1.5 text-xs font-bold text-amber-400 bg-amber-950/60 border border-amber-500/30 px-3 py-1 rounded-full">
          <AlertTriangle className="w-3.5 h-3.5" /> CẦN ĐIỀU CHỈNH
        </span>
      );
    }
    if (analysis.status === 'error') {
      return (
        <span className="flex items-center gap-1.5 text-xs font-bold text-rose-400 bg-rose-950/60 border border-rose-500/30 px-3 py-1 rounded-full animate-pulse">
          <AlertCircle className="w-3.5 h-3.5" /> BẮT LỖI KỸ THUẬT
        </span>
      );
    }
    return (
      <span className="text-xs text-slate-400 bg-slate-800 px-3 py-1 rounded-full">
        ĐANG CHUẨN BỊ
      </span>
    );
  };

  return (
    <div className="flex flex-col gap-4 w-full">
      {/* 1. Technique Selector Carousel / Tab strip */}
      <div className="flex items-center gap-2 overflow-x-auto pb-1 no-scrollbar scroll-smooth">
        {STROKE_MODES.map(item => {
          const isActive = mode === item.id;
          return (
            <button
              key={item.id}
              id={`mode-${item.id.toLowerCase()}-btn`}
              onClick={() => {
                audioCoach.playTone('click');
                onSelectMode(item.id);
              }}
              className={`flex items-center gap-2 px-3.5 py-2 rounded-xl text-xs font-semibold whitespace-nowrap transition-all border ${
                isActive
                  ? 'bg-sky-500 text-slate-950 border-sky-400 shadow-md shadow-sky-500/20 font-bold'
                  : 'bg-slate-900/90 text-slate-300 border-slate-800 hover:border-slate-700 hover:bg-slate-800'
              }`}
            >
              <span className="text-base">{item.icon}</span>
              <span>{item.label}</span>
            </button>
          );
        })}
      </div>

      {/* 2. Main Live Score Card & Audio Control Bar */}
      <div className="bg-slate-900/90 border border-slate-800 rounded-2xl p-4 shadow-xl backdrop-blur-md">
        <div className="flex flex-wrap items-center justify-between gap-3 pb-3 border-b border-slate-800/80">
          {/* Technique Title & Handedness */}
          <div className="flex items-center gap-3">
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-sm font-bold text-slate-100">
                  {STROKE_MODES.find(m => m.id === mode)?.label}
                </h3>
                {getStatusBadge()}
              </div>
              <p className="text-[11px] text-slate-400 mt-0.5 max-w-md">
                {STROKE_MODES.find(m => m.id === mode)?.desc}
              </p>
            </div>
          </div>

          {/* Quick Settings: Handedness & Audio Coach */}
          <div className="flex items-center gap-2">
            {/* Handedness Switch */}
            <button
              id="toggle-handedness-btn"
              onClick={onToggleHandedness}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg border border-slate-700/80 transition-all"
              title="Đổi tay cầm vợt"
            >
              <Hand className="w-3.5 h-3.5 text-sky-400" />
              <span>{isRightHanded ? 'Tay Phải' : 'Tay Trái'}</span>
            </button>

            {/* Sound FX Toggle */}
            <button
              id="toggle-sound-btn"
              onClick={onToggleSound}
              className={`p-1.5 rounded-lg border text-xs transition-all ${
                !isSoundMuted
                  ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/40'
                  : 'bg-slate-800 text-slate-400 border-slate-700'
              }`}
              title={isSoundMuted ? 'Bật âm thanh bíp/chuông bắt lỗi' : 'Tắt âm thanh'}
            >
              {!isSoundMuted ? <Volume2 className="w-4 h-4" /> : <VolumeX className="w-4 h-4" />}
            </button>

            {/* Spoken Voice Coach Toggle */}
            <button
              id="toggle-voice-btn"
              onClick={onToggleVoice}
              className={`px-2.5 py-1.5 rounded-lg border text-xs font-semibold flex items-center gap-1.5 transition-all ${
                !isVoiceMuted
                  ? 'bg-purple-500/10 text-purple-300 border-purple-500/40'
                  : 'bg-slate-800 text-slate-400 border-slate-700'
              }`}
              title="Trợ lý giọng nói AI nhắc lỗi bằng tiếng Việt"
            >
              <span>HLV Giọng Nói</span>
              <span
                className={`w-2 h-2 rounded-full ${
                  !isVoiceMuted ? 'bg-purple-400 animate-pulse' : 'bg-slate-600'
                }`}
              />
            </button>

            {/* Detailed Report Button */}
            <button
              id="open-report-btn"
              onClick={onOpenReport}
              className="flex items-center gap-1 px-3 py-1.5 bg-sky-500/15 hover:bg-sky-500/25 text-sky-300 border border-sky-500/40 rounded-lg text-xs font-semibold transition-all"
            >
              <Award className="w-3.5 h-3.5" />
              <span>Báo Cáo</span>
            </button>
          </div>
        </div>

        {/* 3. Live Score & Error Diagnostic Panel */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-3 pt-3">
          {/* Technique Score Gauge */}
          <div
            className={`flex flex-col items-center justify-center p-3 rounded-xl border ${getScoreColor(
              analysis.score
            )}`}
          >
            <span className="text-[11px] font-medium uppercase tracking-wider text-slate-400">
              Điểm Kỹ Thuật
            </span>
            <div className="flex items-baseline gap-1 my-1">
              <span className="text-3xl font-black font-mono tracking-tight">
                {analysis.score}
              </span>
              <span className="text-xs font-semibold text-slate-400">/100</span>
            </div>
            <div className="w-full bg-slate-950/60 rounded-full h-1.5 overflow-hidden mt-1">
              <div
                className={`h-full transition-all duration-300 ${
                  analysis.score >= 85
                    ? 'bg-emerald-400'
                    : analysis.score >= 60
                    ? 'bg-amber-400'
                    : 'bg-rose-400'
                }`}
                style={{ width: `${analysis.score}%` }}
              />
            </div>
          </div>

          {/* Real-time Kinematic Metric Chips */}
          <div className="md:col-span-3 grid grid-cols-2 sm:grid-cols-3 gap-2">
            {analysis.metrics.map((metric, idx) => (
              <div
                key={idx}
                className="bg-slate-950/60 p-2.5 rounded-xl border border-slate-800/80 flex flex-col justify-between"
              >
                <div className="flex items-center justify-between text-[11px] text-slate-400">
                  <span>{metric.name}</span>
                  <span
                    className={`w-2 h-2 rounded-full ${
                      metric.status === 'optimal'
                        ? 'bg-emerald-400'
                        : metric.status === 'warning'
                        ? 'bg-amber-400'
                        : 'bg-rose-400 animate-ping'
                    }`}
                  />
                </div>
                <div className="mt-1">
                  <span className="text-base font-mono font-bold text-slate-100">
                    {metric.label}
                  </span>
                </div>
                <div className="text-[10px] text-slate-400 mt-0.5">
                  Mục tiêu: {metric.targetRange[0]}-{metric.targetRange[1]}
                  {metric.unit}
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* 4. Active Error / Correction Guidance Callout */}
        {analysis.errors.length > 0 ? (
          <div className="mt-3 p-3 bg-rose-500/10 border border-rose-500/30 rounded-xl space-y-1.5">
            <div className="flex items-center gap-2 text-rose-400 font-bold text-xs">
              <AlertCircle className="w-4 h-4 flex-shrink-0" />
              <span>CẢNH BÁO LỖI TƯ THẾ ({analysis.errors.length}):</span>
            </div>
            <div className="space-y-1 pl-6">
              {analysis.errors.map(err => (
                <div key={err.id} className="text-xs">
                  <span className="font-semibold text-rose-300">{err.title}: </span>
                  <span className="text-slate-300">{err.description}</span>
                </div>
              ))}
            </div>
          </div>
        ) : (
          <div className="mt-3 p-2.5 bg-emerald-500/10 border border-emerald-500/30 rounded-xl flex items-center gap-2 text-emerald-300 text-xs">
            <CheckCircle2 className="w-4 h-4 text-emerald-400 flex-shrink-0" />
            <span>
              <strong>Tuyệt vời!</strong> Tư thế cơ thể và góc vung vợt đang khớp chuẩn với form vận
              động viên Pickleball chuyên nghiệp.
            </span>
          </div>
        )}
      </div>
    </div>
  );
};
