import { useState } from 'react';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { Activity, X } from 'lucide-react';
import { PageHeader } from '../../../shared/ui/PageHeader';
import { apiClient } from '../../../api/client';
import { adminApi } from '../../../api/adminApi';
import { LiveMatch, StopOperation } from '../../../types/api';
import { matchStatusLabel } from '../../../types/status';
import {
  badgeBase, btnGhost, btnPrimary, btnTab, card, fieldLabel, inputClass, modalCard, modalOverlay,
  notice, sectionTitle, tableCard, tableHead, tableText, th, td, rowHover, tone, toolbar,
} from '../../../shared/ui/adminStyles';

const stamp = (value?: string | null) => (value ? new Date(value).toLocaleString('vi-VN') : 'Chưa có');
const message = (error: unknown) =>
  (error as { response?: { data?: { message?: string } } })?.response?.data?.message || (error as Error)?.message;

export function Matches() {
  const [tab, setTab] = useState<'live' | 'history'>('live');
  const [poll, setPoll] = useState(true);
  const [selected, setSelected] = useState<LiveMatch | null>(null);
  const [reason, setReason] = useState('');
  const [operation, setOperation] = useState<StopOperation | null>(null);
  const [notice_, setNotice] = useState('');
  const client = useQueryClient();

  const live = useQuery({
    queryKey: ['admin-live-matches'],
    queryFn: async () => (await apiClient.get<LiveMatch[]>('/api/Admin/matches/live')).data,
    refetchInterval: poll ? 5000 : false,
  });
  const history = useQuery({
    queryKey: ['admin-recent-matches'],
    queryFn: () => adminApi.getRecentMatches(50),
    enabled: tab === 'history',
  });
  const acknowledgement = useQuery({
    queryKey: ['admin-stop-operation', operation?.matchId],
    queryFn: async () => (await apiClient.get<StopOperation>(`/api/Admin/matches/${operation!.matchId}/stop`)).data,
    enabled: !!operation,
    refetchInterval: operation && !operation.acknowledgedAt ? 3000 : false,
  });
  const stop = useMutation({
    mutationFn: async () =>
      (await apiClient.post<StopOperation>(`/api/Admin/matches/${selected!.matchId}/stop`, { reason })).data,
    onSuccess: (data) => {
      setOperation(data);
      setSelected(null);
      setReason('');
      client.invalidateQueries({ queryKey: ['admin-live-matches'] });
    },
    onError: (e) => setNotice(message(e) || 'Không dừng được trận.'),
  });

  const observed = acknowledgement.data || operation;
  const liveCount = live.data?.length || 0;

  return (
    <div className="space-y-6">
      {/* Title */}
      <PageHeader
        icon={Activity}
        title="Giám sát trận đấu"
        subtitle="Điểm và kết nối được cập nhật từ tín hiệu của máy chủ trận đấu. Lịch sử được hiển thị riêng."
      />

      {/* Toolbar */}
      <div className={toolbar}>
        <div className="flex flex-wrap items-center gap-2">
          <button className={btnTab(tab === 'live')} onClick={() => setTab('live')}>
            Đang hoạt động ({liveCount})
          </button>
          <button className={btnTab(tab === 'history')} onClick={() => setTab('history')}>
            Lịch sử
          </button>
        </div>
        <label className="flex items-center gap-2 text-xs text-foreground">
          <input type="checkbox" checked={poll} onChange={(e) => setPoll(e.target.checked)} />
          Cập nhật mỗi 5 giây
        </label>
      </div>

      {notice_ && (
        <div role="alert" className={notice('error')}>
          <span>{notice_}</span>
          <button onClick={() => setNotice('')} className="p-1 hover:opacity-80" aria-label="Đóng thông báo">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}
      {(live.error || history.error) && (
        <p role="alert" className="text-xs text-rose-600">
          {message(live.error || history.error)}
        </p>
      )}

      {/* Stop operation status */}
      {observed && (
        <div className={card}>
          <div className="flex flex-wrap items-center gap-2 mb-2">
            <span className={`${badgeBase} ${observed.acknowledgedAt ? tone.emerald : tone.amber}`}>
              {observed.acknowledgedAt ? 'Đã xác nhận' : 'Chờ xác nhận'}
            </span>
            <span className="font-mono text-[11px] text-muted-foreground">Lệnh {observed.operationId}</span>
          </div>
          <p className="text-xs text-foreground">Trận: <span className="font-mono">{observed.matchId}</span></p>
          <p className="text-xs text-foreground mt-1">{observed.reason}</p>
          <p className="text-xs text-muted-foreground mt-2">
            {observed.acknowledgedAt
              ? `Máy chủ riêng đã xác nhận: ${stamp(observed.acknowledgedAt)}`
              : 'Đã lưu bền; đang chờ máy chủ trận đấu xác nhận. Kết quả đến muộn không được tính Elo/thưởng.'}
          </p>
        </div>
      )}

      {tab === 'live' ? (
        <div className="space-y-4">
          {(live.data || []).map((m) => (
            <article key={m.matchId} className={`${card} space-y-4`}>
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <p className="font-mono text-xs text-muted-foreground">{m.matchId}</p>
                  <p className="text-xs text-muted-foreground mt-1">
                    Bắt đầu: {stamp(m.startedAt)} · Heartbeat: {stamp(m.heartbeatAt)}
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  <span className={`${badgeBase} ${tone.blue}`}>{matchStatusLabel[m.status] ?? m.status}</span>
                  <span className={`${badgeBase} ${tone.neutral}`}>
                    {m.authorityMode === 'dedicated' ? 'Máy chủ riêng' : 'Máy chủ chung'}
                  </span>
                  {m.heartbeatStale && <span className={`${badgeBase} ${tone.rose}`}>Quá hạn heartbeat</span>}
                </div>
              </div>

              {m.live ? (
                <div className="rounded-lg border border-border bg-background p-3">
                  <p className="text-lg font-bold font-display text-foreground">
                    {m.live.scoreA} : {m.live.scoreB}
                    <span className="ml-3 text-xs font-medium text-muted-foreground">
                      Ván {m.live.gamesA} : {m.live.gamesB}
                    </span>
                  </p>
                  <p className="text-xs text-muted-foreground mt-1">
                    {m.live.paused ? 'Đang tạm dừng' : m.live.started ? 'Đang thi đấu' : 'Chờ vào sân'} · Cập nhật:{' '}
                    {stamp(m.live.observedAt)}
                  </p>
                </div>
              ) : (
                <p className="text-xs text-muted-foreground">Chưa có trạng thái mô phỏng từ máy chủ trận đấu.</p>
              )}

              <div className="grid gap-4 md:grid-cols-2">
                {[0, 1].map((team) => (
                  <div key={team} className="rounded-lg border border-border p-3">
                    <p className="text-xs font-semibold text-foreground mb-2">Đội {team === 0 ? 'A' : 'B'}</p>
                    <div className="space-y-1.5">
                      {m.roster.members
                        .filter((p) => p.team === team)
                        .map((p) => {
                          const state = m.live?.players.find((x) => x.id === p.id);
                          return (
                            <div key={p.id} className="flex flex-wrap items-center justify-between gap-2 text-xs">
                              <span className="text-foreground">
                                {p.name}
                                {p.bot ? ' (Bot)' : ''}
                              </span>
                              <span
                                className={
                                  !state ? 'text-muted-foreground' : state.connected ? 'text-emerald-600' : 'text-rose-600'
                                }
                              >
                                {state
                                  ? state.connected
                                    ? 'Đã kết nối'
                                    : `Mất kết nối${state.reconnectUntil ? ` · Chờ đến ${stamp(state.reconnectUntil)}` : ''}`
                                  : 'Chưa có dữ liệu'}
                              </span>
                            </div>
                          );
                        })}
                    </div>
                  </div>
                ))}
              </div>

              <div className="flex flex-wrap items-center justify-between gap-3 border-t border-border pt-3">
                {m.stopOperation ? (
                  <p className="text-xs text-muted-foreground">Đã gửi lệnh dừng: {m.stopOperation.reason}</p>
                ) : (
                  <span />
                )}
                {!m.stopOperation && m.authorityMode === 'dedicated' && (
                  <button
                    className={btnPrimary}
                    onClick={() => {
                      setSelected(m);
                      setReason('');
                    }}
                  >
                    Dừng trận có lý do
                  </button>
                )}
              </div>
            </article>
          ))}
          {!live.isLoading && !live.data?.length && (
            <div className="flex items-center justify-center rounded-xl border border-dashed border-border bg-card p-10 text-sm text-muted-foreground">
              Không có trận đang hoạt động.
            </div>
          )}
        </div>
      ) : (
        <div className={tableCard}>
          <div className="overflow-x-auto">
            <table className={tableText}>
              <thead className={tableHead}>
                <tr>
                  <th className={th}>Trận</th>
                  <th className={th}>Loại</th>
                  <th className={th}>Ván thắng A : B</th>
                  <th className={th}>Kết thúc</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {(history.data || []).map((m) => (
                  <tr key={m.matchId} className={rowHover}>
                    <td className={`${td} font-mono text-muted-foreground`}>{m.matchId}</td>
                    <td className={`${td} text-foreground`}>{m.matchType}</td>
                    <td className={`${td} font-mono font-bold text-foreground`}>
                      {m.teamAScore} : {m.teamBScore}
                    </td>
                    <td className={`${td} text-muted-foreground`}>{stamp(m.createdAt)}</td>
                  </tr>
                ))}
                {!history.isLoading && !history.data?.length && (
                  <tr>
                    <td colSpan={4} className="py-8 px-4 text-center text-muted-foreground">
                      Chưa có lịch sử trận.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Stop dialog */}
      {selected && (
        <div className={modalOverlay}>
          <form
            className={`${modalCard} max-w-lg`}
            onSubmit={(e) => {
              e.preventDefault();
              stop.mutate();
            }}
          >
            <h2 className={sectionTitle}>Dừng trận trên máy chủ riêng</h2>
            <p className="font-mono text-xs text-muted-foreground">{selected.matchId}</p>
            <p className="text-xs text-foreground">
              Trận sẽ bị gián đoạn và không tính Elo/thưởng nếu lệnh dừng được ghi trước kết quả cuối.
            </p>
            <label className={fieldLabel}>
              Lý do
              <textarea
                className={inputClass}
                rows={3}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                minLength={3}
                maxLength={2000}
                required
              />
            </label>
            <div className="flex justify-end gap-3 pt-2">
              <button type="button" className={btnGhost} disabled={stop.isPending} onClick={() => setSelected(null)}>
                Đóng
              </button>
              <button className={btnPrimary} disabled={stop.isPending}>
                Gửi lệnh dừng
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
}
