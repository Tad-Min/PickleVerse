import React from 'react';
import { NavLink } from 'react-router-dom';
import {
  LayoutDashboard,
  Users,
  FolderKanban,
  Activity,
  ShieldAlert,
  BarChart3,
  LogOut,
  Trophy,
} from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { isMockMode } from '../../api/mockMode';

interface NavItem {
  name: string;
  path: string;
  icon: React.ElementType;
}

const NAV_ITEMS: NavItem[] = [
  { name: 'Tổng quan (Dashboard)', path: '/', icon: LayoutDashboard },
  { name: 'Quản lý người dùng', path: '/users', icon: Users },
  { name: 'Nội dung (CMS)', path: '/cms', icon: FolderKanban },
  { name: 'Giám sát trận đấu', path: '/matches', icon: Activity },
  { name: 'Xử lý vi phạm', path: '/moderation', icon: ShieldAlert },
  { name: 'Thống kê & AI Telemetry', path: '/analytics', icon: BarChart3 },
];

export const Sidebar: React.FC = () => {
  const { user, logout } = useAuth();

  return (
    <aside className="w-64 bg-card border-r border-border flex flex-col h-screen sticky top-0 select-none">
      {/* Brand Header */}
      <div className="h-16 border-b border-border flex items-center px-6 gap-3">
        <div className="w-9 h-9 rounded-lg bg-gradient-to-tr from-primary to-accent flex items-center justify-center shadow-lg shadow-primary/20">
          <Trophy className="w-5 h-5 text-white" />
        </div>
        <div>
          <h1 className="text-base font-bold text-foreground leading-tight tracking-tight">PickleVerse</h1>
          <span className="text-xs text-accent font-medium uppercase tracking-wider">Admin Portal</span>
        </div>
      </div>

      {/* Navigation Links */}
      <nav className="flex-1 py-4 px-3 space-y-1 overflow-y-auto">
        <div className="px-3 pb-2 text-[11px] font-semibold text-muted-foreground uppercase tracking-wider">
          Vận hành & Giám sát
        </div>
        {NAV_ITEMS.map((item) => {
          const Icon = item.icon;
          return (
            <NavLink
              key={item.path}
              to={item.path}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-150 ${
                  isActive
                    ? 'bg-primary text-white shadow-sm shadow-primary/30 font-semibold'
                    : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                }`
              }
            >
              <Icon className="w-4 h-4 flex-shrink-0" />
              <span>{item.name}</span>
            </NavLink>
          );
        })}
      </nav>

      {/* Admin User Footer */}
      <div className="p-4 border-t border-border bg-slate-900/40">
        <div className="flex items-center gap-3 mb-3">
          <div className="w-9 h-9 rounded-full bg-slate-700 flex items-center justify-center font-bold text-sm text-foreground border border-slate-600">
            {user?.username ? user.username.charAt(0).toUpperCase() : 'A'}
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-xs font-semibold text-foreground truncate">{user?.username || 'Administrator'}</p>
            <span className="inline-flex items-center px-1.5 py-0.2 text-[10px] font-medium rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
              Role: Admin
            </span>
          </div>
        </div>

        {isMockMode ? (
          <div className="w-full text-center px-3 py-2 text-[11px] font-medium text-cyan-300 bg-cyan-500/10 rounded-md border border-cyan-500/20">
            Chế độ xem thử — không cần đăng nhập
          </div>
        ) : (
          <button
            onClick={logout}
            className="w-full flex items-center justify-center gap-2 px-3 py-2 text-xs font-medium text-rose-400 bg-rose-500/10 hover:bg-rose-500/20 rounded-md transition-colors border border-rose-500/20"
          >
            <LogOut className="w-3.5 h-3.5" />
            <span>Đăng xuất</span>
          </button>
        )}
      </div>
    </aside>
  );
};
