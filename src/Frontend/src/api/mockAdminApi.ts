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

const makeDate = (daysAgo: number, hoursAgo = 0) =>
  new Date(Date.now() - (daysAgo * 24 + hoursAgo) * 60 * 60 * 1000).toISOString();

let users: AdminUserDto[] = [
  { userId: 'demo-admin-001', email: 'admin@pickleverse.local', playerName: 'CourtAdmin', roleId: 2, roleName: 'Admin', status: 'ACTIVE', elo: 2000, timePlayedSeconds: 128400, createdAt: makeDate(220) },
  { userId: 'player-001', email: 'minh.nguyen@example.com', playerName: 'MinhNguyen', roleId: 1, roleName: 'Player', status: 'ACTIVE', elo: 1842, timePlayedSeconds: 72600, createdAt: makeDate(38) },
  { userId: 'player-002', email: 'linh.tran@example.com', playerName: 'LinhSmash', roleId: 1, roleName: 'Player', status: 'ACTIVE', elo: 1695, timePlayedSeconds: 49200, createdAt: makeDate(25) },
  { userId: 'player-003', email: 'bao.le@example.com', playerName: 'BảoLê', roleId: 1, roleName: 'Player', status: 'SUSPENDED', elo: 1430, timePlayedSeconds: 18400, createdAt: makeDate(17) },
  { userId: 'player-004', email: 'an.pham@example.com', playerName: 'AnDink', roleId: 1, roleName: 'Player', status: 'ACTIVE', elo: 2018, timePlayedSeconds: 110300, createdAt: makeDate(93) },
  { userId: 'player-005', email: 'khoa.vo@example.com', playerName: 'KhoaVolley', roleId: 1, roleName: 'Player', status: 'UNVERIFIED', elo: 1000, timePlayedSeconds: 0, createdAt: makeDate(1) },
  { userId: 'player-006', email: 'thao.do@example.com', playerName: 'ThaoDrop', roleId: 1, roleName: 'Player', status: 'BANNED', elo: 1320, timePlayedSeconds: 35700, createdAt: makeDate(61) },
  { userId: 'player-007', email: 'nam.bui@example.com', playerName: 'NamKitchen', roleId: 1, roleName: 'Player', status: 'ACTIVE', elo: 1764, timePlayedSeconds: 61800, createdAt: makeDate(12) },
];

const matches: MatchItemDto[] = [
  { matchId: 'match-live-01', mode: '1v1', courtName: 'Neon Harbor', startTime: makeDate(0, 0.3), status: 'IN_PROGRESS', playerCount: 2 },
  { matchId: 'match-live-02', mode: '2v2', courtName: 'Sunset Club', startTime: makeDate(0, 0.7), status: 'IN_PROGRESS', playerCount: 4 },
  { matchId: 'match-2026-01', mode: '1v1', courtName: 'Neon Harbor', startTime: makeDate(0, 2), endTime: makeDate(0, 1.6), winner: 'MinhNguyen', scoreSummary: '11–8, 9–11, 11–6', status: 'COMPLETED', playerCount: 2 },
  { matchId: 'match-2026-02', mode: '2v2', courtName: 'Garden Court', startTime: makeDate(0, 4), endTime: makeDate(0, 3.4), winner: 'AnDink / LinhSmash', scoreSummary: '11–5, 11–9', status: 'COMPLETED', playerCount: 4 },
  { matchId: 'match-2026-03', mode: '1v1', courtName: 'Rooftop Arena', startTime: makeDate(1, 5), endTime: makeDate(1, 4.5), winner: 'NamKitchen', scoreSummary: '7–11, 11–7, 11–9', status: 'COMPLETED', playerCount: 2 },
  { matchId: 'match-2026-04', mode: '1v1', courtName: 'Garden Court', startTime: makeDate(1, 8), endTime: makeDate(1, 7.7), scoreSummary: '6–11, 4–11', status: 'ABANDONED', playerCount: 2 },
];

let reports: AdminReportDto[] = [
  { reporterId: 'player-001', reporterName: 'MinhNguyen', reportedPlayerId: 'player-003', reportedPlayerName: 'BảoLê', matchId: 'match-2026-04', reason: 'Thoát trận liên tục khi đang thua', status: 'PENDING', createdAt: makeDate(0, 1), replayUrl: null },
  { reporterId: 'player-004', reporterName: 'AnDink', reportedPlayerId: 'player-006', reportedPlayerName: 'ThaoDrop', matchId: 'match-2026-03', reason: 'Có hành vi xúc phạm trong trận', status: 'PENDING', createdAt: makeDate(0, 3), replayUrl: null },
  { reporterId: 'player-007', reporterName: 'NamKitchen', reportedPlayerId: 'player-002', reportedPlayerName: 'LinhSmash', matchId: 'match-2026-02', reason: 'Nghi ngờ cố tình kéo dài thời gian', status: 'RESOLVED', createdAt: makeDate(2), replayUrl: null },
  { reporterId: 'player-002', reporterName: 'LinhSmash', reportedPlayerId: 'player-001', reportedPlayerName: 'MinhNguyen', matchId: 'match-2026-01', reason: 'Va chạm không chủ ý', status: 'DISMISSED', createdAt: makeDate(4), replayUrl: null },
];

let nextLessonId = 8;
let nextDrillId = 24;
let nextCosmeticId = 12;
let nextMissionId = 16;

const waitForDemo = <T,>(value: T): Promise<T> =>
  new Promise((resolve) => window.setTimeout(() => resolve(value), 180));

