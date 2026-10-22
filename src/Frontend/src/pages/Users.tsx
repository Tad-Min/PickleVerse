import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { adminApi } from '../api/adminApi';
import { AdminUserDto } from '../types/api';
import {
  Search,
  UserCheck,
  ShieldAlert,
  RotateCcw,
  Shield,
  CheckCircle2,
  X,
  ChevronLeft,
  ChevronRight,
  Filter,
} from 'lucide-react';

export const Users: React.FC = () => {
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<string>('');
  const [page, setPage] = useState(1);
  const pageSize = 15;

  const queryClient = useQueryClient();

  // Selected user for modal actions
  const [selectedUser, setSelectedUser] = useState<AdminUserDto | null>(null);
  const [activeModal, setActiveModal] = useState<'ROLE' | 'BAN' | 'RESET_ELO' | null>(null);

  // Form states for modals
  const [newRoleId, setNewRoleId] = useState<number>(1);
  const [banReason, setBanReason] = useState<string>('');
  const [bannedUntil, setBannedUntil] = useState<string>('');
  const [defaultElo, setDefaultElo] = useState<number>(2000);
  const [actionMessage, setActionMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  // Query users
  const { data: users = [], isLoading, isFetching } = useQuery({
    queryKey: ['admin-users', search, status, page, pageSize],
    queryFn: () => adminApi.getUsers(search || undefined, status || undefined, page, pageSize),
  });

  // Mutations
  const assignRoleMutation = useMutation({
    mutationFn: (data: { userId: string; roleId: number }) => adminApi.assignRole(data),
    onSuccess: (res) => {
      setActionMessage({ type: 'success', text: res.message || 'Gán vai trò thành công!' });
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      closeModal();
    },
    onError: () => {
      setActionMessage({ type: 'error', text: 'Gán vai trò thất bại. Vui lòng thử lại.' });
    },
  });

  const banMutation = useMutation({
    mutationFn: (data: { userId: string; reason: string; bannedUntil?: string | null }) => adminApi.banUser(data),
    onSuccess: (res) => {
      setActionMessage({ type: 'success', text: res.message || 'Khóa tài khoản thành công!' });
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      closeModal();
    },
    onError: () => {
      setActionMessage({ type: 'error', text: 'Khóa tài khoản thất bại.' });
    },
  });

  const unbanMutation = useMutation({
    mutationFn: (userId: string) => adminApi.unbanUser(userId),
    onSuccess: (res) => {
      setActionMessage({ type: 'success', text: res.message || 'Mở khóa tài khoản thành công!' });
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
    },
    onError: () => {
      setActionMessage({ type: 'error', text: 'Mở khóa tài khoản thất bại.' });
    },
  });

  const resetEloMutation = useMutation({
    mutationFn: (data: { playerId: string; defaultElo: number }) =>
      adminApi.resetPlayerElo(data.playerId, data.defaultElo),
    onSuccess: (res) => {
      setActionMessage({ type: 'success', text: res.message || 'Reset Elo thành công!' });
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      closeModal();
    },
    onError: () => {
      setActionMessage({ type: 'error', text: 'Reset Elo thất bại.' });
    },
  });

  const verifyEmailMutation = useMutation({
    mutationFn: (userId: string) => adminApi.manualVerifyEmail(userId),
    onSuccess: (res) => {
      setActionMessage({ type: 'success', text: res.message || 'Kích hoạt tài khoản thành công!' });
      queryClient.invalidateQueries({ queryKey: ['admin-users'] });
    },
    onError: () => {
      setActionMessage({ type: 'error', text: 'Kích hoạt tài khoản thất bại.' });
    },
  });

  const closeModal = () => {
    setActiveModal(null);
    setSelectedUser(null);
    setBanReason('');
    setBannedUntil('');
  };

  const handleOpenRoleModal = (user: AdminUserDto) => {
    setSelectedUser(user);
    setNewRoleId(user.roleId || 1);
    setActiveModal('ROLE');
  };

  const handleOpenBanModal = (user: AdminUserDto) => {
    setSelectedUser(user);
    setBanReason('');
    setBannedUntil('');
    setActiveModal('BAN');
  };

  const handleOpenResetEloModal = (user: AdminUserDto) => {
    setSelectedUser(user);
    setDefaultElo(2000);
    setActiveModal('RESET_ELO');
  };

  return (
    <div className="space-y-6">
      {/* Title & Feedback */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold font-display text-foreground tracking-tight">Quản lý Người dùng</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Tra cứu tài khoản, phân quyền Role (RBAC), kích hoạt thủ công và xử lý kỷ luật
          </p>
        </div>
      </div>

      {/* Action Notification Banner */}
      {actionMessage && (
        <div
          className={`p-3.5 rounded-lg flex items-center justify-between text-xs font-medium border ${
            actionMessage.type === 'success'
              ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20'
              : 'bg-rose-500/10 text-rose-400 border-rose-500/20'
          }`}
        >
          <span>{actionMessage.text}</span>
          <button onClick={() => setActionMessage(null)} className="p-1 hover:opacity-80">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {/* Filters & Search Toolbar */}
      <div className="bg-card border border-border rounded-xl p-4 flex flex-col md:flex-row items-center justify-between gap-4">
        {/* Search Input */}
        <div className="relative w-full md:w-96">
          <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-muted-foreground">
            <Search className="w-4 h-4" />
          </div>
          <input
            type="text"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            placeholder="Tìm theo tên người chơi, email..."
            className="w-full pl-9 pr-4 py-2 bg-slate-900 border border-border rounded-lg text-xs text-foreground placeholder:text-slate-600 focus:outline-none focus:ring-2 focus:ring-primary focus:border-transparent transition-all"
          />
        </div>

        {/* Status Filter Dropdown */}
        <div className="flex items-center gap-2 w-full md:w-auto">
          <Filter className="w-4 h-4 text-muted-foreground flex-shrink-0" />
          <select
            value={status}
            onChange={(e) => {
              setStatus(e.target.value);
              setPage(1);
            }}
            className="bg-slate-900 border border-border rounded-lg text-xs text-foreground px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary"
          >
            <option value="">Tất cả trạng thái</option>
            <option value="ACTIVE">ACTIVE (Đang hoạt động)</option>
            <option value="BANNED">BANNED (Bị khóa)</option>
            <option value="UNVERIFIED">UNVERIFIED (Chưa kích hoạt)</option>
          </select>
        </div>
      </div>

      {/* Users Table */}
      <div className="bg-card border border-border rounded-xl shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-900/60 border-b border-border text-muted-foreground uppercase tracking-wider text-[11px] font-semibold">
              <tr>
                <th className="py-3.5 px-4">Người chơi / Email</th>
                <th className="py-3.5 px-4">Vai trò (Role)</th>
                <th className="py-3.5 px-4">Trạng thái</th>
                <th className="py-3.5 px-4">Điểm Elo</th>
                <th className="py-3.5 px-4">Thời gian chơi</th>
                <th className="py-3.5 px-4">Ngày tạo</th>
                <th className="py-3.5 px-4 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {isLoading ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-muted-foreground">
                    <div className="inline-block w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin mb-2"></div>
                    <div>Đang tải danh sách người dùng...</div>
                  </td>
                </tr>
              ) : users.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-muted-foreground">
                    Không tìm thấy người dùng phù hợp với bộ lọc.
                  </td>
                </tr>
              ) : (
                users.map((u) => {
                  const isBanned = u.status === 'BANNED';
                  const isUnverified = u.status === 'UNVERIFIED';
                  const isAdminRole = u.roleName === 'Admin' || u.roleId === 2;

                  return (
                    <tr key={u.userId} className="hover:bg-slate-800/50 transition-colors">
                      <td className="py-3 px-4">
                        <div className="font-semibold text-foreground">{u.playerName || 'Chưa đặt tên'}</div>
                        <div className="text-[11px] text-muted-foreground font-mono">{u.email}</div>
                      </td>

                      <td className="py-3 px-4">
                        <span
                          className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium border ${
                            isAdminRole
                              ? 'bg-indigo-500/10 text-indigo-400 border-indigo-500/20'
                              : 'bg-slate-700/30 text-slate-300 border-slate-700'
                          }`}
                        >
                          {isAdminRole ? 'Admin' : 'Player'}
                        </span>
                      </td>

                      <td className="py-3 px-4">
                        <span
                          className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium border ${
                            isBanned
                              ? 'bg-rose-500/10 text-rose-400 border-rose-500/20'
                              : isUnverified
                              ? 'bg-amber-500/10 text-amber-400 border-amber-500/20'
                              : 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20'
                          }`}
                        >
                          {u.status}
                        </span>
                      </td>

                      <td className="py-3 px-4 font-mono font-bold text-accent">{u.elo || 2000}</td>

                      <td className="py-3 px-4 text-muted-foreground font-mono">
                        {(u.timePlayedSeconds / 60).toFixed(0)} phút
                      </td>

                      <td className="py-3 px-4 text-muted-foreground text-[11px]">
                        {new Date(u.createdAt).toLocaleDateString('vi-VN')}
                      </td>

                      <td className="py-3 px-4 text-right">
                        <div className="flex items-center justify-end gap-1.5">
                          {/* Role Assign */}
                          <button
                            onClick={() => handleOpenRoleModal(u)}
                            title="Gán vai trò (Role)"
                            className="p-1.5 rounded hover:bg-slate-700 text-muted-foreground hover:text-foreground transition-colors"
                          >
                            <Shield className="w-3.5 h-3.5" />
                          </button>

                          {/* Verify Email (if unverified) */}
                          {isUnverified && (
                            <button
                              onClick={() => verifyEmailMutation.mutate(u.userId)}
                              title="Kích hoạt tài khoản thủ công"
                              className="p-1.5 rounded hover:bg-emerald-500/20 text-emerald-400 transition-colors"
                            >
                              <CheckCircle2 className="w-3.5 h-3.5" />
                            </button>
                          )}

                          {/* Reset Elo */}
                          <button
                            onClick={() => handleOpenResetEloModal(u)}
                            title="Reset điểm Elo"
                            className="p-1.5 rounded hover:bg-amber-500/20 text-amber-400 transition-colors"
                          >
                            <RotateCcw className="w-3.5 h-3.5" />
                          </button>

                          {/* Ban / Unban */}
                          {isBanned ? (
                            <button
                              onClick={() => unbanMutation.mutate(u.userId)}
                              title="Mở khóa tài khoản (Unban)"
                              className="p-1.5 rounded bg-emerald-500/10 hover:bg-emerald-500/20 text-emerald-400 border border-emerald-500/20 transition-colors"
                            >
                              <UserCheck className="w-3.5 h-3.5" />
                            </button>
                          ) : (
                            <button
                              onClick={() => handleOpenBanModal(u)}
                              title="Khóa tài khoản (Ban)"
                              className="p-1.5 rounded bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 border border-rose-500/20 transition-colors"
                            >
                              <ShieldAlert className="w-3.5 h-3.5" />
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination Bar */}
        <div className="p-4 border-t border-border flex items-center justify-between text-xs text-muted-foreground">
          <div>Trang hiện tại: <span className="font-semibold text-foreground">{page}</span></div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1 || isFetching}
              className="p-1.5 rounded bg-slate-900 border border-border hover:bg-slate-800 disabled:opacity-40 disabled:cursor-not-allowed text-foreground"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
            <button
              onClick={() => setPage((p) => p + 1)}
              disabled={users.length < pageSize || isFetching}
              className="p-1.5 rounded bg-slate-900 border border-border hover:bg-slate-800 disabled:opacity-40 disabled:cursor-not-allowed text-foreground"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>

      {/* MODAL 1: Role Assignment */}
      {activeModal === 'ROLE' && selectedUser && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="w-full max-w-sm bg-card border border-border rounded-xl p-6 shadow-2xl relative">
            <h3 className="text-base font-bold text-foreground font-display mb-2">Gán Vai trò (RBAC)</h3>
            <p className="text-xs text-muted-foreground mb-4">
              Người dùng: <span className="font-semibold text-foreground">{selectedUser.playerName}</span> ({selectedUser.email})
            </p>

            <div className="space-y-3 mb-6">
              <label className="flex items-center gap-3 p-3 rounded-lg border border-border bg-slate-900/50 cursor-pointer hover:border-primary transition-all">
                <input
                  type="radio"
                  name="role"
                  value={1}
                  checked={newRoleId === 1}
                  onChange={() => setNewRoleId(1)}
                  className="text-primary focus:ring-primary"
                />
                <div>
                  <div className="text-xs font-semibold text-foreground">Role: Player (Người chơi)</div>
                  <div className="text-[11px] text-muted-foreground">Chỉ truy cập game client, không thể vào trang quản trị.</div>
                </div>
              </label>

              <label className="flex items-center gap-3 p-3 rounded-lg border border-border bg-slate-900/50 cursor-pointer hover:border-primary transition-all">
                <input
                  type="radio"
                  name="role"
                  value={2}
                  checked={newRoleId === 2}
                  onChange={() => setNewRoleId(2)}
                  className="text-primary focus:ring-primary"
                />
                <div>
                  <div className="text-xs font-semibold text-foreground">Role: Admin (Quản trị viên)</div>
                  <div className="text-[11px] text-muted-foreground">Toàn quyền truy cập và thao tác trên Admin Portal.</div>
                </div>
              </label>
            </div>

            <div className="flex items-center justify-end gap-2">
              <button
                onClick={closeModal}
                className="px-3 py-1.5 text-xs text-muted-foreground hover:text-foreground"
              >
                Hủy bỏ
              </button>
              <button
                onClick={() => assignRoleMutation.mutate({ userId: selectedUser.userId, roleId: newRoleId })}
                disabled={assignRoleMutation.isPending}
                className="px-4 py-1.5 text-xs font-semibold bg-primary hover:bg-primary-hover text-white rounded-lg shadow-sm"
              >
                {assignRoleMutation.isPending ? 'Đang lưu...' : 'Xác nhận gán'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 2: Ban User */}
      {activeModal === 'BAN' && selectedUser && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="w-full max-w-md bg-card border border-border rounded-xl p-6 shadow-2xl relative">
            <h3 className="text-base font-bold text-foreground font-display mb-1 text-rose-400">Khóa tài khoản người chơi</h3>
            <p className="text-xs text-muted-foreground mb-4">
              Khóa tài khoản: <span className="font-semibold text-foreground">{selectedUser.playerName}</span> ({selectedUser.email})
            </p>

            <div className="space-y-4 mb-6">
              <div>
                <label className="block text-xs font-semibold text-foreground mb-1.5">
                  Lý do vi phạm kỷ luật <span className="text-rose-400">*</span>
                </label>
                <textarea
                  value={banReason}
                  onChange={(e) => setBanReason(e.target.value)}
                  placeholder="Ví dụ: Gian lận điểm Elo, sử dụng ngôn từ kích động, AFK liên tục..."
                  rows={3}
                  className="w-full p-2.5 bg-slate-900 border border-border rounded-lg text-xs text-foreground placeholder:text-slate-600 focus:outline-none focus:ring-2 focus:ring-rose-500"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-foreground mb-1.5">
                  Thời hạn khóa (Bỏ trống nếu muốn khóa vĩnh viễn)
                </label>
                <input
                  type="datetime-local"
                  value={bannedUntil}
                  onChange={(e) => setBannedUntil(e.target.value)}
                  className="w-full p-2 bg-slate-900 border border-border rounded-lg text-xs text-foreground focus:outline-none focus:ring-2 focus:ring-primary"
                />
              </div>
            </div>

            <div className="flex items-center justify-end gap-2">
              <button onClick={closeModal} className="px-3 py-1.5 text-xs text-muted-foreground hover:text-foreground">
                Hủy bỏ
              </button>
              <button
                onClick={() =>
                  banMutation.mutate({
                    userId: selectedUser.userId,
                    reason: banReason || 'Vi phạm điều khoản cộng đồng PickleVerse.',
                    bannedUntil: bannedUntil ? new Date(bannedUntil).toISOString() : null,
                  })
                }
                disabled={banMutation.isPending || !banReason.trim()}
                className="px-4 py-1.5 text-xs font-semibold bg-rose-600 hover:bg-rose-500 text-white rounded-lg shadow-sm disabled:opacity-50"
              >
                {banMutation.isPending ? 'Đang thực thi...' : 'Xác nhận khóa'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 3: Reset Elo */}
      {activeModal === 'RESET_ELO' && selectedUser && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="w-full max-w-sm bg-card border border-border rounded-xl p-6 shadow-2xl relative">
            <h3 className="text-base font-bold text-foreground font-display mb-1 text-amber-400">Đặt lại điểm Elo</h3>
            <p className="text-xs text-muted-foreground mb-4">
              Người chơi: <span className="font-semibold text-foreground">{selectedUser.playerName}</span> (Elo hiện tại: {selectedUser.elo})
            </p>

            <div className="space-y-3 mb-6">
              <div>
                <label className="block text-xs font-semibold text-foreground mb-1.5">Điểm Elo mới</label>
                <input
                  type="number"
                  value={defaultElo}
                  onChange={(e) => setDefaultElo(Number(e.target.value))}
                  className="w-full p-2 bg-slate-900 border border-border rounded-lg text-xs text-foreground focus:outline-none focus:ring-2 focus:ring-amber-500"
                />
              </div>
            </div>

            <div className="flex items-center justify-end gap-2">
              <button onClick={closeModal} className="px-3 py-1.5 text-xs text-muted-foreground hover:text-foreground">
                Hủy bỏ
              </button>
              <button
                onClick={() => resetEloMutation.mutate({ playerId: selectedUser.userId, defaultElo })}
                disabled={resetEloMutation.isPending}
                className="px-4 py-1.5 text-xs font-semibold bg-amber-600 hover:bg-amber-500 text-white rounded-lg shadow-sm"
              >
                {resetEloMutation.isPending ? 'Đang lưu...' : 'Đặt lại Elo'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
