import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../../api/client';
import { adminApi } from '../../../api/adminApi';
import { AdminReportDto, ReviewReportRequest, SanctionAppeal, SanctionDecision } from '../../../types/api';
import { AppealStatus, ReportStatus, UserStatus, reportStatusLabel } from '../../../types/status';

const field = 'w-full p-2 bg-slate-900 border border-border rounded text-sm';
const action = 'px-3 py-2 bg-primary rounded text-white text-sm disabled:opacity-40';
const errorText = (e: unknown) => (e as { response?: { data?: { message?: string } }; message?: string })?.response?.data?.message || (e as Error)?.message || 'Không thực hiện được yêu cầu.';
async function replay(match: string) {
  const { data } = await apiClient.get(`/api/Replays/${match}`, { responseType: 'blob' });
  const url = URL.createObjectURL(data); const a = document.createElement('a'); a.href = url; a.download = `replay-${match}.json`; a.click(); setTimeout(() => URL.revokeObjectURL(url), 1000);
}
export function Moderation() {
  const client = useQueryClient();
  const [filter, setFilter] = useState<ReportStatus | ''>(ReportStatus.Pending);
  const [selected, setSelected] = useState<AdminReportDto | null>(null);
  const [status, setStatus] = useState<ReviewReportRequest['status']>(ReportStatus.Accepted);
  const [reason, setReason] = useState(''); const [sanction, setSanction] = useState(false);
  const [restriction, setRestriction] = useState<UserStatus.Banned | UserStatus.Suspended>(UserStatus.Banned);
  const [until, setUntil] = useState(''); const [notice, setNotice] = useState('');
  const [appeal, setAppeal] = useState<SanctionAppeal | null>(null);
  const [appealStatus, setAppealStatus] = useState<AppealStatus>(AppealStatus.Accepted);
  const [appealReason, setAppealReason] = useState('');
  const reports = useQuery({ queryKey: ['admin-reports', filter], queryFn: () => adminApi.getReports(filter || undefined) });
  const appeals = useQuery({ queryKey: ['admin-appeals'], queryFn: async () => (await apiClient.get<SanctionAppeal[]>('/api/Admin/appeals')).data });
  const decisions = useQuery({ queryKey: ['admin-sanctions'], queryFn: async () => (await apiClient.get<SanctionDecision[]>('/api/Admin/sanctions')).data });
  const invalidate = () => { ['admin-reports','admin-appeals','admin-sanctions','admin-users'].forEach(key => client.invalidateQueries({ queryKey: [key] })); };
  const review = useMutation({ mutationFn: (request: ReviewReportRequest) => adminApi.reviewReport(request),
    onSuccess: () => { setSelected(null); setNotice('Đã ghi kết luận và lịch sử xử lý.'); invalidate(); }, onError: e => setNotice(errorText(e)) });
  const reviewAppeal = useMutation({ mutationFn: async () => apiClient.post(`/api/Admin/appeals/${appeal!.appealId}/review`, { status: appealStatus, reason: appealReason }),
    onSuccess: () => { setAppeal(null); setNotice('Đã xử lý khiếu nại.'); invalidate(); }, onError: e => setNotice(errorText(e)) });
  const open = (report: AdminReportDto) => { setSelected(report); setReason(''); setSanction(false); setStatus(ReportStatus.Accepted); setUntil(''); };
  return <div className="space-y-6">
    <h1 className="text-2xl font-bold">Tố cáo, quyết định xử lý và khiếu nại</h1>
    {notice && <p role="status" className="p-3 bg-card border rounded">{notice}</p>}
    <div className="flex gap-3 items-center"><select className={field} value={filter} onChange={e => setFilter(e.target.value as ReportStatus | '')}>
      <option value="">Tất cả tố cáo</option>{Object.values(ReportStatus).map(s => <option key={s} value={s}>{reportStatusLabel[s]}</option>)}
    </select><button className={action} onClick={invalidate}>Làm mới</button></div>
    {reports.error && <p role="alert">{errorText(reports.error)}</p>}
    <div className="overflow-x-auto border rounded"><table className="w-full text-sm text-left"><thead><tr><th>Người gửi / bị tố cáo</th><th>Trận / bằng chứng</th><th>Nội dung</th><th>Kết luận</th><th /></tr></thead><tbody>
      {(reports.data || []).map(r => <tr className="border-t" key={`${r.matchId}-${r.reporterId}-${r.reportedPlayerId}`}>
        <td className="p-3">{r.reporterName}<br />→ {r.reportedPlayerName}</td><td className="p-3 font-mono text-xs">{r.matchId}<br />
          {r.replayUrl && <button className={action} onClick={() => replay(r.matchId).catch(e => setNotice(errorText(e)))}>Tải replay</button>}</td>
        <td className="p-3 max-w-sm whitespace-pre-wrap">{r.reason}</td><td className="p-3">{reportStatusLabel[r.status]}<br />{r.decisionReason}</td>
        <td className="p-3">{[ReportStatus.Pending, ReportStatus.Checking].includes(r.status as ReportStatus) && <button className={action} onClick={() => open(r)}>Xem xét</button>}</td>
      </tr>)}
      {!reports.isLoading && !reports.data?.length && <tr><td colSpan={5} className="p-5">Không có tố cáo phù hợp.</td></tr>}
    </tbody></table></div>
    <h2 className="text-xl font-semibold">Khiếu nại</h2>
    {appeals.error && <p role="alert">{errorText(appeals.error)}</p>}
    <div className="space-y-3">{(appeals.data || []).map(a => { const d = decisions.data?.find(s => s.sanctionId === a.sanctionId); return <article className="p-4 border rounded bg-card" key={a.appealId}>
      <p className="font-mono text-xs">Người chơi: {a.userId} · Quyết định: {a.sanctionId}</p>
      <p className="my-2 whitespace-pre-wrap">{d?.restriction}: {d?.reason}<br />Thời hạn: {d?.expiresAt ? new Date(d.expiresAt).toLocaleString('vi-VN') : 'Không thời hạn'}{d?.revokedAt && ' · Đã thu hồi'}</p>
      {d?.reportMatchId && <button className={action} onClick={() => replay(d.reportMatchId!).catch(e => setNotice(errorText(e)))}>Tải bằng chứng trận</button>}
      <p className="whitespace-pre-wrap my-2">{a.statement}</p><p>{a.status} · {a.decisionReason || 'Chưa có kết luận'}</p>
      {(a.status === AppealStatus.Pending || a.status === AppealStatus.Checking) && <button className={action} onClick={() => { setAppeal(a); setAppealReason(''); setAppealStatus(AppealStatus.Accepted); }}>Xem xét khiếu nại</button>}
    </article>; })}{!appeals.isLoading && !appeals.data?.length && <p>Chưa có khiếu nại.</p>}</div>
    <details className="border p-4 rounded"><summary>Lịch sử quyết định xử lý ({decisions.data?.length || 0})</summary>
      {decisions.error && <p>{errorText(decisions.error)}</p>}{(decisions.data || []).map(d => <p key={d.sanctionId} className="border-t py-3 whitespace-pre-wrap">{d.userId} · {d.restriction} · {new Date(d.createdAt).toLocaleString('vi-VN')}<br />{d.reason}<br />{d.revokedAt ? `Đã thu hồi: ${d.revocationReason}` : d.expiresAt ? `Hết hạn: ${new Date(d.expiresAt).toLocaleString('vi-VN')}` : 'Không thời hạn'}</p>)}
    </details>
    {selected && <div className="fixed inset-0 z-50 bg-black/80 flex items-center justify-center p-4"><form className="bg-card p-6 rounded space-y-4 w-full max-w-xl max-h-[90vh] overflow-auto" onSubmit={e => { e.preventDefault(); review.mutate({ reporterId: selected.reporterId, reportedPlayerId: selected.reportedPlayerId, matchId: selected.matchId, status, decisionReason: reason, banReportedPlayer: sanction && status === ReportStatus.Accepted, banReason: reason, restriction, bannedUntil: until ? new Date(until).toISOString() : null }); }}>
      <h2 className="text-xl font-bold">Xử lý tố cáo</h2><p>{selected.reason}</p>
      <label className="block">Kết quả<select className={field} value={status} onChange={e => setStatus(e.target.value as ReviewReportRequest['status'])}><option value={ReportStatus.Checking}>Đang xem xét</option><option value={ReportStatus.Accepted}>Chấp nhận tố cáo</option><option value={ReportStatus.Declined}>Bác tố cáo</option></select></label>
      <label className="block">Căn cứ và lý do quyết định<textarea className={field} required minLength={3} maxLength={2000} value={reason} onChange={e => setReason(e.target.value)} /></label>
      {status === ReportStatus.Accepted && <label className="block"><input type="checkbox" checked={sanction} onChange={e => setSanction(e.target.checked)} /> Áp dụng hạn chế tài khoản</label>}
      {sanction && status === ReportStatus.Accepted && <><select className={field} value={restriction} onChange={e => setRestriction(e.target.value as UserStatus.Banned | UserStatus.Suspended)}><option value={UserStatus.Banned}>Khóa tài khoản</option><option value={UserStatus.Suspended}>Tạm ngưng</option></select><label className="block">Thời hạn<input className={field} type="datetime-local" required={restriction === UserStatus.Suspended} value={until} onChange={e => setUntil(e.target.value)} /></label></>}
      <div className="flex gap-3"><button type="button" className={action} disabled={review.isPending} onClick={() => setSelected(null)}>Đóng</button><button className={action} disabled={review.isPending}>Lưu quyết định</button></div>
    </form></div>}
    {appeal && <div className="fixed inset-0 z-50 bg-black/80 flex items-center justify-center p-4"><form className="bg-card rounded p-6 space-y-4 w-full max-w-xl" onSubmit={e => { e.preventDefault(); reviewAppeal.mutate(); }}>
      <h2 className="text-xl font-bold">Xem xét khiếu nại</h2><p className="whitespace-pre-wrap">{appeal.statement}</p>
      <select className={field} value={appealStatus} onChange={e => setAppealStatus(e.target.value as AppealStatus)}><option value={AppealStatus.Checking}>Đang xem xét</option><option value={AppealStatus.Accepted}>Chấp nhận, thu hồi quyết định liên quan</option><option value={AppealStatus.Declined}>Từ chối</option></select>
      <label className="block">Lý do<textarea className={field} required minLength={3} maxLength={2000} value={appealReason} onChange={e => setAppealReason(e.target.value)} /></label>
      <p>Chấp nhận chỉ thu hồi quyết định gắn với khiếu nại này. Các quyết định khác còn hiệu lực được giữ nguyên.</p>
      <div className="flex gap-3"><button type="button" className={action} disabled={reviewAppeal.isPending} onClick={() => setAppeal(null)}>Đóng</button><button className={action} disabled={reviewAppeal.isPending}>Lưu quyết định</button></div>
    </form></div>}
  </div>;
}
