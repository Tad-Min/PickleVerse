import { useState } from 'react';
import { useQuery, useQueryClient, useMutation } from '@tanstack/react-query';
import { apiClient } from '../../../api/client';
import { adminApi } from '../../../api/adminApi';
import { LiveMatch, StopOperation } from '../../../types/api';

const button = 'px-3 py-2 rounded bg-primary text-white text-sm disabled:opacity-40';
const stamp = (value?: string | null) => value ? new Date(value).toLocaleString('vi-VN') : 'Chưa có';
const message = (error: unknown) => (error as { response?: { data?: { message?: string } } })?.response?.data?.message || (error as Error)?.message;
export function Matches() {
  const [tab, setTab] = useState<'live' | 'history'>('live'); const [poll, setPoll] = useState(true);
  const [selected, setSelected] = useState<LiveMatch | null>(null); const [reason, setReason] = useState('');
  const [operation, setOperation] = useState<StopOperation | null>(null); const [notice, setNotice] = useState('');
  const client = useQueryClient();
  const live = useQuery({ queryKey: ['admin-live-matches'], queryFn: async () => (await apiClient.get<LiveMatch[]>('/api/Admin/matches/live')).data, refetchInterval: poll ? 5000 : false });
  const history = useQuery({ queryKey: ['admin-recent-matches'], queryFn: () => adminApi.getRecentMatches(50), enabled: tab === 'history' });
  const acknowledgement = useQuery({ queryKey: ['admin-stop-operation', operation?.matchId], queryFn: async () => (await apiClient.get<StopOperation>(`/api/Admin/matches/${operation!.matchId}/stop`)).data,
    enabled: !!operation, refetchInterval: operation && !operation.acknowledgedAt ? 3000 : false });
  const stop = useMutation({ mutationFn: async () => (await apiClient.post<StopOperation>(`/api/Admin/matches/${selected!.matchId}/stop`, { reason })).data,
    onSuccess: data => { setOperation(data); setSelected(null); setReason(''); client.invalidateQueries({ queryKey: ['admin-live-matches'] }); }, onError: e => setNotice(message(e) || 'Không dừng được trận.') });
  const observed = acknowledgement.data || operation;
  return <div className="space-y-5">
    <h1 className="text-2xl font-bold">Giám sát trận đấu</h1><p>Điểm và kết nối lấy từ heartbeat của Dedicated. Lịch sử được hiển thị riêng.</p>
    <div className="flex gap-3"><button className={button} onClick={() => setTab('live')}>Trận đang hoạt động ({live.data?.length || 0})</button><button className={button} onClick={() => setTab('history')}>Lịch sử</button><label><input type="checkbox" checked={poll} onChange={e => setPoll(e.target.checked)} /> Cập nhật mỗi 5 giây</label></div>
    {notice && <p role="alert">{notice}</p>}{(live.error || history.error) && <p role="alert">{message(live.error || history.error)}</p>}
    {observed && <article className="border rounded p-4 bg-card"><p>Lệnh dừng: {observed.operationId}</p><p>Trận: {observed.matchId}</p><p>{observed.reason}</p><p>{observed.acknowledgedAt ? `Dedicated đã xác nhận: ${stamp(observed.acknowledgedAt)}` : 'Đã lưu bền; đang chờ Dedicated xác nhận. Kết quả đến muộn không được tính Elo/thưởng.'}</p></article>}
    {tab === 'live' ? <div className="space-y-4">{(live.data || []).map(m => <article className="border rounded p-5 bg-card space-y-3" key={m.matchId}>
      <div className="flex justify-between"><p className="font-mono">{m.matchId}</p><p>{m.status} · {m.authorityMode}</p></div>
      <p>Bắt đầu: {stamp(m.startedAt)} · Heartbeat: {stamp(m.heartbeatAt)} <strong className={m.heartbeatStale ? 'text-red-400' : ''}>{m.heartbeatStale && '(Quá hạn)'}</strong></p>
      {m.live ? <p className="text-xl">Điểm {m.live.scoreA} : {m.live.scoreB} · Ván {m.live.gamesA} : {m.live.gamesB} · {m.live.paused ? 'Đang tạm dừng' : m.live.started ? 'Đang thi đấu' : 'Chờ vào sân'} · Cập nhật: {stamp(m.live.observedAt)}</p> : <p>Chưa có trạng thái mô phỏng từ Dedicated.</p>}
      <div className="grid grid-cols-2 gap-4">{[0,1].map(team => <div key={team}><strong>Đội {team === 0 ? 'A' : 'B'}</strong>{m.roster.members.filter(p => p.team === team).map(p => { const state = m.live?.players.find(x => x.id === p.id); return <p key={p.id}>{p.name}{p.bot ? ' (Bot)' : ''} · {state ? state.connected ? 'Đã kết nối' : `Mất kết nối${state.reconnectUntil ? ` · Chờ đến ${stamp(state.reconnectUntil)}` : ''}` : 'Chưa có dữ liệu'}</p>; })}</div>)}</div>
      {m.stopOperation ? <p>Đã gửi lệnh dừng: {m.stopOperation.reason}</p> : m.authorityMode === 'dedicated' && <button className={button} onClick={() => { setSelected(m); setReason(''); }}>Dừng trận có lý do</button>}
    </article>)}{!live.isLoading && !live.data?.length && <p>Không có trận đang hoạt động.</p>}</div> : <table className="w-full text-left text-sm"><thead><tr><th>Trận</th><th>Loại</th><th>Ván thắng A : B</th><th>Kết thúc</th></tr></thead><tbody>{(history.data || []).map(m => <tr key={m.matchId} className="border-t"><td className="p-3 font-mono">{m.matchId}</td><td>{m.matchType}</td><td>{m.teamAScore} : {m.teamBScore}</td><td>{stamp(m.createdAt)}</td></tr>)}</tbody></table>}
    {selected && <div className="fixed inset-0 z-50 bg-black/80 flex justify-center items-center p-4"><form className="bg-card p-6 rounded w-full max-w-lg space-y-4" onSubmit={e => { e.preventDefault(); stop.mutate(); }}><h2 className="text-xl font-bold">Dừng trận Dedicated</h2><p>{selected.matchId}</p><p>Trận sẽ bị gián đoạn và không tính Elo/thưởng nếu lệnh dừng được ghi trước kết quả cuối.</p><label className="block">Lý do<textarea className="w-full p-3 bg-slate-900 border rounded" value={reason} onChange={e => setReason(e.target.value)} minLength={3} maxLength={2000} required /></label><div className="flex gap-3"><button type="button" className={button} disabled={stop.isPending} onClick={() => setSelected(null)}>Đóng</button><button className={button} disabled={stop.isPending}>Gửi lệnh dừng</button></div></form></div>}
  </div>;
}
