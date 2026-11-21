import { UserStatus, ReportStatus, AppealStatus, MatchStatus } from "./status";
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
  status: `${UserStatus}`;
  emailVerified?: boolean;
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
  restriction?: UserStatus.Banned | UserStatus.Suspended;
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
  reward: { coins: number; gems: number; xp: number };
}

// W03 - Match Monitoring
export interface MatchItemDto {
  matchId: string; matchType: string; teamAScore: number; teamBScore: number; createdAt: string;
  isWinner?: boolean; receivedElo?: number; replayUrl?: string | null;
}

// W04 - Moderation & Tribunal
export interface AdminReportDto {
  reporterId: string;
  reporterName: string;
  reportedPlayerId: string;
  reportedPlayerName: string;
  matchId: string;
  reason: string;
  status: `${ReportStatus}`;
  reviewerId?: string;
  decisionReason?: string;
  createdAt: string;
  replayUrl?: string | null;
}

export interface ReviewReportRequest {
  reporterId: string;
  reportedPlayerId: string;
  matchId: string;
  status: `${ReportStatus.Accepted}` | `${ReportStatus.Declined}` | `${ReportStatus.Checking}`;
  decisionReason: string;
  restriction?: UserStatus.Banned | UserStatus.Suspended;
  bannedUntil?: string | null;
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

export interface SanctionDecision {
  sanctionId: string; userId: string; actorId: string; restriction: UserStatus; reason: string;
  createdAt: string; expiresAt?: string | null; revokedAt?: string | null; revocationReason?: string | null;
  reportMatchId?: string | null; reporterId?: string | null;
}
export interface SanctionAppeal {
  appealId: string; sanctionId: string; userId: string; statement: string; status: AppealStatus;
  createdAt: string; updatedAt: string; reviewerId?: string | null; decisionReason?: string | null;
}

export interface StopOperation {
  operationId: string; matchId: string; leaseId: string; actorId: string; reason: string; createdAt: string; acknowledgedAt?: string | null;
}
export interface LiveMatch {
  matchId: string; status: MatchStatus; authorityMode: 'shared' | 'dedicated'; createdAt: string; startedAt?: string | null;
  heartbeatAt?: string | null; heartbeatStale: boolean;
  roster: { members: { id: string; name: string; team: number; bot: boolean }[] };
  live?: { scoreA: number; scoreB: number; gamesA: number; gamesB: number; started: boolean; paused: boolean; finished: boolean; observedAt: string;
    players: { id: string; connected: boolean; reconnectUntil?: string | null }[] } | null;
  stopOperation?: StopOperation | null;
}

export interface QueueHealth {
  searches: number; searching: number; matched: number; cancelled: number; timedOut: number; botFallback: number;
  cancelledAfterAssignment: number; averageWaitSeconds?: number | null; p95WaitSeconds?: number | null;
}
export interface AnalyticsReport {
  timeZone: string; from: string; to: string; dauDefinition: string; retentionDefinition: string; timeDefinition: string;
  summary: AdminAnalyticsResponse; daily: { day: string; players: number }[];
  retention: { day: string; players: number; d1?: number | null; d7?: number | null; d30?: number | null }[];
  lessons: { lessonId: number; code: string; title: string; starts: number; finished: number; passed: number; players: number; completionRate: number }[];
  matchmaking: QueueHealth; errors: AiTechniqueErrorHeatmapDto[];
}
