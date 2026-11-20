import React, { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { NavLink } from 'react-router-dom';
import {
  LayoutDashboard,
  Users,
  FolderKanban,
  Activity,
  ShieldAlert,
  BarChart3,
  ClipboardCheck,
  LogOut,
  Trophy,
  Eye,
} from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { isMockMode } from '../../api/mockMode';

/** Thời gian giữ chuột trên một mục trước khi hiện tên đầy đủ. */
const LABEL_DELAY_MS = 2000;

interface NavItem {
  name: string;
  path: string;
  icon: React.ElementType;
}

const NAV_ITEMS: NavItem[] = [
  { name: 'Tổng quan', path: '/', icon: LayoutDashboard },
  { name: 'Quản lý người dùng', path: '/users', icon: Users },
  { name: 'Nội dung (CMS)', path: '/cms', icon: FolderKanban },
  { name: 'Giám sát trận đấu', path: '/matches', icon: Activity },
  { name: 'Xử lý vi phạm', path: '/moderation', icon: ShieldAlert },
  { name: 'Thống kê & Telemetry AI', path: '/analytics', icon: BarChart3 },
  { name: 'Gán nhãn coaching', path: '/coaching', icon: ClipboardCheck },
];

/**
 * Bọc một mục của sidebar. Khi người dùng giữ chuột (hoặc focus bàn phím)
 * trên mục đó đủ LABEL_DELAY_MS, hiện tên mục dưới dạng nhãn nổi bên phải.
 * Nhãn được portal ra body để không bị cắt bởi vùng cuộn của sidebar.
 */
const DelayedLabel: React.FC<{ label: string; className?: string; children: React.ReactNode }> = ({
  label,
  className,
  children,
}) => {
  const ref = useRef<HTMLDivElement>(null);
  const timer = useRef<number | null>(null);
  const [anchor, setAnchor] = useState<{ x: number; y: number } | null>(null);

  const cancel = () => {
    if (timer.current !== null) {
      window.clearTimeout(timer.current);
      timer.current = null;
    }
  };

  const start = () => {
    cancel();
    timer.current = window.setTimeout(() => {
      const rect = ref.current?.getBoundingClientRect();
      if (rect) setAnchor({ x: rect.right + 10, y: rect.top + rect.height / 2 });
      timer.current = null;
    }, LABEL_DELAY_MS);
  };

  const stop = () => {
    cancel();
    setAnchor(null);
  };

  useEffect(() => cancel, []);

  return (
    <div ref={ref} className={className} onMouseEnter={start} onMouseLeave={stop} onFocus={start} onBlur={stop}>
      {children}
      {anchor &&
        createPortal(
          <div
            role="tooltip"
            style={{ position: 'fixed', left: anchor.x, top: anchor.y, transform: 'translateY(-50%)' }}
            className="z-50 pointer-events-none whitespace-nowrap rounded-md border border-border bg-card px-3 py-1.5 text-sm font-medium text-foreground shadow-lg"
          >
            {label}
          </div>,
          document.body,
        )}
    </div>
  );
};

export const Sidebar: React.FC = () => {
  const { user, logout } = useAuth();
  const displayName = user?.username || 'Quản trị viên';

  return (
    <aside className="w-16 bg-card border-r border-border flex flex-col h-screen sticky top-0 select-none">
      {/* Brand Header */}
      <div className="h-16 border-b border-border flex items-center justify-center">
        <DelayedLabel label="PickleVerse · Cổng quản trị">
          <div className="w-9 h-9 rounded-lg bg-gradient-to-tr from-primary to-accent flex items-center justify-center shadow-lg shadow-primary/20">
            <Trophy className="w-5 h-5 text-white" />
          </div>
        </DelayedLabel>
      </div>

      {/* Navigation Links */}
      <nav className="flex-1 py-4 px-2 space-y-1 overflow-y-auto overflow-x-hidden">
        <div className="mx-2 mb-3 border-t border-border" aria-hidden="true" />
        {NAV_ITEMS.map((item) => {
          const Icon = item.icon;
          return (
            <DelayedLabel key={item.path} label={item.name}>
              <NavLink
                to={item.path}
                aria-label={item.name}
                className={({ isActive }) =>
                  `flex items-center justify-center w-full h-10 rounded-lg transition-all duration-150 ${
                    isActive
                      ? 'bg-primary text-white shadow-sm shadow-primary/30'
                      : 'text-muted-foreground hover:bg-muted hover:text-foreground'
                  }`
                }
              >
                <Icon className="w-5 h-5 flex-shrink-0" />
              </NavLink>
            </DelayedLabel>
          );
        })}
      </nav>

      {/* Admin User Footer */}
      <div className="p-3 border-t border-border bg-background/40 flex flex-col items-center gap-3">
        <DelayedLabel label={`${displayName} · Vai trò: Quản trị viên`}>
          <div className="w-9 h-9 rounded-full bg-muted flex items-center justify-center font-bold text-sm text-foreground border border-border">
            {user?.username ? user.username.charAt(0).toUpperCase() : 'A'}
          </div>
        </DelayedLabel>

        {isMockMode ? (
          <DelayedLabel label="Chế độ xem thử — không cần đăng nhập">
            <div className="w-9 h-9 rounded-md flex items-center justify-center text-blue-700 bg-blue-500/10 border border-blue-500/20">
              <Eye className="w-4 h-4" />
            </div>
          </DelayedLabel>
        ) : (
          <DelayedLabel label="Đăng xuất">
            <button
              onClick={logout}
              aria-label="Đăng xuất"
              className="w-9 h-9 flex items-center justify-center text-rose-600 bg-rose-500/10 hover:bg-rose-500/20 rounded-md transition-colors border border-rose-500/20"
            >
              <LogOut className="w-4 h-4" />
            </button>
          </DelayedLabel>
        )}
      </div>
    </aside>
  );
};
