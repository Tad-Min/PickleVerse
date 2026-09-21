/**
 * Drill Session Report & Posture Error Breakdown Modal
 */

import React from 'react';
import { DrillStats, StrokeMode } from '../types';
import { Award, CheckCircle2, Flame, RefreshCw, Trophy, X, Zap } from 'lucide-react';
import confetti from 'canvas-confetti';

interface DrillAnalyticsModalProps {
  isOpen: boolean;
  onClose: () => void;
  stats: DrillStats;
  currentMode: StrokeMode;
  onResetSession: () => void;
}

export const DrillAnalyticsModal: React.FC<DrillAnalyticsModalProps> = ({
  isOpen,
  onClose,
  stats,
  currentMode,
  onResetSession,
}) => {
  if (!isOpen) return null;

  const total = stats.totalSwings || 1;
  const accuracy = Math.round((stats.perfectSwings / total) * 100);

  const triggerCelebration = () => {
    confetti({
      particleCount: 80,
      spread: 70,
      origin: { y: 0.6 },
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-md animate-in fade-in duration-200">
      <div className="relative w-full max-w-lg bg-slate-900 border border-slate-800 rounded-3xl p-6 shadow-2xl space-y-5">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-800 pb-3">
          <div className="flex items-center gap-2.5">
            <div className="p-2 bg-sky-500/20 rounded-xl border border-sky-500/40">
              <Trophy className="w-5 h-5 text-sky-400" />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-100">Báo Cáo Tập Luyện Pickleball</h3>
              <p className="text-xs text-slate-400">Phân tích kỹ thuật & thống kê lỗi thực tế</p>
            </div>
          </div>
          <button
            id="close-report-modal-btn"
            onClick={onClose}
            className="p-1.5 text-slate-400 hover:text-white rounded-lg bg-slate-800/60 hover:bg-slate-800 transition-all"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Big Metric Highlights */}
        <div className="grid grid-cols-3 gap-3">
          <div className="bg-slate-950/60 p-3.5 rounded-2xl border border-slate-800/80 text-center">
            <span className="text-[11px] text-slate-400 block mb-1">Độ Chuẩn Xác</span>
            <span className="text-2xl font-black font-mono text-emerald-400">{accuracy}%</span>
          </div>
          <div className="bg-slate-950/60 p-3.5 rounded-2xl border border-slate-800/80 text-center">
            <span className="text-[11px] text-slate-400 block mb-1">Cú Đánh Chuẩn</span>
            <span className="text-2xl font-black font-mono text-sky-400">
              {stats.perfectSwings}
            </span>
          </div>
          <div className="bg-slate-950/60 p-3.5 rounded-2xl border border-slate-800/80 text-center">
            <span className="text-[11px] text-slate-400 block mb-1">Lỗi Cần Sửa</span>
            <span className="text-2xl font-black font-mono text-rose-400">
              {stats.errorSwings}
            </span>
          </div>
        </div>

        {/* Most Frequent Mistakes Detected */}
        <div className="space-y-2.5">
          <h4 className="text-xs font-bold uppercase tracking-wider text-slate-400">
            Các Lỗi Gặp Phổ Biến Nhất
          </h4>
          <div className="space-y-1.5 max-h-40 overflow-y-auto pr-1">
            {Object.keys(stats.commonErrors).length > 0 ? (
              Object.entries(stats.commonErrors).map(([errorTitle, count]) => (
                <div
                  key={errorTitle}
                  className="flex items-center justify-between p-2.5 bg-slate-950/40 rounded-xl border border-slate-800 text-xs"
                >
                  <span className="text-slate-200 font-medium">{errorTitle}</span>
                  <span className="px-2 py-0.5 bg-rose-500/20 text-rose-300 font-mono font-bold rounded-md">
                    {count} lần
                  </span>
                </div>
              ))
            ) : (
              <div className="p-3 bg-emerald-500/10 border border-emerald-500/20 rounded-xl text-center text-xs text-emerald-300">
                Chưa phát hiện lỗi lặp lại nào! Bạn đang tập rất chuẩn form.
              </div>
            )}
          </div>
        </div>

        {/* Coach Recommendations */}
        <div className="p-3.5 bg-sky-500/10 border border-sky-500/25 rounded-2xl text-xs space-y-1">
          <div className="flex items-center gap-1.5 font-bold text-sky-300">
            <Award className="w-4 h-4" />
            <span>Lời Khuyên Huấn Luyện Viên AI:</span>
          </div>
          <p className="text-slate-300 leading-relaxed">
            {accuracy > 80
              ? 'Form động học của bạn rất mượt mà! Hãy tiếp tục duy trì chuyển động con lắc từ vai và giữ trọng tâm thấp khi di chuyển quanh vạch bếp.'
              : 'Hãy chú ý khuỵu gối sâu hơn (khoảng 135°) và khóa cổ tay khi dink. Tận dụng bóng mờ Pro Form trên mô hình 3D để căn góc cánh tay chuẩn xác.'}
          </p>
        </div>

        {/* Actions */}
        <div className="flex items-center justify-between pt-2">
          <button
            id="reset-session-btn"
            onClick={() => {
              onResetSession();
              onClose();
            }}
            className="flex items-center gap-1.5 px-3.5 py-2 text-xs font-semibold text-slate-400 hover:text-slate-200 bg-slate-800 rounded-xl transition-all"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            Làm mới buổi tập
          </button>
          <div className="flex items-center gap-2">
            <button
              id="celebrate-btn"
              onClick={triggerCelebration}
              className="px-3.5 py-2 bg-amber-500/20 text-amber-300 border border-amber-500/30 hover:bg-amber-500/30 text-xs font-bold rounded-xl transition-all"
            >
              🎉 Ăn Mừng
            </button>
            <button
              id="confirm-close-report-btn"
              onClick={onClose}
              className="px-4 py-2 bg-sky-500 hover:bg-sky-400 text-slate-950 text-xs font-bold rounded-xl shadow-md transition-all"
            >
              Tiếp tục tập luyện
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
