import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import {
  Users,
  UserCheck,
  Trophy,
  Clock,
  ShieldAlert,
  ArrowUpRight,
  Activity,
  Layers,
  Sparkles,
  TrendingUp,
} from 'lucide-react';
import { adminApi } from '../../../api/adminApi';
import { useAuth } from '../../../context/AuthContext';
import { Skeleton } from '../../../shared/ui/Skeleton';

const formatHours = (seconds: number) => `${(seconds / 3600).toFixed(1)} giờ`;

interface StatCardProps {
  label: string;
  value: React.ReactNode;
  hint: React.ReactNode;
  icon: React.ElementType;
  tone: string; // lớp màu cho ô icon
  loading: boolean;
}

const StatCard: React.FC<StatCardProps> = ({ label, value, hint, icon: Icon, tone, loading }) => (
  <div className="group relative overflow-hidden rounded-2xl border border-border bg-card p-5 shadow-card transition-all duration-200 hover:-translate-y-0.5 hover:shadow-lift">
    <div className="mb-4 flex items-center justify-between">
      <span className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">{label}</span>
      <div className={`flex h-9 w-9 items-center justify-center rounded-xl ${tone}`}>
        <Icon className="h-[18px] w-[18px]" />
      </div>
    </div>
    {loading ? (
      <Skeleton className="h-8 w-24" />
    ) : (
      <div className="tabular font-display text-3xl font-bold tracking-tight text-foreground">{value}</div>
    )}
    <div className="mt-2 flex items-center gap-1.5 text-xs text-muted-foreground">{hint}</div>
  </div>
);

interface QuickLinkProps {
  to: string;
  title: string;
  description: string;
  icon: React.ElementType;
  tone: string;
}

const QuickLink: React.FC<QuickLinkProps> = ({ to, title, description, icon: Icon, tone }) => (
  <Link
    to={to}
    className="group relative overflow-hidden rounded-2xl border border-border bg-card p-6 shadow-card transition-all duration-200 hover:-translate-y-0.5 hover:border-primary/30 hover:shadow-lift"
  >
    <div className={`mb-5 flex h-11 w-11 items-center justify-center rounded-xl transition-transform duration-200 group-hover:scale-110 ${tone}`}>
      <Icon className="h-5 w-5" />
    </div>
    <div className="flex items-center justify-between gap-3">
      <h3 className="font-display text-base font-semibold text-foreground">{title}</h3>
      <ArrowUpRight className="h-4 w-4 text-muted-foreground transition-all duration-200 group-hover:translate-x-0.5 group-hover:-translate-y-0.5 group-hover:text-primary" />
    </div>
    <p className="mt-2 text-xs leading-relaxed text-muted-foreground">{description}</p>
  </Link>
);

