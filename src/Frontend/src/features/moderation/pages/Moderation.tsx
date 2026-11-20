import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { RefreshCw, X } from 'lucide-react';
import { apiClient } from '../../../api/client';
import { adminApi } from '../../../api/adminApi';
import { AdminReportDto, ReviewReportRequest, SanctionAppeal, SanctionDecision } from '../../../types/api';
import { AppealStatus, ReportStatus, UserStatus, appealStatusLabel, reportStatusLabel } from '../../../types/status';

const field = 'w-full p-2.5 bg-background border border-border rounded-lg text-xs text-foreground focus:outline-none focus:ring-2 focus:ring-primary';
const primaryBtn = 'px-4 py-1.5 text-xs font-semibold bg-primary hover:bg-primary-hover text-white rounded-lg shadow-sm disabled:opacity-40';
const ghostBtn = 'px-4 py-1.5 text-xs font-semibold bg-background border border-border hover:bg-muted text-foreground rounded-lg disabled:opacity-40';
const smallBtn = 'px-2.5 py-1 text-[11px] font-semibold bg-background border border-border hover:bg-muted text-foreground rounded-md disabled:opacity-40';
const badge = 'inline-flex items-center whitespace-nowrap px-2 py-0.5 rounded text-[11px] font-medium border';
const statusTone: Record<string, string> = {
  PENDING: 'bg-amber-500/10 text-amber-600 border-amber-500/20',
  CHECKING: 'bg-indigo-500/10 text-indigo-600 border-indigo-500/20',
  ACCEPTED: 'bg-rose-500/10 text-rose-600 border-rose-500/20',
  DECLINED: 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20',
};
const restrictionLabel: Record<string, string> = { BANNED: 'Khóa tài khoản', SUSPENDED: 'Đình chỉ' };
const tableHead = 'py-3.5 px-4 text-left';
const formatTime = (value?: string | null) => (value ? new Date(value).toLocaleString('vi-VN') : '');

const errorText = (e: unknown) =>
  (e as { response?: { data?: { message?: string } }; message?: string })?.response?.data?.message ||
  (e as Error)?.message ||
  'Không thực hiện được yêu cầu.';

