import React from 'react';
import {
  LayoutDashboard,
  Users,
  FolderKanban,
  Activity,
  ShieldAlert,
  BarChart3,
  ClipboardCheck,
} from 'lucide-react';

export interface NavItem {
  name: string;
  path: string;
  icon: React.ElementType;
  /** Tiêu đề hiển thị ở thanh trên cùng. */
  title: string;
}

export const NAV_ITEMS: NavItem[] = [
  { name: 'Tổng quan', path: '/', icon: LayoutDashboard, title: 'Tổng quan' },
  { name: 'Quản lý người dùng', path: '/users', icon: Users, title: 'Quản lý người dùng' },
  { name: 'Nội dung (CMS)', path: '/cms', icon: FolderKanban, title: 'Quản trị nội dung' },
  { name: 'Giám sát trận đấu', path: '/matches', icon: Activity, title: 'Giám sát trận đấu' },
  { name: 'Xử lý vi phạm', path: '/moderation', icon: ShieldAlert, title: 'Xử lý vi phạm' },
  { name: 'Thống kê & Telemetry AI', path: '/analytics', icon: BarChart3, title: 'Thống kê' },
  { name: 'Gán nhãn coaching', path: '/coaching', icon: ClipboardCheck, title: 'Gán nhãn coaching' },
];

export const findNavItem = (pathname: string): NavItem | undefined =>
  NAV_ITEMS.find((item) => (item.path === '/' ? pathname === '/' : pathname.startsWith(item.path)));
