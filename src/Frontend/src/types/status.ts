export enum UserStatus { Active = 'ACTIVE', Banned = 'BANNED', Suspended = 'SUSPENDED' }
export enum ReportStatus { Pending = 'PENDING', Checking = 'CHECKING', Accepted = 'ACCEPTED', Declined = 'DECLINED' }
export enum MatchStatus { Preparing = 'PREPARING', Playing = 'PLAYING', Finished = 'FINISHED', Cancelled = 'CANCELLED' }
export enum ContentPublishStatus { Draft = 'DRAFT', Published = 'PUBLISHED', Unpublished = 'UNPUBLISHED' }
export enum AppealStatus { Pending = 'PENDING', Checking = 'CHECKING', Accepted = 'ACCEPTED', Declined = 'DECLINED' }
export enum ProgressResetGroup { Learning = 'LEARNING', Ranking = 'RANKING', Missions = 'MISSIONS', Economy = 'ECONOMY' }
export const userStatusLabel: Record<UserStatus, string> = {
  ACTIVE: 'Hoạt động', BANNED: 'Bị cấm', SUSPENDED: 'Tạm đình chỉ',
};
export const reportStatusLabel: Record<ReportStatus, string> = {
  PENDING: 'Chờ xử lý', CHECKING: 'Đang xem xét', ACCEPTED: 'Đã chấp nhận', DECLINED: 'Đã từ chối',
};
