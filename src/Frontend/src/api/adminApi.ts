import { apiClient } from './client';
import { isMockMode } from './mockMode';
import { mockAdminApi } from './mockAdminApi';
import {
  AdminAnalyticsResponse,
  AdminReportDto,
  AdminUserDto,
  AiTechniqueErrorHeatmapDto,
  AssignRoleRequest,
  AuthResponse,
  BanUserRequest,
  CreateMissionRequest,
  LoginRequest,
  PlayerAuthResponse,
  MatchItemDto,
  ReviewReportRequest,
  SaveShopItemRequest,
  ShopItem,
  ShopItems,
} from '../types/api';

export const adminApi = {
  // Auth
  login: async (credentials: LoginRequest): Promise<AuthResponse> => {
    if (isMockMode) return mockAdminApi.login(credentials);
    const { data } = await apiClient.post<PlayerAuthResponse>('/api/Auth/login', credentials);
    if (!data.success || !data.accessToken || !data.profile) throw new Error(data.message || 'Đăng nhập thất bại.');
    const encoded = data.accessToken.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const claims = JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(encoded), c => c.charCodeAt(0)))) as Record<string, string>;
    // UI visibility only; the backend validates signature, session version and role on every request.
    return { token: data.accessToken, userId: data.profile.userId, username: data.profile.playerName,
      email: credentials.email, role: claims.role || claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || 'Player' };
  },

  // W01 - User Management & RBAC
  getUsers: async (
    search?: string,
    status?: string,
    page: number = 1,
    pageSize: number = 20
  ): Promise<AdminUserDto[]> => {
    if (isMockMode) return mockAdminApi.getUsers(search, status, page, pageSize);
    const params = new URLSearchParams();
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    params.append('page', page.toString());
    params.append('pageSize', pageSize.toString());

    const res = await apiClient.get<AdminUserDto[]>(`/api/Admin/users?${params.toString()}`);
    return res.data;
  },

  assignRole: async (data: AssignRoleRequest): Promise<{ message: string }> => {
    if (isMockMode) return mockAdminApi.assignRole(data);
    const res = await apiClient.post<{ message: string }>('/api/Admin/users/assign-role', data);
    return res.data;
  },

  banUser: async (data: BanUserRequest): Promise<{ message: string }> => {
    if (isMockMode) return mockAdminApi.banUser(data);
    const res = await apiClient.post<{ message: string }>('/api/Admin/users/ban', data);
    return res.data;
  },

  unbanUser: async (userId: string, reason: string): Promise<{ message: string }> => {
    if (isMockMode) return mockAdminApi.unbanUser(userId);
    const res = await apiClient.post<{ message: string }>(`/api/Admin/users/${userId}/unban`, { reason });
    return res.data;
  },

  resetPlayerElo: async (playerId: string, defaultElo: number = 100): Promise<{ message: string }> => {
    if (isMockMode) return mockAdminApi.resetPlayerElo(playerId, defaultElo);
    const res = await apiClient.post<{ message: string }>(
      `/api/Admin/users/${playerId}/reset-elo?defaultElo=${defaultElo}`
    );
    return res.data;
  },

  manualVerifyEmail: async (userId: string): Promise<{ message: string }> => {
    if (isMockMode) return mockAdminApi.manualVerifyEmail(userId);
    const res = await apiClient.post<{ message: string }>(`/api/Admin/users/${userId}/verify-email`);
    return res.data;
  },

  // W02 - Content CMS
  getShopItems: async (): Promise<ShopItems> => {
    if (isMockMode) return mockAdminApi.getShopItems();
    return (await apiClient.get<ShopItems>('/api/Admin/cms/cosmetics')).data;
  },

  createShopItem: async (data: SaveShopItemRequest): Promise<ShopItem> => {
    if (isMockMode) return mockAdminApi.createShopItem(data);
    return (await apiClient.post<ShopItem>('/api/Admin/cms/cosmetics', data)).data;
  },

  updateShopItem: async (cosmeticId: number, data: SaveShopItemRequest): Promise<ShopItem> => {
    if (isMockMode) return mockAdminApi.updateShopItem(cosmeticId, data);
    return (await apiClient.put<ShopItem>(`/api/Admin/cms/cosmetics/${cosmeticId}`, data)).data;
  },

  deleteShopItem: async (cosmeticId: number): Promise<void> => {
    if (isMockMode) return mockAdminApi.deleteShopItem(cosmeticId);
    await apiClient.delete(`/api/Admin/cms/cosmetics/${cosmeticId}`);
  },

  createMission: async (data: CreateMissionRequest): Promise<{ missionId: number; message: string }> => {
    if (isMockMode) return mockAdminApi.createMission(data);
    const res = await apiClient.post<{ missionId: number; message: string }>('/api/Admin/cms/missions', data);
    return res.data;
  },

  // W03 - Match Monitoring
  getRecentMatches: async (limit: number = 50): Promise<MatchItemDto[]> => {
    if (isMockMode) return mockAdminApi.getRecentMatches(limit);
    const res = await apiClient.get<MatchItemDto[]>(`/api/Admin/matches/recent?limit=${limit}`);
    return res.data;
  },

  // W04 - Moderation & Tribunal
  getReports: async (status?: string): Promise<AdminReportDto[]> => {
    if (isMockMode) return mockAdminApi.getReports(status);
    const params = status ? `?status=${encodeURIComponent(status)}` : '';
    const res = await apiClient.get<AdminReportDto[]>(`/api/Admin/reports${params}`);
    return res.data;
  },

  reviewReport: async (data: ReviewReportRequest): Promise<{ message: string }> => {
    if (isMockMode) return mockAdminApi.reviewReport(data);
    const res = await apiClient.post<{ message: string }>('/api/Admin/reports/review', data);
    return res.data;
  },

  // W05 - Analytics & Telemetry
  getAnalyticsSummary: async (): Promise<AdminAnalyticsResponse> => {
    if (isMockMode) return mockAdminApi.getAnalyticsSummary();
    const res = await apiClient.get<AdminAnalyticsResponse>('/api/Admin/analytics/summary');
    return res.data;
  },

  getAiTechniqueErrorHeatmap: async (): Promise<AiTechniqueErrorHeatmapDto[]> => {
    if (isMockMode) return mockAdminApi.getAiTechniqueErrorHeatmap();
    const res = await apiClient.get<AiTechniqueErrorHeatmapDto[]>('/api/Admin/analytics/error-heatmap');
    return res.data;
  },
};
