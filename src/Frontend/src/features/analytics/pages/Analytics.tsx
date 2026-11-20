import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid } from 'recharts';
import { apiClient } from '../../../api/client';
import { AnalyticsReport } from '../../../types/api';
import {
  btnPrimary, card, fieldLabel, inputClass, pageSubtitle, pageTitle, sectionTitle, tableCard, tableHead, tableText, th, td, rowHover,
} from '../../../shared/ui/adminStyles';

const today = new Date(Date.now() + 7 * 3600000).toISOString().slice(0, 10);
const monthAgo = new Date(Date.now() + 7 * 3600000 - 29 * 86400000).toISOString().slice(0, 10);
const rate = (value?: number | null) => (value == null ? 'Chưa đủ dữ liệu' : (value * 100).toFixed(1) + '%');
const seconds = (value?: number | null) => (value == null ? 'Chưa có dữ liệu' : value.toFixed(1) + ' giây');

const statTile = 'bg-card border border-border rounded-xl p-5 shadow-sm';

export function Analytics() {
  const [from, setFrom] = useState(monthAgo);
  const [to, setTo] = useState(today);
  const report = useQuery({
    queryKey: ['admin-analytics-report', from, to],
    queryFn: async () =>
      (await apiClient.get<AnalyticsReport>('/api/Admin/analytics/report', { params: { from, to } })).data,
    enabled: !!from && !!to && from <= to,
  });
  const data = report.data;
  const errorMessage =
    (report.error as { response?: { data?: { message?: string } } })?.response?.data?.message ||
    (report.error as Error)?.message;

  return (
    <div className="space-y-6">
      {/* Title */}
      <div>
        <h1 className={pageTitle}>Thống kê người chơi và ghép trận</h1>
        <p className={pageSubtitle}>Theo dõi người chơi đăng nhập, tỷ lệ quay lại, bài luyện tập và tình trạng ghép trận</p>
      </div>

      {/* Date range toolbar */}
      <form
        className="bg-card border border-border rounded-xl p-4 flex flex-col md:flex-row md:items-end gap-4"
        onSubmit={(e) => {
          e.preventDefault();
          report.refetch();
        }}
      >
        <label className={`${fieldLabel} md:w-56`}>
          Từ ngày (Việt Nam)
          <input className={inputClass} type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} required />
        </label>
        <label className={`${fieldLabel} md:w-56`}>
          Đến ngày
          <input className={inputClass} type="date" value={to} min={from} max={today} onChange={(e) => setTo(e.target.value)} required />
        </label>
        <button className={`${btnPrimary} md:ml-auto`} disabled={report.isFetching}>
          {report.isFetching ? 'Đang tính…' : 'Làm mới'}
        </button>
      </form>

      {report.error && (
        <p role="alert" className="text-xs text-rose-600">
          {errorMessage}
        </p>
      )}

      {data && (
        <>
          <p className="text-xs text-muted-foreground">
            {data.timeZone} · {data.from} → {data.to}
          </p>

          {/* Summary tiles */}
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            {[
              ['Đăng ký trong khoảng', data.summary.totalUsers],
              ['DAU hôm nay', data.summary.activeUsersToday],
              ['Trận hoàn tất trong khoảng', data.summary.totalMatchesPlayed],
              ['Giờ hoạt động trong khoảng', (data.summary.totalPlayTimeSeconds / 3600).toFixed(2)],
            ].map(([label, value]) => (
              <div className={statTile} key={label}>
                <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">{label}</p>
                <p className="mt-2 text-2xl font-bold font-display text-foreground">{value}</p>
              </div>
            ))}
          </div>

          {/* Daily chart */}
          <section className={card}>
            <h2 className={sectionTitle}>Người chơi đăng nhập theo ngày</h2>
            <p className="text-xs text-muted-foreground mt-1 mb-4">{data.dauDefinition}</p>
            <div className="h-64">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={data.daily}>
                  <CartesianGrid strokeDasharray="3 3" stroke="#DEDEE3" />
                  <XAxis dataKey="day" stroke="#686868" fontSize={11} />
                  <YAxis allowDecimals={false} stroke="#686868" fontSize={11} />
                  <Tooltip />
                  <Area dataKey="players" name="Người chơi" stroke="#2563EB" fill="#DBEAFE" />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </section>

          {/* Retention */}
          <section className={tableCard}>
            <div className="p-4 border-b border-border">
              <h2 className={sectionTitle}>Tỷ lệ quay lại</h2>
              <p className="text-xs text-muted-foreground mt-1">{data.retentionDefinition}</p>
            </div>
            <div className="overflow-x-auto">
              <table className={tableText}>
                <thead className={tableHead}>
                  <tr>
                    <th className={th}>Ngày đăng nhập đầu</th>
                    <th className={th}>Số người</th>
                    <th className={th}>D1</th>
                    <th className={th}>D7</th>
                    <th className={th}>D30</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {data.retention.map((c) => (
                    <tr key={c.day} className={rowHover}>
                      <td className={`${td} text-foreground`}>{c.day}</td>
                      <td className={`${td} font-mono`}>{c.players}</td>
                      <td className={`${td} font-mono`}>{rate(c.d1)}</td>
                      <td className={`${td} font-mono`}>{rate(c.d7)}</td>
                      <td className={`${td} font-mono`}>{rate(c.d30)}</td>
                    </tr>
                  ))}
                  {!data.retention.length && (
                    <tr>
                      <td colSpan={5} className="py-8 px-4 text-center text-muted-foreground">
                        Chưa có cohort trong khoảng này.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </section>

          {/* Lessons */}
          <section className={tableCard}>
            <div className="p-4 border-b border-border">
              <h2 className={sectionTitle}>Bài luyện tập phổ biến</h2>
              <p className="text-xs text-muted-foreground mt-1">
                Tỷ lệ vượt qua = số lượt đạt yêu cầu / số lượt bắt đầu. Các lượt chưa kết thúc vẫn được tính trong mẫu số.
              </p>
            </div>
            <div className="overflow-x-auto">
              <table className={tableText}>
                <thead className={tableHead}>
                  <tr>
                    <th className={th}>Bài</th>
                    <th className={th}>Người chơi</th>
                    <th className={th}>Bắt đầu</th>
                    <th className={th}>Kết thúc</th>
                    <th className={th}>Vượt qua</th>
                    <th className={th}>Tỷ lệ</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {data.lessons.map((l) => (
                    <tr key={l.lessonId} className={rowHover}>
                      <td className={`${td} text-foreground font-medium`}>{l.title}</td>
                      <td className={`${td} font-mono`}>{l.players}</td>
                      <td className={`${td} font-mono`}>{l.starts}</td>
                      <td className={`${td} font-mono`}>{l.finished}</td>
                      <td className={`${td} font-mono`}>{l.passed}</td>
                      <td className={`${td} font-mono font-bold text-primary`}>{rate(l.completionRate)}</td>
                    </tr>
                  ))}
                  {!data.lessons.length && (
                    <tr>
                      <td colSpan={6} className="py-8 px-4 text-center text-muted-foreground">
                        Chưa có dữ liệu bài luyện tập.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </section>

          {/* Matchmaking */}
          <section className={card}>
            <h2 className={sectionTitle}>Tình trạng ghép trận</h2>
            <p className="text-xs text-muted-foreground mt-1 mb-4">
              Nhật ký cập nhật từ hàng đợi bền. Một lượt giữ kết quả ghép đầu tiên; hủy sau khi ghép được thống kê riêng.
            </p>
            <div className="grid gap-3 md:grid-cols-3 xl:grid-cols-5">
              {[
                ['Lượt tìm', data.matchmaking.searches],
                ['Đang chờ', data.matchmaking.searching],
                ['Ghép người thật', data.matchmaking.matched],
                ['Bot dự phòng', data.matchmaking.botFallback],
                ['Hủy trước ghép', data.matchmaking.cancelled],
                ['Hết thời gian chờ', data.matchmaking.timedOut],
                ['Hủy sau ghép', data.matchmaking.cancelledAfterAssignment],
                ['Chờ trung bình', seconds(data.matchmaking.averageWaitSeconds)],
                ['P95 chờ', seconds(data.matchmaking.p95WaitSeconds)],
              ].map(([label, value]) => (
                <div key={label} className="rounded-lg border border-border bg-background p-3">
                  <p className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">{label}</p>
                  <p className="mt-1 text-sm font-bold text-foreground">{value}</p>
                </div>
              ))}
            </div>
          </section>

          {/* Error heatmap */}
          <section className={card}>
            <h2 className={sectionTitle}>Bản đồ nhiệt lỗi được hệ thống ghi nhận</h2>
            <p className="text-xs text-muted-foreground mt-1 mb-4">
              Đây là kết quả luật phân tích; chất lượng coaching cần nghiệm thu độc lập với chuyên gia.
            </p>
            <div className="grid gap-3 md:grid-cols-3">
              {data.errors.map((t) => (
                <div className="rounded-lg border border-border bg-background p-4" key={t.techniqueId}>
                  <p className="text-sm font-semibold text-foreground">
                    {t.techniqueName} <span className="text-primary">· {t.totalErrorsCount}</span>
                  </p>
                  <div className="mt-2 space-y-1">
                    {t.errorBreakdown.map((e) => (
                      <p key={e.errorType} className="flex justify-between text-xs text-muted-foreground">
                        <span>{e.errorType}</span>
                        <span className="font-mono font-semibold text-foreground">{e.count}</span>
                      </p>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </section>

          <p className="text-xs text-muted-foreground">{data.timeDefinition}</p>
        </>
      )}
    </div>
  );
}
