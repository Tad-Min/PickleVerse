import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminApi } from '../../../api/adminApi';
import { AdminReportDto, ReviewReportRequest } from '../../../types/api';
import { ShieldAlert, CheckCircle2, XCircle, Filter, X, AlertTriangle } from 'lucide-react';

export const Moderation: React.FC = () => {
  const [statusFilter, setStatusFilter] = useState<string>('PENDING');
  const [selectedReport, setSelectedReport] = useState<AdminReportDto | null>(null);

  // Review form states
  const [reviewStatus, setReviewStatus] = useState<'RESOLVED' | 'DISMISSED'>('RESOLVED');
  const [banReportedPlayer, setBanReportedPlayer] = useState<boolean>(false);
  const [banReason, setBanReason] = useState<string>('');
  const [toast, setToast] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  const queryClient = useQueryClient();

  const { data: reports = [], isLoading, refetch } = useQuery({
    queryKey: ['admin-reports', statusFilter],
    queryFn: () => adminApi.getReports(statusFilter || undefined),
  });

  const reviewMutation = useMutation({
    mutationFn: (data: ReviewReportRequest) => adminApi.reviewReport(data),
    onSuccess: (res) => {
      setToast({ type: 'success', text: res.message || 'Xử lý báo cáo vi phạm thành công!' });
      queryClient.invalidateQueries({ queryKey: ['admin-reports'] });
      queryClient.invalidateQueries({ queryKey: ['admin-analytics-summary'] });
      closeModal();
    },
    onError: () => {
      setToast({ type: 'error', text: 'Xử lý báo cáo thất bại. Vui lòng thử lại.' });
    },
  });

  const closeModal = () => {
    setSelectedReport(null);
    setBanReportedPlayer(false);
    setBanReason('');
  };

  const handleOpenReview = (report: AdminReportDto) => {
    setSelectedReport(report);
    setReviewStatus('RESOLVED');
    setBanReportedPlayer(false);
    setBanReason(`Vi phạm hành vi từ báo cáo trận đấu ${report.matchId.substring(0, 8)}: ${report.reason}`);
  };

  const handleSubmitReview = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedReport) return;

    reviewMutation.mutate({
      reporterId: selectedReport.reporterId,
      reportedPlayerId: selectedReport.reportedPlayerId,
      matchId: selectedReport.matchId,
      status: reviewStatus,
      banReportedPlayer,
      banReason: banReportedPlayer ? banReason : undefined,
    });
  };

  return (
    <div className="space-y-6">
      {/* Title */}
      <div>
        <h1 className="text-2xl font-bold font-display text-foreground tracking-tight">
          Hội đồng Xử lý Vi phạm (Tribunal & Moderation)
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Tiếp nhận tố cáo người chơi, kiểm tra dữ liệu trận đấu và đưa ra quyết định xử phạt kỷ luật
        </p>
      </div>

      {/* Toast Notification */}
      {toast && (
        <div
          className={`p-3.5 rounded-lg flex items-center justify-between text-xs font-medium border ${
            toast.type === 'success'
              ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20'
              : 'bg-rose-500/10 text-rose-400 border-rose-500/20'
          }`}
        >
          <span>{toast.text}</span>
          <button onClick={() => setToast(null)} className="text-muted-foreground hover:text-foreground">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {/* Filter Bar */}
      <div className="bg-card border border-border rounded-xl p-4 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Filter className="w-4 h-4 text-muted-foreground" />
          <span className="text-xs text-muted-foreground">Lọc theo trạng thái:</span>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            className="bg-slate-900 border border-border rounded-lg text-xs text-foreground px-3 py-1.5 focus:ring-2 focus:ring-primary"
          >
            <option value="PENDING">PENDING (Đang chờ xử lý)</option>
            <option value="RESOLVED">RESOLVED (Đã duyệt xử phạt)</option>
            <option value="DISMISSED">DISMISSED (Đã bác đơn)</option>
            <option value="">Tất cả</option>
          </select>
        </div>

        <button
          onClick={() => refetch()}
          className="text-xs px-3 py-1.5 bg-slate-900 hover:bg-slate-800 text-foreground border border-border rounded-lg transition-colors"
        >
          Làm mới
        </button>
      </div>

      {/* Reports Table */}
      <div className="bg-card border border-border rounded-xl shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-900/60 border-b border-border text-muted-foreground uppercase tracking-wider text-[11px] font-semibold">
              <tr>
                <th className="py-3.5 px-4">Người tố cáo</th>
                <th className="py-3.5 px-4">Người bị tố cáo</th>
                <th className="py-3.5 px-4">Trận đấu (Match ID)</th>
                <th className="py-3.5 px-4">Lý do vi phạm</th>
                <th className="py-3.5 px-4">Thời gian gửi</th>
                <th className="py-3.5 px-4">Trạng thái</th>
                <th className="py-3.5 px-4 text-right">Xử lý</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {isLoading ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-muted-foreground">
                    <div className="inline-block w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin mb-2"></div>
                    <div>Đang tải danh sách báo cáo vi phạm...</div>
                  </td>
                </tr>
              ) : reports.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-muted-foreground">
                    Không có báo cáo vi phạm nào trong danh mục này.
                  </td>
                </tr>
              ) : (
                reports.map((r) => {
                  const isPending = r.status === 'PENDING';
                  return (
                    <tr key={`${r.matchId}-${r.reportedPlayerId}`} className="hover:bg-slate-800/50 transition-colors">
                      <td className="py-3 px-4 font-semibold text-foreground">
                        {r.reporterName || 'Player #1'}
                      </td>

                      <td className="py-3 px-4">
                        <span className="font-bold text-rose-400">{r.reportedPlayerName || 'Người vi phạm'}</span>
                      </td>

                      <td className="py-3 px-4 font-mono text-accent text-[11px]">
                        {r.matchId ? r.matchId.substring(0, 8) + '...' : 'Unknown'}
                      </td>

                      <td className="py-3 px-4 text-foreground font-medium max-w-xs truncate" title={r.reason}>
                        {r.reason}
                      </td>

                      <td className="py-3 px-4 text-muted-foreground text-[11px]">
                        {new Date(r.createdAt).toLocaleString('vi-VN')}
                      </td>

                      <td className="py-3 px-4">
                        <span
                          className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium border ${
                            r.status === 'PENDING'
                              ? 'bg-rose-500/10 text-rose-400 border-rose-500/20 animate-pulse'
                              : r.status === 'RESOLVED'
                              ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20'
                              : 'bg-slate-700/30 text-slate-400 border-slate-700'
                          }`}
                        >
                          {r.status}
                        </span>
                      </td>

                      <td className="py-3 px-4 text-right">
                        {isPending ? (
                          <button
                            onClick={() => handleOpenReview(r)}
                            className="px-3 py-1 bg-primary hover:bg-primary-hover text-white text-[11px] font-semibold rounded-md shadow-sm transition-all"
                          >
                            Thẩm định
                          </button>
                        ) : (
                          <span className="text-muted-foreground text-[11px]">Đã giải quyết</span>
                        )}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* REVIEW REPORT MODAL */}
      {selectedReport && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="w-full max-w-md bg-card border border-border rounded-xl p-6 shadow-2xl relative">
            <div className="flex items-center justify-between pb-3 border-b border-border mb-4">
              <div className="flex items-center gap-2">
                <ShieldAlert className="w-5 h-5 text-rose-400" />
                <h3 className="text-base font-bold text-foreground font-display">Thẩm định Báo cáo Vi phạm</h3>
              </div>
              <button onClick={closeModal} className="text-muted-foreground hover:text-foreground">
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="space-y-3 mb-5 text-xs">
              <div className="p-3 bg-slate-900 rounded-lg border border-border space-y-1">
                <div>
                  <span className="text-muted-foreground">Người bị tố cáo:</span>{' '}
                  <strong className="text-rose-400">{selectedReport.reportedPlayerName}</strong>
                </div>
                <div>
                  <span className="text-muted-foreground">Người tố cáo:</span>{' '}
                  <span className="text-foreground">{selectedReport.reporterName}</span>
                </div>
                <div>
                  <span className="text-muted-foreground">Lý do phản ánh:</span>{' '}
                  <span className="text-amber-300 italic">"{selectedReport.reason}"</span>
                </div>
              </div>
            </div>

            <form onSubmit={handleSubmitReview} className="space-y-4">
              {/* Review Status Decision */}
              <div>
                <label className="block text-xs font-semibold text-foreground mb-1.5">Quyết định thẩm định</label>
                <div className="grid grid-cols-2 gap-3">
                  <button
                    type="button"
                    onClick={() => setReviewStatus('RESOLVED')}
                    className={`flex items-center justify-center gap-2 p-2.5 rounded-lg border text-xs font-semibold transition-all ${
                      reviewStatus === 'RESOLVED'
                        ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30'
                        : 'bg-slate-900 border-border text-muted-foreground'
                    }`}
                  >
                    <CheckCircle2 className="w-4 h-4" />
                    <span>Duyệt vi phạm</span>
                  </button>

                  <button
                    type="button"
                    onClick={() => {
                      setReviewStatus('DISMISSED');
                      setBanReportedPlayer(false);
                    }}
                    className={`flex items-center justify-center gap-2 p-2.5 rounded-lg border text-xs font-semibold transition-all ${
                      reviewStatus === 'DISMISSED'
                        ? 'bg-slate-700 text-slate-200 border-slate-600'
                        : 'bg-slate-900 border-border text-muted-foreground'
                    }`}
                  >
                    <XCircle className="w-4 h-4" />
                    <span>Bác đơn (Vô hiệu)</span>
                  </button>
                </div>
              </div>

              {/* Ban Checkbox (if RESOLVED) */}
              {reviewStatus === 'RESOLVED' && (
                <div className="space-y-3 pt-2">
                  <label className="flex items-center gap-2.5 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={banReportedPlayer}
                      onChange={(e) => setBanReportedPlayer(e.target.checked)}
                      className="rounded border-border text-rose-500 focus:ring-rose-500 bg-slate-900"
                    />
                    <span className="text-xs font-semibold text-rose-400 flex items-center gap-1.5">
                      <AlertTriangle className="w-3.5 h-3.5" />
                      Áp dụng chế tài: Khóa tài khoản người chơi này
                    </span>
                  </label>

                  {banReportedPlayer && (
                    <div>
                      <label className="block text-[11px] font-semibold text-foreground mb-1">
                        Lý do ghi vào hồ sơ kỷ luật
                      </label>
                      <input
                        type="text"
                        value={banReason}
                        onChange={(e) => setBanReason(e.target.value)}
                        placeholder="Lý do khóa..."
                        required
                        className="w-full p-2 bg-slate-900 border border-border rounded-lg text-xs text-foreground focus:ring-2 focus:ring-rose-500"
                      />
                    </div>
                  )}
                </div>
              )}

              <div className="flex items-center justify-end gap-2 pt-4 border-t border-border">
                <button
                  type="button"
                  onClick={closeModal}
                  className="px-3 py-1.5 text-xs text-muted-foreground hover:text-foreground"
                >
                  Hủy
                </button>
                <button
                  type="submit"
                  disabled={reviewMutation.isPending}
                  className="px-4 py-2 bg-primary hover:bg-primary-hover text-white text-xs font-semibold rounded-lg shadow-sm disabled:opacity-50"
                >
                  {reviewMutation.isPending ? 'Đang thực thi...' : 'Hoàn tất thẩm định'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
