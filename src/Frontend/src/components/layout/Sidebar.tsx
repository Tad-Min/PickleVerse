import React, { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { NavLink } from 'react-router-dom';
import { LogOut, Trophy, Eye } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { isMockMode } from '../../api/mockMode';
import { NAV_ITEMS } from './navigation';

/** Thời gian giữ chuột trên một mục trước khi hiện tên đầy đủ. */
const LABEL_DELAY_MS = 2000;

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
      if (rect) setAnchor({ x: rect.right + 12, y: rect.top + rect.height / 2 });
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
            className="z-50 pointer-events-none animate-fade-in whitespace-nowrap rounded-lg bg-slate-900 px-3 py-1.5 text-xs font-medium text-white shadow-lift"
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
    <aside className="sticky top-0 flex h-screen w-[72px] select-none flex-col border-r border-border bg-card/90 backdrop-blur">
      {/* Brand */}
      <div className="flex h-16 items-center justify-center">
        <DelayedLabel label="PickleVerse · Cổng quản trị">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-primary to-blue-500 shadow-glow">
            <Trophy className="h-5 w-5 text-white" />
          </div>
        </DelayedLabel>
      </div>

      {/* Navigation */}
      <nav className="flex-1 space-y-1.5 overflow-y-auto overflow-x-hidden px-3 py-3">
        {NAV_ITEMS.map((item) => {
          const Icon = item.icon;
          return (
            <DelayedLabel key={item.path} label={item.name}>
              <NavLink
                to={item.path}
                end={item.path === '/'}
                aria-label={item.name}
                className={({ isActive }) =>
                  `relative flex h-11 w-full items-center justify-center rounded-xl transition-all duration-200 ${
                    isActive
                      ? 'bg-primary text-white shadow-glow'
                      : 'text-muted-foreground hover:bg-primary-soft hover:text-primary'
                  }`
                }
              >
                <Icon className="h-5 w-5 shrink-0" />
              </NavLink>
            </DelayedLabel>
          );
        })}
      </nav>

      {/* Account */}
      <div className="flex flex-col items-center gap-3 border-t border-border px-3 py-4">
        <DelayedLabel label={`${displayName} · Vai trò: Quản trị viên`}>
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-gradient-to-br from-slate-700 to-slate-900 text-sm font-bold text-white ring-2 ring-white shadow-card">
            {user?.username ? user.username.charAt(0).toUpperCase() : 'A'}
          </div>
        </DelayedLabel>

        {isMockMode ? (
          <DelayedLabel label="Chế độ xem thử — không cần đăng nhập">
            <div className="flex h-9 w-9 items-center justify-center rounded-lg border border-blue-500/20 bg-blue-500/10 text-blue-700">
              <Eye className="h-4 w-4" />
            </div>
          </DelayedLabel>
        ) : (
          <DelayedLabel label="Đăng xuất">
            <button
              onClick={logout}
              aria-label="Đăng xuất"
              className="flex h-9 w-9 items-center justify-center rounded-lg border border-rose-500/20 bg-rose-500/10 text-rose-600 transition-colors hover:bg-rose-500/20"
            >
              <LogOut className="h-4 w-4" />
            </button>
          </DelayedLabel>
        )}
      </div>
    </aside>
  );
};
