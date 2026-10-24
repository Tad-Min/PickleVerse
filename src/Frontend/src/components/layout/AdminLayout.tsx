import React from 'react';
import { Outlet } from 'react-router-dom';
import { Sidebar } from './Sidebar';
import { ShieldCheck, Bell } from 'lucide-react';

export const AdminLayout: React.FC = () => {
  return (
    <div className="flex h-screen bg-background overflow-hidden text-foreground">
      {/* Sidebar */}
      <Sidebar />

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
        {/* Top Navbar */}
        <header className="h-16 border-b border-border bg-card/60 backdrop-blur-md px-8 flex items-center justify-between z-10">
          <div className="flex items-center gap-2 text-xs text-muted-foreground">
            <ShieldCheck className="w-4 h-4 text-emerald-400" />
            <span>Môi trường bảo mật:</span>
            <span className="font-semibold text-emerald-400">Authenticated (Role: Admin)</span>
          </div>

          <div className="flex items-center gap-4">
            <button
              title="Thông báo hệ thống"
              className="p-2 text-muted-foreground hover:text-foreground hover:bg-muted rounded-lg transition-colors relative"
            >
              <Bell className="w-4 h-4" />
              <span className="absolute top-1.5 right-1.5 w-2 h-2 bg-accent rounded-full animate-pulse"></span>
            </button>
            <div className="h-4 w-[1px] bg-border"></div>
            <div className="text-xs text-muted-foreground">
              Server API: <span className="text-foreground font-mono text-[11px]">http://localhost:8080</span>
            </div>
          </div>
        </header>

        {/* Dynamic Page View */}
        <main className="flex-1 overflow-y-auto p-8">
          <div className="max-w-7xl mx-auto space-y-8">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
};
