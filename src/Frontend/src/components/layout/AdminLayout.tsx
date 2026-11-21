import React from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { Sidebar } from './Sidebar';
import { CalendarDays, ShieldCheck } from 'lucide-react';

const today = () =>
  new Date().toLocaleDateString('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' });

export const AdminLayout: React.FC = () => {
  const { pathname } = useLocation();

  return (
    <div className="flex h-screen overflow-hidden text-foreground">
      <Sidebar />

      <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
        {/* Top bar */}
        <header className="z-10 flex h-16 items-center justify-end border-b border-border bg-card/70 px-8 backdrop-blur-md">
          <div className="flex items-center gap-3">
            <div className="hidden items-center gap-2 text-xs text-muted-foreground md:flex">
              <CalendarDays className="h-4 w-4" />
              <span className="capitalize">{today()}</span>
            </div>
            <span className="hidden h-5 w-px bg-border md:block" />
            <div className="inline-flex items-center gap-2 rounded-full border border-emerald-500/20 bg-emerald-500/10 px-3 py-1 text-xs font-semibold text-emerald-600">
              <ShieldCheck className="h-4 w-4" />
              Đã xác thực (Quản trị viên)
            </div>
          </div>
        </header>

        {/* Page */}
        <main className="flex-1 overflow-y-auto p-8">
          <div key={pathname} className="mx-auto max-w-7xl animate-fade-up space-y-8">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
};
