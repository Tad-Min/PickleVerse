import React, { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { adminApi } from '../api/adminApi';
import { MatchItemDto } from '../types/api';
import {
  RotateCw,
  Trophy,
  Users,
  Calendar,
  X,
  Radio,
  Eye,
} from 'lucide-react';

export const Matches: React.FC = () => {
  const [autoPolling, setAutoPolling] = useState<boolean>(true);
  const [pollingInterval, setPollingInterval] = useState<number>(15000); // 15 seconds
  const [selectedMatch, setSelectedMatch] = useState<MatchItemDto | null>(null);

  // Auto-pause polling when tab is inactive
  useEffect(() => {
    const handleVisibilityChange = () => {
      if (document.hidden) {
        setAutoPolling(false);
      } else {
        setAutoPolling(true);
      }
    };
    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
  }, []);

  const {
    data: matches = [],
    isLoading,
    isFetching,
    refetch,
    dataUpdatedAt,
  } = useQuery({
    queryKey: ['admin-recent-matches'],
    queryFn: () => adminApi.getRecentMatches(50),
    refetchInterval: autoPolling ? pollingInterval : false,
  });

  return (
    <div className="space-y-6">
      {/* Title & Polling Bar */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold font-display text-foreground tracking-tight">Giám sát Trận đấu</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Theo dõi thời gian thực 50 trận đấu gần nhất trên Dedicated Game Servers
          </p>
        </div>

        {/* Polling & Refresh Toolbar */}
        <div className="flex items-center gap-3 bg-card border border-border p-2 rounded-xl text-xs">
          {/* Polling Toggle */}
          <label className="flex items-center gap-2 cursor-pointer select-none px-2">
            <span className="relative flex h-2.5 w-2.5">
              {autoPolling && (
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
              )}
              <span
                className={`relative inline-flex rounded-full h-2.5 w-2.5 ${
                  autoPolling ? 'bg-emerald-500' : 'bg-slate-600'
                }`}
              ></span>
            </span>
            <span className="font-semibold text-foreground">Auto-polling</span>
            <input
              type="checkbox"
              checked={autoPolling}
              onChange={(e) => setAutoPolling(e.target.checked)}
              className="sr-only"
            />
          </label>

          <div className="h-4 w-[1px] bg-border"></div>

          {/* Interval Selector */}
          <select
            value={pollingInterval}
            onChange={(e) => setPollingInterval(Number(e.target.value))}
            disabled={!autoPolling}
            className="bg-slate-900 border border-border rounded px-2 py-1 text-xs text-foreground disabled:opacity-50"
          >
            <option value={10000}>Mỗi 10s</option>
            <option value={15000}>Mỗi 15s</option>
            <option value={30000}>Mỗi 30s</option>
          </select>

          {/* Manual Refresh Button */}
          <button
            onClick={() => refetch()}
            disabled={isFetching}
            title="Làm mới thủ công"
            className="p-1.5 bg-slate-900 hover:bg-slate-800 text-foreground border border-border rounded transition-colors disabled:opacity-50"
          >
            <RotateCw className={`w-3.5 h-3.5 ${isFetching ? 'animate-spin text-accent' : ''}`} />
          </button>
        </div>
      </div>

      {/* Status Bar */}
      <div className="flex items-center justify-between text-xs text-muted-foreground">
        <div className="flex items-center gap-2">
          <Radio className="w-3.5 h-3.5 text-accent animate-pulse" />
          <span>Tổng số trận hiển thị: <strong className="text-foreground">{matches.length}</strong></span>
        </div>
        {dataUpdatedAt > 0 && (
          <div>Cập nhật lúc: {new Date(dataUpdatedAt).toLocaleTimeString('vi-VN')}</div>
        )}
      </div>

      {/* Matches Table */}
      <div className="bg-card border border-border rounded-xl shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-900/60 border-b border-border text-muted-foreground uppercase tracking-wider text-[11px] font-semibold">
              <tr>
                <th className="py-3.5 px-4">Mã trận / Thể loại</th>
                <th className="py-3.5 px-4">Sân thi đấu</th>
                <th className="py-3.5 px-4">Trạng thái</th>
                <th className="py-3.5 px-4">Thời gian bắt đầu</th>
                <th className="py-3.5 px-4">Người thắng</th>
                <th className="py-3.5 px-4">Kết quả tóm tắt</th>
                <th className="py-3.5 px-4 text-right">Chi tiết</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {isLoading ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-muted-foreground">
                    <div className="inline-block w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin mb-2"></div>
                    <div>Đang cập nhật phiên thi đấu từ game servers...</div>
                  </td>
                </tr>
              ) : matches.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-muted-foreground">
                    Hiện chưa có trận đấu nào trong danh sách gần đây.
                  </td>
                </tr>
              ) : (
                matches.map((m) => {
                  const isLive = m.status === 'IN_PROGRESS';
                  const isCompleted = m.status === 'COMPLETED';

                  return (
                    <tr key={m.matchId} className="hover:bg-slate-800/50 transition-colors">
                      <td className="py-3 px-4">
                        <div className="font-mono text-accent text-[11px]">
                          {m.matchId ? m.matchId.substring(0, 8) + '...' : 'Unknown'}
                        </div>
                        <div className="inline-flex items-center gap-1 text-[11px] font-semibold text-foreground mt-0.5">
                          <Users className="w-3 h-3 text-muted-foreground" />
                          <span>{m.mode || '1v1 Singles'}</span>
                        </div>
                      </td>

                      <td className="py-3 px-4 font-medium text-foreground">
                        {m.courtName || 'Sân tiêu chuẩn HCMC'}
                      </td>

                      <td className="py-3 px-4">
                        <span
                          className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium border ${
                            isLive
                              ? 'bg-amber-500/10 text-amber-400 border-amber-500/20 animate-pulse'
                              : isCompleted
                              ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20'
                              : 'bg-slate-700/30 text-slate-400 border-slate-700'
                          }`}
                        >
                          {m.status || 'COMPLETED'}
                        </span>
                      </td>

                      <td className="py-3 px-4 text-muted-foreground text-[11px]">
                        <div className="flex items-center gap-1">
                          <Calendar className="w-3 h-3" />
                          <span>{m.startTime ? new Date(m.startTime).toLocaleTimeString('vi-VN') : 'Vừa xong'}</span>
                        </div>
                      </td>

                      <td className="py-3 px-4 font-semibold text-foreground">
                        {m.winner ? (
                          <div className="flex items-center gap-1.5 text-amber-400">
                            <Trophy className="w-3.5 h-3.5" />
                            <span>{m.winner}</span>
                          </div>
                        ) : (
                          <span className="text-muted-foreground font-normal">Chưa kết thúc</span>
                        )}
                      </td>

                      <td className="py-3 px-4 text-muted-foreground font-mono text-[11px]">
                        {m.scoreSummary || '11 - 8, 11 - 9'}
                      </td>

                      <td className="py-3 px-4 text-right">
                        <button
                          onClick={() => setSelectedMatch(m)}
                          className="p-1.5 rounded hover:bg-slate-700 text-muted-foreground hover:text-foreground transition-colors"
                          title="Xem biên bản trận đấu"
                        >
                          <Eye className="w-3.5 h-3.5" />
                        </button>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* MATCH DETAIL MODAL */}
      {selectedMatch && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="w-full max-w-lg bg-card border border-border rounded-xl p-6 shadow-2xl relative">
            <div className="flex items-center justify-between pb-4 border-b border-border mb-4">
              <div className="flex items-center gap-2">
                <Trophy className="w-5 h-5 text-accent" />
                <h3 className="text-base font-bold text-foreground font-display">Biên bản Trận đấu</h3>
              </div>
              <button onClick={() => setSelectedMatch(null)} className="text-muted-foreground hover:text-foreground">
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="space-y-3 text-xs">
              <div className="flex justify-between py-1.5 border-b border-border/50">
                <span className="text-muted-foreground">Match ID:</span>
                <span className="font-mono text-accent font-semibold">{selectedMatch.matchId}</span>
              </div>
              <div className="flex justify-between py-1.5 border-b border-border/50">
                <span className="text-muted-foreground">Thể thức:</span>
                <span className="font-semibold text-foreground">{selectedMatch.mode || '1v1 Singles'}</span>
              </div>
              <div className="flex justify-between py-1.5 border-b border-border/50">
                <span className="text-muted-foreground">Sân thi đấu:</span>
                <span className="text-foreground">{selectedMatch.courtName || 'Standard Court'}</span>
              </div>
              <div className="flex justify-between py-1.5 border-b border-border/50">
                <span className="text-muted-foreground">Trạng thái:</span>
                <span className="font-semibold text-emerald-400">{selectedMatch.status || 'COMPLETED'}</span>
              </div>
              <div className="flex justify-between py-1.5 border-b border-border/50">
                <span className="text-muted-foreground">Người chiến thắng:</span>
                <span className="font-bold text-amber-400">{selectedMatch.winner || 'Đang đấu'}</span>
              </div>
              <div className="flex justify-between py-1.5">
                <span className="text-muted-foreground">Tỷ số chi tiết:</span>
                <span className="font-mono font-bold text-foreground">{selectedMatch.scoreSummary || 'Set 1: 11-7 | Set 2: 11-9'}</span>
              </div>
            </div>

            <div className="mt-6 pt-4 border-t border-border flex justify-end">
              <button
                onClick={() => setSelectedMatch(null)}
                className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-foreground text-xs font-semibold rounded-lg"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