export const mockAdminApi = {
  login: async (credentials: LoginRequest): Promise<AuthResponse> => {
    const identity = credentials.emailOrUsername.trim().toLowerCase();
    const admin = users.find((user) => user.roleId === 2 && user.status === 'ACTIVE');
    if (!admin || !['admin', 'admin@pickleverse.local'].includes(identity) || credentials.passwordHash !== 'Admin123!') {
      throw new Error('Tài khoản demo không hợp lệ. Dùng admin@pickleverse.local / Admin123!.');
    }
    return waitForDemo({ token: 'pickleverse-local-demo-token', userId: admin.userId, username: admin.playerName, email: admin.email, role: 'Admin' });
  },

  getUsers: async (search?: string, status?: string, page = 1, pageSize = 20): Promise<AdminUserDto[]> => {
    const query = search?.trim().toLowerCase();
    const filtered = users.filter((user) => {
      const matchesSearch = !query || `${user.email} ${user.playerName}`.toLowerCase().includes(query);
      return matchesSearch && (!status || user.status === status);
    });
    return waitForDemo(filtered.slice((page - 1) * pageSize, page * pageSize));
  },

  assignRole: async (data: AssignRoleRequest) => {
    const user = users.find((item) => item.userId === data.userId);
    if (user) {
      user.roleId = data.roleId;
      user.roleName = data.roleId === 2 ? 'Admin' : 'Player';
    }
    return waitForDemo({ message: 'Đã cập nhật vai trò trong dữ liệu demo.' });
  },

  banUser: async (data: BanUserRequest) => {
    const user = users.find((item) => item.userId === data.userId);
    if (user) user.status = data.bannedUntil ? 'SUSPENDED' : 'BANNED';
    return waitForDemo({ message: 'Đã áp dụng trạng thái trong dữ liệu demo.' });
  },

  unbanUser: async (userId: string) => {
    const user = users.find((item) => item.userId === userId);
    if (user) user.status = 'ACTIVE';
    return waitForDemo({ message: 'Đã mở khóa trong dữ liệu demo.' });
  },

  resetPlayerElo: async (playerId: string, defaultElo = 2000) => {
    const user = users.find((item) => item.userId === playerId);
    if (user) user.elo = defaultElo;
    return waitForDemo({ message: `Đã reset Elo demo về ${defaultElo}.` });
  },

  manualVerifyEmail: async (userId: string) => {
    const user = users.find((item) => item.userId === userId);
    if (user?.status === 'UNVERIFIED') user.status = 'ACTIVE';
    return waitForDemo({ message: 'Đã xác thực email demo.' });
  },

  createLesson: async (data: CreateLessonRequest) => waitForDemo({ lessonId: nextLessonId++, message: `Đã tạo bài học “${data.lessonName}” trong phiên demo.` }),
  createDrill: async (data: CreateDrillRequest) => waitForDemo({ drillId: nextDrillId++, message: `Đã tạo bài tập “${data.drillName}” trong phiên demo.` }),
  createCosmetic: async (data: CreateCosmeticRequest) => waitForDemo({ cosmeticId: nextCosmeticId++, message: `Đã tạo vật phẩm “${data.cosmeticName}” trong phiên demo.` }),
  createMission: async (data: CreateMissionRequest) => waitForDemo({ missionId: nextMissionId++, message: `Đã tạo nhiệm vụ “${data.missionName}” trong phiên demo.` }),

  getRecentMatches: async (limit = 50): Promise<MatchItemDto[]> => waitForDemo(matches.slice(0, limit)),
  getReports: async (status?: string): Promise<AdminReportDto[]> => waitForDemo(reports.filter((report) => !status || report.status === status)),

  reviewReport: async (data: ReviewReportRequest) => {
    const report = reports.find((item) => item.reporterId === data.reporterId && item.reportedPlayerId === data.reportedPlayerId && item.matchId === data.matchId);
    if (report) report.status = data.status;
    if (report && data.banReportedPlayer) {
      const reportedUser = users.find((item) => item.userId === data.reportedPlayerId);
      if (reportedUser) reportedUser.status = 'BANNED';
    }
    return waitForDemo({ message: 'Đã cập nhật kết quả xử lý báo cáo demo.' });
  },

  getAnalyticsSummary: async (): Promise<AdminAnalyticsResponse> => waitForDemo({
    totalUsers: users.length,
    activeUsersToday: 1248,
    totalMatchesPlayed: 3482,
    totalPlayTimeSeconds: 8276400,
    totalReportsPending: reports.filter((report) => report.status === 'PENDING').length,
  }),

  getAiTechniqueErrorHeatmap: async (): Promise<AiTechniqueErrorHeatmapDto[]> => waitForDemo([
    { techniqueId: 1, techniqueName: 'Serve', totalErrorsCount: 64, errorBreakdown: [{ errorType: 'Serve trễ', count: 28 }, { errorType: 'Sai vị trí chân', count: 21 }, { errorType: 'Bóng không qua lưới', count: 15 }] },
    { techniqueId: 2, techniqueName: 'Dink', totalErrorsCount: 42, errorBreakdown: [{ errorType: 'Bóng quá cao', count: 24 }, { errorType: 'Điểm chạm muộn', count: 18 }] },
    { techniqueId: 3, techniqueName: 'Volley', totalErrorsCount: 31, errorBreakdown: [{ errorType: 'Vung vợt rộng', count: 17 }, { errorType: 'Sai tư thế sẵn sàng', count: 14 }] },
    { techniqueId: 4, techniqueName: 'Drop', totalErrorsCount: 18, errorBreakdown: [{ errorType: 'Bóng đi quá sâu', count: 11 }, { errorType: 'Thiếu độ cong', count: 7 }] },
    { techniqueId: 5, techniqueName: 'Kitchen', totalErrorsCount: 9, errorBreakdown: [{ errorType: 'Chạm kitchen', count: 9 }] },
  ]),
};
