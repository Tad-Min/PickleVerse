export enum UserStatus { Active = 'ACTIVE', Banned = 'BANNED', Suspended = 'SUSPENDED' }
export enum ReportStatus { Pending = 'PENDING', Checking = 'CHECKING', Accepted = 'ACCEPTED', Declined = 'DECLINED' }
export enum MatchStatus { Preparing = 'PREPARING', Playing = 'PLAYING', Finished = 'FINISHED', Cancelled = 'CANCELLED' }
export enum ContentPublishStatus { Draft = 'DRAFT', Published = 'PUBLISHED', Unpublished = 'UNPUBLISHED' }
export enum AppealStatus { Pending = 'PENDING', Checking = 'CHECKING', Accepted = 'ACCEPTED', Declined = 'DECLINED' }
export enum ProgressResetGroup { Learning = 'LEARNING', Ranking = 'RANKING', Missions = 'MISSIONS', Economy = 'ECONOMY' }
export const userStatusLabel: Record<UserStatus, string> = {
  ACTIVE: 'Đang hoạt động', BANNED: 'Bị cấm', SUSPENDED: 'Đình chỉ',
};
export const reportStatusLabel: Record<ReportStatus, string> = {
  PENDING: 'Chờ xử lý', CHECKING: 'Đang xem xét', ACCEPTED: 'Đã chấp nhận', DECLINED: 'Đã từ chối',
};
export const matchStatusLabel: Record<MatchStatus, string> = {
  PREPARING: 'Chuẩn bị', PLAYING: 'Đang thi đấu', FINISHED: 'Đã kết thúc', CANCELLED: 'Đã hủy',
};
export const appealStatusLabel: Record<AppealStatus, string> = {
  PENDING: 'Chờ xử lý', CHECKING: 'Đang xem xét', ACCEPTED: 'Đã chấp nhận', DECLINED: 'Đã từ chối',
};
