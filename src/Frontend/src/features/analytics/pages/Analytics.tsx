import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { adminApi } from '../../../api/adminApi';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  Cell,
} from 'recharts';
import { Activity, Flame, Info, RotateCw } from 'lucide-react';

export const Analytics: React.FC = () => {
  const {
    data: heatmaps = [],
    isLoading,
    refetch,
    isFetching,
  } = useQuery({
    queryKey: ['admin-ai-heatmap'],
    queryFn: () => adminApi.getAiTechniqueErrorHeatmap(),
  });

  // Calculate color intensity for heatmap cell
  const getCellColor = (count: number) => {
    if (count === 0) return 'bg-slate-900 text-slate-500 border-slate-800';
    if (count < 10) return 'bg-emerald-950/40 text-emerald-400 border-emerald-800/40';
    if (count < 25) return 'bg-amber-950/40 text-amber-400 border-amber-800/40 font-semibold';
    return 'bg-rose-950/60 text-rose-400 border-rose-800/60 font-bold animate-pulse';
  };

  const chartData = heatmaps.map((h) => ({
    name: h.techniqueName,
    errors: h.totalErrorsCount,
  }));

  const COLORS = ['#4F46E5', '#06B6D4', '#10B981', '#F59E0B', '#EF4444'];

  return (
    <div className="space-y-8">
      {/* Title */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold font-display text-foreground tracking-tight">
            Bản đồ nhiệt Lỗi Kỹ thuật AI (Technique-Error Heatmap)
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Dữ liệu tổng hợp từ engine phân tích telemetry: Phát hiện các điểm nghẽn kỹ thuật người chơi mắc lỗi nhiều nhất
          </p>
        </div>

        <button
          onClick={() => refetch()}
          disabled={isFetching}
          className="inline-flex items-center gap-2 px-4 py-2 bg-slate-800 hover:bg-slate-700 border border-border rounded-lg text-xs font-semibold text-foreground transition-colors self-start sm:self-auto"
        >
          <RotateCw className={`w-3.5 h-3.5 ${isFetching ? 'animate-spin text-accent' : ''}`} />
          <span>Làm mới phân tích</span>
        </button>
      </div>

      {/* Info Alert Box */}
      <div className="p-4 rounded-xl bg-indigo-500/10 border border-indigo-500/20 flex items-start gap-3 text-xs text-indigo-300">
        <Info className="w-5 h-5 flex-shrink-0 text-indigo-400 mt-0.5" />
        <div className="space-y-1">
          <div className="font-semibold text-indigo-200">Cách đọc Bản đồ nhiệt AI:</div>
          <div>
            Các ô có màu <strong className="text-rose-400">Đỏ / Cam</strong> cảnh báo kỹ thuật có tỷ lệ sai sót cao (ví dụ: giao bóng quá trễ, dink bóng bổng hoặc bước chân vào Non-Volley Zone). Ban huấn luyện nên tăng cường các bài tập Drills tương ứng trong mục CMS.
          </div>
        </div>
      </div>

      {/* Heatmap Matrix Grid */}
      <div className="bg-card border border-border rounded-xl p-6 shadow-sm space-y-6">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Flame className="w-5 h-5 text-rose-400" />
            <h2 className="text-base font-bold font-display text-foreground">
              Ma trận Phân tích Lỗi theo 5 Kỹ thuật Cốt lõi
            </h2>
          </div>

          {/* Legend */}
          <div className="flex items-center gap-3 text-[11px] text-muted-foreground">
            <div className="flex items-center gap-1.5">
              <span className="w-3 h-3 rounded bg-slate-900 border border-slate-700 inline-block"></span>
              <span>0 lỗi</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-3 h-3 rounded bg-emerald-950/60 border border-emerald-800 inline-block"></span>
              <span>1 - 9 (Thấp)</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-3 h-3 rounded bg-amber-950/60 border border-amber-800 inline-block"></span>
              <span>10 - 24 (Vừa)</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-3 h-3 rounded bg-rose-950/80 border border-rose-800 inline-block"></span>
              <span>$\ge 25$ (Cao)</span>
            </div>
          </div>
        </div>

        {isLoading ? (
          <div className="py-16 text-center text-muted-foreground text-xs">
            <div className="inline-block w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin mb-2"></div>
            <div>Đang tổng hợp dữ liệu telemetry từ AI Engine...</div>
          </div>
        ) : heatmaps.length === 0 ? (
          <div className="py-12 text-center text-muted-foreground text-xs">
            Chưa có đủ mẫu telemetry để kết xuất ma trận nhiệt.
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-5 gap-4">
            {heatmaps.map((tech) => (
              <div
                key={tech.techniqueId}
                className="bg-slate-900/50 border border-border rounded-xl p-4 flex flex-col justify-between"
              >
                <div>
                  <div className="text-xs font-bold text-foreground font-display mb-1 flex items-center justify-between">
                    <span className="truncate" title={tech.techniqueName}>{tech.techniqueName}</span>
                  </div>
                  <div className="text-[11px] text-muted-foreground mb-3">
                    Tổng lỗi ghi nhận: <strong className="text-accent">{tech.totalErrorsCount}</strong>
                  </div>

                  {/* Breakdown items */}
                  <div className="space-y-2">
                    {tech.errorBreakdown.length === 0 ? (
                      <div className="text-[11px] text-muted-foreground italic">Không có lỗi chi tiết</div>
                    ) : (
                      tech.errorBreakdown.map((err, idx) => (
                        <div
                          key={idx}
                          className={`p-2 rounded-lg border text-xs flex items-center justify-between ${getCellColor(
                            err.count
                          )}`}
                          title={`${tech.techniqueName} - ${err.errorType}: ${err.count} lần`}
                        >
                          <span className="text-[11px] truncate max-w-[120px]">{err.errorType}</span>
                          <span className="font-mono text-xs">{err.count}</span>
                        </div>
                      ))
                    )}
                  </div>
                </div>

                <div className="mt-4 pt-3 border-t border-border/50 text-right">
                  <span className="text-[10px] text-muted-foreground">Technique #{tech.techniqueId}</span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Aggregate Errors Chart */}
      <div className="bg-card border border-border rounded-xl p-6 shadow-sm space-y-4">
        <div className="flex items-center gap-2">
          <Activity className="w-5 h-5 text-accent" />
          <h2 className="text-base font-bold font-display text-foreground">
            Phân bổ Tổng Số Lỗi theo Kỹ thuật
          </h2>
        </div>

        <div className="h-72 w-full pt-4">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={chartData} margin={{ top: 10, right: 20, left: 0, bottom: 20 }}>
              <XAxis dataKey="name" stroke="#94A3B8" fontSize={12} tickLine={false} />
              <YAxis stroke="#94A3B8" fontSize={12} tickLine={false} />
              <Tooltip
                contentStyle={{ backgroundColor: '#1E293B', borderColor: '#334155', borderRadius: '0.5rem', color: '#F8FAFC' }}
                cursor={{ fill: 'rgba(255, 255, 255, 0.05)' }}
              />
              <Bar dataKey="errors" radius={[6, 6, 0, 0]}>
                {chartData.map((_entry, index) => (
                  <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                ))}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  );
};
