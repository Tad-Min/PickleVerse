import React from 'react';
import { useQuery } from '@tanstack/react-query';
import { adminApi } from '../../../api/adminApi';
import {
  Users,
  UserCheck,
  Trophy,
  Clock,
  ShieldAlert,
  ArrowUpRight,
  TrendingUp,
  Activity,
  Layers,
} from 'lucide-react';
import { Link } from 'react-router-dom';

export const Dashboard: React.FC = () => {
  const { data: summary, isLoading, isError, refetch } = useQuery({
    queryKey: ['admin-analytics-summary'],
    queryFn: () => adminApi.getAnalyticsSummary(),
  });

  const formatHours = (seconds: number) => {
    const hours = (seconds / 3600).toFixed(1);
    return `${hours} giờ`;
  };

  return (
    <div className="space-y-8">
      {/* Title & Refresh */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold font-display text-foreground tracking-tight">
            Bảng điều khiển Vận hành
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Tổng quan chỉ số người chơi, phiên thi đấu và an ninh mạng PickleVerse
          </p>
        </div>

        <button
          onClick={() => refetch()}
          className="inline-flex items-center gap-2 px-4 py-2 bg-slate-800 hover:bg-slate-700 border border-border rounded-lg text-xs font-semibold text-foreground transition-colors self-start sm:self-auto"
        >
          <Activity className="w-3.5 h-3.5 text-accent" />
          <span>Làm mới số liệu</span>
        </button>
      </div>

      {/* 5 Metric Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
        {/* Total Users */}
        <div className="bg-card border border-border rounded-xl p-5 shadow-sm relative overflow-hidden group hover:border-slate-600 transition-all">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Tổng người dùng</span>
            <div className="w-8 h-8 rounded-lg bg-blue-500/10 text-blue-400 flex items-center justify-center">
              <Users className="w-4 h-4" />
            </div>
          </div>
          <div className="text-2xl font-bold font-display text-foreground">
            {isLoading ? '...' : (summary?.totalUsers || 0).toLocaleString()}
          </div>
          <div className="mt-2 text-xs text-muted-foreground flex items-center gap-1">
            <span className="text-emerald-400 font-medium">Đã đăng ký</span> tài khoản
          </div>
        </div>

        {/* Active Today */}
        <div className="bg-card border border-border rounded-xl p-5 shadow-sm relative overflow-hidden group hover:border-slate-600 transition-all">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Hoạt động hôm nay</span>
            <div className="w-8 h-8 rounded-lg bg-emerald-500/10 text-emerald-400 flex items-center justify-center">
              <UserCheck className="w-4 h-4" />
            </div>
          </div>
          <div className="text-2xl font-bold font-display text-foreground">
            {isLoading ? '...' : (summary?.activeUsersToday || 0).toLocaleString()}
          </div>
          <div className="mt-2 text-xs text-muted-foreground flex items-center gap-1">
            <TrendingUp className="w-3.5 h-3.5 text-emerald-400" />
            <span>DAU ghi nhận</span>
          </div>
        </div>

        {/* Total Matches */}
        <div className="bg-card border border-border rounded-xl p-5 shadow-sm relative overflow-hidden group hover:border-slate-600 transition-all">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Trận đã đấu</span>
            <div className="w-8 h-8 rounded-lg bg-amber-500/10 text-amber-400 flex items-center justify-center">
              <Trophy className="w-4 h-4" />
            </div>
          </div>
          <div className="text-2xl font-bold font-display text-foreground">
            {isLoading ? '...' : (summary?.totalMatchesPlayed || 0).toLocaleString()}
          </div>
          <div className="mt-2 text-xs text-muted-foreground flex items-center gap-1">
            <span>Singles & Doubles</span>
          </div>
        </div>

        {/* Total Play Time */}
        <div className="bg-card border border-border rounded-xl p-5 shadow-sm relative overflow-hidden group hover:border-slate-600 transition-all">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Thời gian thi đấu</span>
            <div className="w-8 h-8 rounded-lg bg-indigo-500/10 text-indigo-400 flex items-center justify-center">
              <Clock className="w-4 h-4" />
            </div>
          </div>
          <div className="text-2xl font-bold font-display text-foreground">
            {isLoading ? '...' : formatHours(summary?.totalPlayTimeSeconds || 0)}
          </div>
          <div className="mt-2 text-xs text-muted-foreground flex items-center gap-1">
            <span>Tổng thời lượng online</span>
          </div>
        </div>

        {/* Pending Reports */}
        <div className="bg-card border border-border rounded-xl p-5 shadow-sm relative overflow-hidden group hover:border-slate-600 transition-all">
          <div className="flex items-center justify-between mb-3">
            <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Báo cáo chờ duyệt</span>
            <div className="w-8 h-8 rounded-lg bg-rose-500/10 text-rose-400 flex items-center justify-center">
              <ShieldAlert className="w-4 h-4" />
            </div>
          </div>
          <div className="text-2xl font-bold font-display text-foreground">
            {isLoading ? '...' : (summary?.totalReportsPending || 0).toLocaleString()}
          </div>
          <div className="mt-2 text-xs text-muted-foreground flex items-center gap-1">
            {summary?.totalReportsPending && summary.totalReportsPending > 0 ? (
              <span className="text-rose-400 font-semibold">Cần xử lý ngay</span>
            ) : (
              <span className="text-emerald-400">Không có vi phạm</span>
            )}
          </div>
        </div>
      </div>

      {isError && (
        <div className="p-4 rounded-xl bg-rose-500/10 border border-rose-500/20 text-rose-400 text-xs">
          Không thể kết nối tới Backend API để lấy số liệu tổng quan. Vui lòng đảm bảo Backend API (ASP.NET Core) đang chạy trên cổng 5000.
        </div>
      )}

      {/* Quick Navigation Panels */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <Link
          to="/users"
          className="bg-card border border-border rounded-xl p-6 hover:border-primary/50 transition-all group"
        >
          <div className="w-10 h-10 rounded-lg bg-primary/10 text-primary flex items-center justify-center mb-4 group-hover:scale-110 transition-transform">
            <Users className="w-5 h-5" />
          </div>
          <div className="flex items-center justify-between">
            <h3 className="text-base font-semibold text-foreground font-display">Quản lý Người dùng & RBAC</h3>
            <ArrowUpRight className="w-4 h-4 text-muted-foreground group-hover:text-primary transition-colors" />
          </div>
          <p className="text-xs text-muted-foreground mt-2">
            Tìm kiếm tài khoản, phân quyền quản trị, khóa/mở khóa tài khoản có thời hạn, kích hoạt email và reset điểm Elo.
          </p>
        </Link>

        <Link
          to="/cms"
          className="bg-card border border-border rounded-xl p-6 hover:border-accent/50 transition-all group"
        >
          <div className="w-10 h-10 rounded-lg bg-accent/10 text-accent flex items-center justify-center mb-4 group-hover:scale-110 transition-transform">
            <Layers className="w-5 h-5" />
          </div>
          <div className="flex items-center justify-between">
            <h3 className="text-base font-semibold text-foreground font-display">Hệ thống Nội dung (CMS)</h3>
            <ArrowUpRight className="w-4 h-4 text-muted-foreground group-hover:text-accent transition-colors" />
          </div>
          <p className="text-xs text-muted-foreground mt-2">
            Tạo mới bài học (Lessons), bài tập kỹ thuật (Drills), đăng bán vợt/trang phục vào Shop và thiết lập nhiệm vụ hàng ngày.
          </p>
        </Link>

        <Link
          to="/analytics"
          className="bg-card border border-border rounded-xl p-6 hover:border-emerald-500/50 transition-all group"
        >
          <div className="w-10 h-10 rounded-lg bg-emerald-500/10 text-emerald-400 flex items-center justify-center mb-4 group-hover:scale-110 transition-transform">
            <Activity className="w-5 h-5" />
          </div>
          <div className="flex items-center justify-between">
            <h3 className="text-base font-semibold text-foreground font-display">Bản đồ nhiệt lỗi kỹ thuật AI</h3>
            <ArrowUpRight className="w-4 h-4 text-muted-foreground group-hover:text-emerald-400 transition-colors" />
          </div>
          <p className="text-xs text-muted-foreground mt-2">
            Trực quan hóa ma trận lỗi từ engine AI: Serve trễ, Dink quá cao, phạm luật Kitchen để điều chỉnh giáo trình đào tạo.
          </p>
        </Link>
      </div>
    </div>
  );
};
