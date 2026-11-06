import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer } from 'recharts';
import { apiClient } from '../../../api/client';
import { AnalyticsReport } from '../../../types/api';

const today = new Date(Date.now() + 7 * 3600000).toISOString().slice(0,10);
const monthAgo = new Date(Date.now() + 7 * 3600000 - 29 * 86400000).toISOString().slice(0,10);
const rate = (value?: number | null) => value == null ? 'Chưa đủ dữ liệu' : (value * 100).toFixed(1) + '%';
const seconds = (value?: number | null) => value == null ? 'Chưa có dữ liệu' : value.toFixed(1) + ' giây';
export function Analytics() {
  const [from, setFrom] = useState(monthAgo); const [to, setTo] = useState(today);
  const report = useQuery({ queryKey: ['admin-analytics-report', from, to], queryFn: async () => (await apiClient.get<AnalyticsReport>('/api/Admin/analytics/report', { params: { from, to } })).data, enabled: !!from && !!to && from <= to });
  const data = report.data;
  return <div className="space-y-6">
    <h1 className="text-2xl font-bold">Analytics người chơi và ghép trận</h1>
    <form className="flex gap-4 items-end flex-wrap" onSubmit={e => { e.preventDefault(); report.refetch(); }}>
      <label>Từ ngày (Việt Nam)<input className="block bg-card border rounded p-2" type="date" value={from} max={to} onChange={e => setFrom(e.target.value)} required /></label>
      <label>Đến ngày<input className="block bg-card border rounded p-2" type="date" value={to} min={from} max={today} onChange={e => setTo(e.target.value)} required /></label>
      <button className="bg-primary px-4 py-2 rounded disabled:opacity-40" disabled={report.isFetching}>Làm mới</button>
    </form>
    {report.isFetching && <p>Đang tính báo cáo…</p>}{report.error && <p role="alert">{(report.error as { response?: { data?: { message?: string } } }).response?.data?.message || (report.error as Error).message}</p>}
    {data && <>
      <p className="text-sm text-muted-foreground">{data.timeZone} · {data.from} → {data.to}</p>
      <div className="grid md:grid-cols-4 gap-4">{[['Đăng ký trong khoảng', data.summary.totalUsers], ['DAU hôm nay', data.summary.activeUsersToday], ['Trận hoàn tất trong khoảng', data.summary.totalMatchesPlayed], ['Giờ hoạt động trong khoảng', (data.summary.totalPlayTimeSeconds / 3600).toFixed(2)]].map(([label,value]) => <div className="p-4 bg-card border rounded" key={label}><p>{label}</p><strong className="text-2xl">{value}</strong></div>)}</div>
      <article className="bg-card border rounded p-5"><h2 className="text-xl font-semibold">Người chơi đăng nhập theo ngày</h2><p className="text-sm my-3">{data.dauDefinition}</p><div className="h-64"><ResponsiveContainer width="100%" height="100%"><AreaChart data={data.daily}><XAxis dataKey="day" /><YAxis allowDecimals={false} /><Tooltip /><Area dataKey="players" name="Người chơi" stroke="#22d3ee" fill="#164e63" /></AreaChart></ResponsiveContainer></div></article>
      <article className="bg-card border rounded p-5 overflow-x-auto"><h2 className="text-xl font-semibold">Retention</h2><p className="text-sm my-3">{data.retentionDefinition}</p><table className="w-full text-sm text-left"><thead><tr><th>Ngày đăng nhập đầu</th><th>Số người</th><th>D1</th><th>D7</th><th>D30</th></tr></thead><tbody>{data.retention.map(c => <tr className="border-t" key={c.day}><td className="py-2">{c.day}</td><td>{c.players}</td><td>{rate(c.d1)}</td><td>{rate(c.d7)}</td><td>{rate(c.d30)}</td></tr>)}</tbody></table>{!data.retention.length && <p>Chưa có cohort trong khoảng này.</p>}</article>
      <article className="bg-card border rounded p-5 overflow-x-auto"><h2 className="text-xl font-semibold">Bài luyện tập phổ biến</h2><p className="text-sm my-3">Tỷ lệ vượt qua = số lượt đạt yêu cầu / số lượt bắt đầu. Các lượt chưa kết thúc vẫn được tính trong mẫu số.</p><table className="w-full text-sm text-left"><thead><tr><th>Bài</th><th>Người chơi</th><th>Bắt đầu</th><th>Kết thúc</th><th>Vượt qua</th><th>Tỷ lệ</th></tr></thead><tbody>{data.lessons.map(l => <tr className="border-t" key={l.lessonId}><td className="py-2">{l.title}</td><td>{l.players}</td><td>{l.starts}</td><td>{l.finished}</td><td>{l.passed}</td><td>{rate(l.completionRate)}</td></tr>)}</tbody></table></article>
      <article className="bg-card border rounded p-5"><h2 className="text-xl font-semibold">Matchmaking health</h2><p className="text-sm my-3">Nhật ký cập nhật từ hàng đợi bền. Một lượt giữ kết quả ghép đầu tiên; hủy sau khi ghép được thống kê riêng.</p><div className="grid md:grid-cols-4 gap-3">{[['Lượt tìm',data.matchmaking.searches],['Đang chờ',data.matchmaking.searching],['Ghép người thật',data.matchmaking.matched],['Bot dự phòng',data.matchmaking.botFallback],['Hủy trước ghép',data.matchmaking.cancelled],['Timeout',data.matchmaking.timedOut],['Hủy sau ghép',data.matchmaking.cancelledAfterAssignment],['Chờ trung bình',seconds(data.matchmaking.averageWaitSeconds)],['P95 chờ',seconds(data.matchmaking.p95WaitSeconds)]].map(([label,value]) => <p key={label}>{label}: <strong>{value}</strong></p>)}</div></article>
      <article className="bg-card border rounded p-5"><h2 className="text-xl font-semibold">Heatmap lỗi được hệ thống ghi nhận</h2><p className="text-sm my-3">Đây là kết quả luật phân tích; chất lượng coaching cần nghiệm thu độc lập với chuyên gia.</p><div className="grid md:grid-cols-3 gap-3">{data.errors.map(t => <div className="p-3 border rounded" key={t.techniqueId}><strong>{t.techniqueName} · {t.totalErrorsCount}</strong>{t.errorBreakdown.map(e => <p key={e.errorType}>{e.errorType}: {e.count}</p>)}</div>)}</div></article>
      <p className="text-sm text-muted-foreground">{data.timeDefinition}</p>
    </>}
  </div>;
}
