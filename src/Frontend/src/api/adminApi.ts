import { apiClient } from './client';
import {
  AdminAnalyticsResponse,
  AdminReportDto,
  AdminUserDto,
  AiTechniqueErrorHeatmapDto,
  AssignRoleRequest,
  AuthResponse,
  BanUserRequest,
  CreateCosmeticRequest,
  CreateDrillRequest,
  CreateLessonRequest,
  CreateMissionRequest,
  LoginRequest,
  MatchItemDto,
  ReviewReportRequest,
} from '../types/api';

export const adminApi = {
  // Auth
  login: async (credentials: LoginRequest): Promise<AuthResponse> => {
    const res = await apiClient.post<AuthResponse>('/api/Auth/login', credentials);
    return res.data;
  },

  // W01 - User Management & RBAC
  getUsers: async (
    search?: string,
    status?: string,
    page: number = 1,
    pageSize: number = 20
  ): Promise<AdminUserDto[]> => {
    const params = new URLSearchParams();
    if (search) params.append('search', search);
    if (status) params.append('status', status);
    params.append('page', page.toString());
    params.append('pageSize', pageSize.toString());

    const res = await apiClient.get<AdminUserDto[]>(`/api/Admin/users?${params.toString()}`);
    return res.data;
  },

  assignRole: async (data: AssignRoleRequest): Promise<{ message: string }> => {
    const res = await apiClient.post<{ message: string }>('/api/Admin/users/assign-role', data);
    return res.data;
  },

  banUser: async (data: BanUserRequest): Promise<{ message: string }> => {
    const res = await apiClient.post<{ message: string }>('/api/Admin/users/ban', data);
    return res.data;
  },

  unbanUser: async (userId: string): Promise<{ message: string }> => {
    const res = await apiClient.post<{ message: string }>(`/api/Admin/users/${userId}/unban`);
    return res.data;
  },

  resetPlayerElo: async (playerId: string, defaultElo: number = 2000): Promise<{ message: string }> => {
    const res = await apiClient.post<{ message: string }>(
      `/api/Admin/users/${playerId}/reset-elo?defaultElo=${defaultElo}`
    );
    return res.data;
  },

  manualVerifyEmail: async (userId: string): Promise<{ message: string }> => {
    const res = await apiClient.post<{ message: string }>(`/api/Admin/users/${userId}/verify-email`);
    return res.data;
  },

  // W02 - Content CMS
  createLesson: async (data: CreateLessonRequest): Promise<{ lessonId: number; message: string }> => {
    const res = await apiClient.post<{ lessonId: number; message: string }>('/api/Admin/cms/lessons', data);
    return res.data;
  },

  createDrill: async (data: CreateDrillRequest): Promise<{ drillId: number; message: string }> => {
    const res = await apiClient.post<{ drillId: number; message: string }>('/api/Admin/cms/drills', data);
    return res.data;
  },

  createCosmetic: async (data: CreateCosmeticRequest): Promise<{ cosmeticId: number; message: string }> => {
    const res = await apiClient.post<{ cosmeticId: number; message: string }>('/api/Admin/cms/cosmetics', data);
    return res.data;
  },

  createMission: async (data: CreateMissionRequest): Promise<{ missionId: number; message: string }> => {
    const res = await apiClient.post<{ missionId: number; message: string }>('/api/Admin/cms/missions', data);
    return res.data;
  },

  // W03 - Match Monitoring
  getRecentMatches: async (limit: number = 50): Promise<MatchItemDto[]> => {
    const res = await apiClient.get<MatchItemDto[]>(`/api/Admin/matches/recent?limit=${limit}`);
    return res.data;
  },

  // W04 - Moderation & Tribunal
  getReports: async (status?: string): Promise<AdminReportDto[]> => {
    const params = status ? `?status=${encodeURIComponent(status)}` : '';
    const res = await apiClient.get<AdminReportDto[]>(`/api/Admin/reports${params}`);
    return res.data;
  },

  reviewReport: async (data: ReviewReportRequest): Promise<{ message: string }> => {
    const res = await apiClient.post<{ message: string }>('/api/Admin/reports/review', data);
    return res.data;
  },

  // W05 - Analytics & Telemetry
  getAnalyticsSummary: async (): Promise<AdminAnalyticsResponse> => {
    const res = await apiClient.get<AdminAnalyticsResponse>('/api/Admin/analytics/summary');
    return res.data;
  },

  getAiTechniqueErrorHeatmap: async (): Promise<AiTechniqueErrorHeatmapDto[]> => {
    const res = await apiClient.get<AiTechniqueErrorHeatmapDto[]>('/api/Admin/analytics/error-heatmap');
    return res.data;
  },
};