async function replay(match: string) {
  const { data } = await apiClient.get(`/api/Replays/${match}`, { responseType: 'blob' });
  const url = URL.createObjectURL(data);
  const a = document.createElement('a');
  a.href = url;
  a.download = `replay-${match}.json`;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export function Moderation() {
  const client = useQueryClient();
  const [filter, setFilter] = useState<ReportStatus | ''>(ReportStatus.Pending);
  const [selected, setSelected] = useState<AdminReportDto | null>(null);
  const [status, setStatus] = useState<ReviewReportRequest['status']>(ReportStatus.Accepted);
  const [reason, setReason] = useState('');
  const [sanction, setSanction] = useState(false);
  const [restriction, setRestriction] = useState<UserStatus.Banned | UserStatus.Suspended>(UserStatus.Banned);
  const [until, setUntil] = useState('');
  const [notice, setNotice] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [appeal, setAppeal] = useState<SanctionAppeal | null>(null);
  const [appealStatus, setAppealStatus] = useState<AppealStatus>(AppealStatus.Accepted);
  const [appealReason, setAppealReason] = useState('');

  const reports = useQuery({ queryKey: ['admin-reports', filter], queryFn: () => adminApi.getReports(filter || undefined) });
  const appeals = useQuery({
    queryKey: ['admin-appeals'],
    queryFn: async () => (await apiClient.get<SanctionAppeal[]>('/api/Admin/appeals')).data,
  });
  const decisions = useQuery({
    queryKey: ['admin-sanctions'],
    queryFn: async () => (await apiClient.get<SanctionDecision[]>('/api/Admin/sanctions')).data,
  });

  const invalidate = () => {
    ['admin-reports', 'admin-appeals', 'admin-sanctions', 'admin-users'].forEach((key) =>
      client.invalidateQueries({ queryKey: [key] }),
    );
  };

  const review = useMutation({
    mutationFn: (request: ReviewReportRequest) => adminApi.reviewReport(request),
    onSuccess: () => {
      setSelected(null);
      setNotice({ type: 'success', text: 'Đã ghi kết luận và lịch sử xử lý.' });
      invalidate();
    },
    onError: (e) => setNotice({ type: 'error', text: errorText(e) }),
  });

  const reviewAppeal = useMutation({
    mutationFn: async () =>
      apiClient.post(`/api/Admin/appeals/${appeal!.appealId}/review`, { status: appealStatus, reason: appealReason }),
    onSuccess: () => {
      setAppeal(null);
      setNotice({ type: 'success', text: 'Đã xử lý khiếu nại.' });
      invalidate();
    },
    onError: (e) => setNotice({ type: 'error', text: errorText(e) }),
  });

  const openReport = (report: AdminReportDto) => {
    setSelected(report);
    setReason('');
    setSanction(false);
    setStatus(ReportStatus.Accepted);
    setUntil('');
  };

  const openAppeal = (item: SanctionAppeal) => {
    setAppeal(item);
    setAppealReason('');
    setAppealStatus(AppealStatus.Accepted);
  };

  const runReplay = (matchId: string) => replay(matchId).catch((e) => setNotice({ type: 'error', text: errorText(e) }));

  return (
    <div className="space-y-6">
      {/* Title */}
      <div>
        <h1 className="text-2xl font-bold font-display text-foreground tracking-tight">Xử lý vi phạm</h1>
        <p className="text-sm text-muted-foreground mt-1">
          Tiếp nhận tố cáo, ra quyết định xử lý và giải quyết khiếu nại
        </p>
      </div>

      {/* Notification Banner */}
      {notice && (
        <div
          role="status"
          className={`p-3.5 rounded-lg flex items-center justify-between text-xs font-medium border ${
            notice.type === 'success'
              ? 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20'
              : 'bg-rose-500/10 text-rose-600 border-rose-500/20'
          }`}
        >
          <span>{notice.text}</span>
          <button onClick={() => setNotice(null)} className="p-1 hover:opacity-80" aria-label="Đóng thông báo">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {/* Toolbar */}
      <div className="bg-card border border-border rounded-xl p-4 flex flex-col md:flex-row items-center justify-between gap-4">
        <p className="text-xs text-muted-foreground">Danh sách tố cáo theo trạng thái xử lý</p>
        <div className="flex items-center gap-2 w-full md:w-auto">
          <select
            value={filter}
            onChange={(e) => setFilter(e.target.value as ReportStatus | '')}
            className="bg-background border border-border rounded-lg text-xs text-foreground px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary"
          >
            <option value="">Tất cả tố cáo</option>
            {Object.values(ReportStatus).map((s) => (
              <option key={s} value={s}>
                {reportStatusLabel[s]}
              </option>
            ))}
          </select>
          <button onClick={invalidate} className={`${ghostBtn} inline-flex items-center gap-1.5`}>
            <RefreshCw className="w-3.5 h-3.5" />
            Làm mới
          </button>
        </div>
      </div>

      {reports.error && <p role="alert" className="text-xs text-rose-600">{errorText(reports.error)}</p>}

      {/* Reports Table */}
      <div className="bg-card border border-border rounded-xl shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-background/60 border-b border-border text-muted-foreground uppercase tracking-wider text-[11px] font-semibold">
              <tr>
                <th className={tableHead}>Người gửi → Bị tố cáo</th>
                <th className={tableHead}>Trận / bằng chứng</th>
                <th className={tableHead}>Nội dung</th>
                <th className={tableHead}>Trạng thái</th>
                <th className="py-3.5 px-4 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {(reports.data || []).map((r) => (
                <tr key={`${r.matchId}-${r.reporterId}-${r.reportedPlayerId}`} className="hover:bg-muted/50 transition-colors">
                  <td className="py-3 px-4">
                    <div className="font-semibold text-foreground">{r.reporterName}</div>
                    <div className="text-muted-foreground">→ {r.reportedPlayerName}</div>
                  </td>
                  <td className="py-3 px-4">
                    <div className="font-mono text-[11px] text-muted-foreground">{r.matchId}</div>
                    {r.replayUrl && (
                      <button className={`${smallBtn} mt-1.5`} onClick={() => runReplay(r.matchId)}>
                        Tải replay
                      </button>
                    )}
                  </td>
                  <td className="py-3 px-4 max-w-sm whitespace-pre-wrap text-foreground">{r.reason}</td>
                  <td className="py-3 px-4">
                    <span className={`${badge} ${statusTone[r.status] ?? ''}`}>
                      {reportStatusLabel[r.status as ReportStatus] ?? r.status}
                    </span>
                    {r.decisionReason && (
                      <div className="mt-1.5 text-muted-foreground whitespace-pre-wrap">{r.decisionReason}</div>
                    )}
                  </td>
                  <td className="py-3 px-4 text-right">
                    {[ReportStatus.Pending, ReportStatus.Checking].includes(r.status as ReportStatus) && (
                      <button className={primaryBtn} onClick={() => openReport(r)}>
                        Xem xét
                      </button>
                    )}
                  </td>
                </tr>
              ))}
              {!reports.isLoading && !reports.data?.length && (
                <tr>
                  <td colSpan={5} className="py-8 px-4 text-center text-muted-foreground">
                    Không có tố cáo phù hợp.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Appeals */}
      <div>
        <h2 className="text-lg font-bold font-display text-foreground mb-3">Khiếu nại</h2>
        {appeals.error && <p role="alert" className="text-xs text-rose-600">{errorText(appeals.error)}</p>}
        <div className="bg-card border border-border rounded-xl shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-background/60 border-b border-border text-muted-foreground uppercase tracking-wider text-[11px] font-semibold">
                <tr>
                  <th className={tableHead}>Người chơi / Quyết định</th>
                  <th className={tableHead}>Hình thức xử lý</th>
                  <th className={tableHead}>Nội dung khiếu nại</th>
                  <th className={tableHead}>Trạng thái</th>
                  <th className="py-3.5 px-4 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {(appeals.data || []).map((a) => {
                  const d = decisions.data?.find((s) => s.sanctionId === a.sanctionId);
                  const canReview = a.status === AppealStatus.Pending || a.status === AppealStatus.Checking;
                  return (
                    <tr key={a.appealId} className="hover:bg-muted/50 transition-colors align-top">
                      <td className="py-3 px-4">
                        <div className="font-mono text-[11px] text-muted-foreground">Người chơi: {a.userId}</div>
                        <div className="font-mono text-[11px] text-muted-foreground">Quyết định: {a.sanctionId}</div>
                      </td>
                      <td className="py-3 px-4">
                        <div className="text-foreground">
                          {d ? restrictionLabel[d.restriction] ?? d.restriction : '—'}
                          {d?.reason ? `: ${d.reason}` : ''}
                        </div>
                        <div className="text-muted-foreground mt-1">
                          Thời hạn: {d?.expiresAt ? formatTime(d.expiresAt) : 'Không thời hạn'}
                          {d?.revokedAt && ' · Đã thu hồi'}
                        </div>
                        {d?.reportMatchId && (
                          <button className={`${smallBtn} mt-1.5`} onClick={() => runReplay(d.reportMatchId!)}>
                            Tải bằng chứng trận
                          </button>
                        )}
                      </td>
                      <td className="py-3 px-4 max-w-sm whitespace-pre-wrap text-foreground">{a.statement}</td>
                      <td className="py-3 px-4">
                        <span className={`${badge} ${statusTone[a.status] ?? ''}`}>
                          {appealStatusLabel[a.status] ?? a.status}
                        </span>
                        {a.decisionReason && (
                          <div className="mt-1.5 text-muted-foreground whitespace-pre-wrap">{a.decisionReason}</div>
                        )}
                      </td>
                      <td className="py-3 px-4 text-right">
                        {canReview && (
                          <button className={primaryBtn} onClick={() => openAppeal(a)}>
                            Xem xét khiếu nại
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
                {!appeals.isLoading && !appeals.data?.length && (
                  <tr>
                    <td colSpan={5} className="py-8 px-4 text-center text-muted-foreground">
                      Chưa có khiếu nại.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {/* Decision History */}
      <details className="bg-card border border-border rounded-xl p-4">
        <summary className="cursor-pointer text-sm font-semibold text-foreground">
          Lịch sử quyết định xử lý ({decisions.data?.length || 0})
        </summary>
        {decisions.error && <p className="mt-3 text-xs text-rose-600">{errorText(decisions.error)}</p>}
        <div className="mt-3 divide-y divide-border">
          {(decisions.data || []).map((d) => (
            <div key={d.sanctionId} className="py-3 text-xs space-y-1">
              <div className="font-semibold text-foreground">
                {d.userId} · {restrictionLabel[d.restriction] ?? d.restriction} · {formatTime(d.createdAt)}
              </div>
              <div className="text-foreground whitespace-pre-wrap">{d.reason}</div>
              <div className="text-muted-foreground">
                {d.revokedAt
                  ? `Đã thu hồi: ${d.revocationReason}`
                  : d.expiresAt
                  ? `Hết hạn: ${formatTime(d.expiresAt)}`
                  : 'Không thời hạn'}
              </div>
            </div>
          ))}
        </div>
      </details>

      {/* Report review dialog */}
      {selected && (
        <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
          <form
            className="bg-card border border-border rounded-xl shadow-lg p-6 space-y-4 w-full max-w-xl max-h-[90vh] overflow-auto"
            onSubmit={(e) => {
              e.preventDefault();
              review.mutate({
                reporterId: selected.reporterId,
                reportedPlayerId: selected.reportedPlayerId,
                matchId: selected.matchId,
                status,
                decisionReason: reason,
                banReportedPlayer: sanction && status === ReportStatus.Accepted,
                banReason: reason,
                restriction,
                bannedUntil: until ? new Date(until).toISOString() : null,
              });
            }}
          >
            <h2 className="text-lg font-bold font-display text-foreground">Xử lý tố cáo</h2>
            <p className="text-xs text-muted-foreground whitespace-pre-wrap">{selected.reason}</p>

            <label className="block space-y-1 text-xs font-medium text-foreground">
              Kết quả
              <select className={field} value={status} onChange={(e) => setStatus(e.target.value as ReviewReportRequest['status'])}>
                <option value={ReportStatus.Checking}>Đang xem xét</option>
                <option value={ReportStatus.Accepted}>Chấp nhận tố cáo</option>
                <option value={ReportStatus.Declined}>Bác tố cáo</option>
              </select>
            </label>

            <label className="block space-y-1 text-xs font-medium text-foreground">
              Căn cứ và lý do quyết định
              <textarea
                className={field}
                required
                minLength={3}
                maxLength={2000}
                rows={3}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>

            {status === ReportStatus.Accepted && (
              <label className="flex items-center gap-2 text-xs text-foreground">
                <input type="checkbox" checked={sanction} onChange={(e) => setSanction(e.target.checked)} />
                Áp dụng hạn chế tài khoản
              </label>
            )}

            {sanction && status === ReportStatus.Accepted && (
              <div className="grid gap-3 md:grid-cols-2">
                <label className="block space-y-1 text-xs font-medium text-foreground">
                  Hình thức
                  <select
                    className={field}
                    value={restriction}
                    onChange={(e) => setRestriction(e.target.value as UserStatus.Banned | UserStatus.Suspended)}
                  >
                    <option value={UserStatus.Banned}>Khóa tài khoản</option>
                    <option value={UserStatus.Suspended}>Đình chỉ</option>
                  </select>
                </label>
                <label className="block space-y-1 text-xs font-medium text-foreground">
                  Thời hạn
                  <input
                    className={field}
                    type="datetime-local"
                    required={restriction === UserStatus.Suspended}
                    value={until}
                    onChange={(e) => setUntil(e.target.value)}
                  />
                </label>
              </div>
            )}

            <div className="flex justify-end gap-3 pt-2">
              <button type="button" className={ghostBtn} disabled={review.isPending} onClick={() => setSelected(null)}>
                Đóng
              </button>
              <button className={primaryBtn} disabled={review.isPending}>
                Lưu quyết định
              </button>
            </div>
          </form>
        </div>
      )}

      {/* Appeal review dialog */}
      {appeal && (
        <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
          <form
            className="bg-card border border-border rounded-xl shadow-lg p-6 space-y-4 w-full max-w-xl"
            onSubmit={(e) => {
              e.preventDefault();
              reviewAppeal.mutate();
            }}
          >
            <h2 className="text-lg font-bold font-display text-foreground">Xem xét khiếu nại</h2>
            <p className="text-xs text-foreground whitespace-pre-wrap">{appeal.statement}</p>

            <label className="block space-y-1 text-xs font-medium text-foreground">
              Kết quả
              <select className={field} value={appealStatus} onChange={(e) => setAppealStatus(e.target.value as AppealStatus)}>
                <option value={AppealStatus.Checking}>Đang xem xét</option>
                <option value={AppealStatus.Accepted}>Chấp nhận, thu hồi quyết định liên quan</option>
                <option value={AppealStatus.Declined}>Từ chối</option>
              </select>
            </label>

            <label className="block space-y-1 text-xs font-medium text-foreground">
              Lý do
              <textarea
                className={field}
                required
                minLength={3}
                maxLength={2000}
                rows={3}
                value={appealReason}
                onChange={(e) => setAppealReason(e.target.value)}
              />
            </label>

            <p className="text-xs text-muted-foreground">
              Chấp nhận chỉ thu hồi quyết định gắn với khiếu nại này. Các quyết định khác còn hiệu lực được giữ nguyên.
            </p>

            <div className="flex justify-end gap-3 pt-2">
              <button type="button" className={ghostBtn} disabled={reviewAppeal.isPending} onClick={() => setAppeal(null)}>
                Đóng
              </button>
              <button className={primaryBtn} disabled={reviewAppeal.isPending}>
                Lưu quyết định
              </button>
            </div>
          </form>
        </div>
      )}
    </div>
  );
}
