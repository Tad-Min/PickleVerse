// TypeScript DTOs matching src/Backend/Entities/DTOs.

export interface LoginRequest {
  email: string;
  password: string;
}

export interface PlayerAuthResponse {
  success: boolean;
  message: string;
  code: string;
  accessToken?: string;
  refreshToken?: string;
  profile?: { userId: string; playerName: string };
}

export interface PasswordRecoveryReceipt {
  code: string;
  message: string;
  otpLifetimeMinutes: number;
  otpResendCooldownSeconds: number;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  password: string;
  confirmPassword: string;
}

export interface AuthResponse {
  token: string;
  userId: string;
  username: string;
  email: string;
  role: string;
}

export interface UserSession {
  userId: string;
  username: string;
  email: string;
  role: string;
  token: string;
}

// W01 - User Management & RBAC
export interface AdminUserDto {
  userId: string;
  email: string;
  playerName: string;
  roleId: number;
  roleName: string;
  status: string; // "ACTIVE", "BANNED", "UNVERIFIED"
  elo: number;
  timePlayedSeconds: number;
  createdAt: string;
}

export interface AssignRoleRequest {
  userId: string;
  roleId: number; // 1: Player, 2: Admin
}

export interface BanUserRequest {
  userId: string;
  reason: string;
  bannedUntil?: string | null;
}

// W02 - Content CMS
export interface CreateLessonRequest {
  lessonName: string;
  description: string;
  requiredLevel: number;
  order: number;
}

export interface CreateDrillRequest {
  lessonId: number;
  techniqueId: number;
  drillName: string;
  description: string;
  target: number;
  order: number;
}

export interface CreateCosmeticRequest {
  itemCode: string;
  cosmeticName: string;
  description: string;
  price: number;
  type: string; // "PADDLE", "OUTFIT", "HAT"
  currencyId: number; // 1: Gold, 2: Gem
}

export interface CreateMissionRequest {
  missionName: string;
  description: string;
  type: string; // "DAILY", "WEEKLY"
  target: number;
  reward: string;
}

// W03 - Match Monitoring
export interface MatchItemDto {
  matchId: string;
  mode: string; // "1v1", "2v2"
  courtName: string;
  startTime: string;
  endTime?: string | null;
  winner?: string | null;
  scoreSummary?: string | null;
  status: string; // "IN_PROGRESS", "COMPLETED", "ABANDONED"
  playerCount?: number;
}

// W04 - Moderation & Tribunal
export interface AdminReportDto {
  reporterId: string;
  reporterName: string;
  reportedPlayerId: string;
  reportedPlayerName: string;
  matchId: string;
  reason: string;
  status: string; // "PENDING", "RESOLVED", "DISMISSED"
  createdAt: string;
  replayUrl?: string | null;
}

export interface ReviewReportRequest {
  reporterId: string;
  reportedPlayerId: string;
  matchId: string;
  status: 'RESOLVED' | 'DISMISSED';
  banReportedPlayer: boolean;
  banReason?: string;
}

// W05 - Analytics & Telemetry
export interface AdminAnalyticsResponse {
  totalUsers: number;
  activeUsersToday: number;
  totalMatchesPlayed: number;
  totalPlayTimeSeconds: number;
  totalReportsPending: number;
}

export interface ErrorTypeBreakdownDto {
  errorType: string;
  count: number;
}

export interface AiTechniqueErrorHeatmapDto {
  techniqueId: number;
  techniqueName: string;
  totalErrorsCount: number;
  errorBreakdown: ErrorTypeBreakdownDto[];
}