export const Dashboard: React.FC = () => {
  const { user } = useAuth();
  const { data: summary, isLoading, isError } = useQuery({
    queryKey: ['admin-analytics-summary'],
    queryFn: () => adminApi.getAnalyticsSummary(),
  });

  const pending = summary?.totalReportsPending || 0;
  const name = user?.username || 'Quản trị viên';

  return (
    <div className="space-y-8">
      {/* Hero */}
      <section className="relative overflow-hidden rounded-3xl bg-gradient-to-br from-primary via-blue-600 to-indigo-600 p-8 text-white shadow-glow">
        <div className="pointer-events-none absolute -right-16 -top-20 h-64 w-64 rounded-full bg-white/10 blur-2xl" />
        <div className="pointer-events-none absolute -bottom-24 left-1/3 h-56 w-56 rounded-full bg-indigo-300/20 blur-3xl" />
        <div className="relative flex flex-col gap-6 md:flex-row md:items-center md:justify-between">
          <div>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-white/15 px-3 py-1 text-[11px] font-semibold uppercase tracking-wider backdrop-blur">
              <Sparkles className="h-3.5 w-3.5" />
              Bảng điều khiển vận hành
            </span>
            <h1 className="mt-4 font-display text-3xl font-bold tracking-tight">Xin chào, {name}</h1>
            <p className="mt-2 max-w-xl text-sm text-blue-100">
              Tổng quan chỉ số người chơi, phiên thi đấu và an ninh mạng PickleVerse trong một màn hình.
            </p>
          </div>
          <div className="flex flex-wrap gap-3">
            <Link
              to="/moderation"
              className="inline-flex items-center gap-2 rounded-xl bg-white px-4 py-2.5 text-xs font-semibold text-primary shadow-card transition-transform hover:-translate-y-0.5"
            >
              <ShieldAlert className="h-4 w-4" />
              {pending > 0 ? `Xử lý ${pending} báo cáo` : 'Xem xử lý vi phạm'}
            </Link>
            <Link
              to="/analytics"
              className="inline-flex items-center gap-2 rounded-xl border border-white/30 bg-white/10 px-4 py-2.5 text-xs font-semibold text-white backdrop-blur transition-colors hover:bg-white/20"
            >
              <TrendingUp className="h-4 w-4" />
              Xem thống kê
            </Link>
          </div>
        </div>
      </section>

      {/* Metrics */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <StatCard
          label="Tổng người dùng"
          icon={Users}
          tone="bg-blue-500/10 text-blue-600"
          loading={isLoading}
          value={(summary?.totalUsers || 0).toLocaleString()}
          hint={<><span className="font-medium text-emerald-600">Đã đăng ký</span> tài khoản</>}
        />
        <StatCard
          label="Hoạt động hôm nay"
          icon={UserCheck}
          tone="bg-emerald-500/10 text-emerald-600"
          loading={isLoading}
          value={(summary?.activeUsersToday || 0).toLocaleString()}
          hint={<><TrendingUp className="h-3.5 w-3.5 text-emerald-600" />Người chơi hoạt động (DAU)</>}
        />
        <StatCard
          label="Trận đã đấu"
          icon={Trophy}
          tone="bg-amber-500/10 text-amber-600"
          loading={isLoading}
          value={(summary?.totalMatchesPlayed || 0).toLocaleString()}
          hint="Đơn & Đôi"
        />
        <StatCard
          label="Thời gian thi đấu"
          icon={Clock}
          tone="bg-indigo-500/10 text-indigo-600"
          loading={isLoading}
          value={formatHours(summary?.totalPlayTimeSeconds || 0)}
          hint="Tổng thời lượng online"
        />
        <StatCard
          label="Báo cáo chờ duyệt"
          icon={ShieldAlert}
          tone="bg-rose-500/10 text-rose-600"
          loading={isLoading}
          value={pending.toLocaleString()}
          hint={
            pending > 0 ? (
              <span className="font-semibold text-rose-600">Cần xử lý ngay</span>
            ) : (
              <span className="text-emerald-600">Không có vi phạm</span>
            )
          }
        />
      </div>

      {isError && (
        <div className="rounded-2xl border border-rose-500/20 bg-rose-500/10 p-4 text-xs text-rose-600">
          Không thể kết nối tới Backend API để lấy số liệu tổng quan. Vui lòng đảm bảo Backend API (ASP.NET Core) đang chạy.
        </div>
      )}

      {/* Quick links */}
      <div>
        <h2 className="mb-4 font-display text-lg font-bold text-foreground">Truy cập nhanh</h2>
        <div className="grid grid-cols-1 gap-5 md:grid-cols-3">
          <QuickLink
            to="/users"
            title="Quản lý Người dùng"
            icon={Users}
            tone="bg-primary/10 text-primary"
            description="Tìm kiếm tài khoản, phân quyền quản trị, khóa/mở khóa tài khoản có thời hạn, kích hoạt email và reset điểm Elo."
          />
          <QuickLink
            to="/cms"
            title="Hệ thống Nội dung"
            icon={Layers}
            tone="bg-indigo-500/10 text-indigo-600"
            description="Tạo mới bài học, bài tập kỹ thuật, đăng bán vợt/trang phục vào cửa hàng và thiết lập nhiệm vụ hàng ngày."
          />
          <QuickLink
            to="/analytics"
            title="Bản đồ nhiệt lỗi kỹ thuật AI"
            icon={Activity}
            tone="bg-emerald-500/10 text-emerald-600"
            description="Trực quan hóa ma trận lỗi từ engine AI: giao bóng trễ, Dink quá cao, phạm luật vùng cấm để điều chỉnh giáo trình đào tạo."
          />
        </div>
      </div>
    </div>
  );
};
