import { apiClient } from './client';

export interface ReplayLaunchLink { matchId: string; url: string; expiresAt: string }

export function validateReplayLaunch(link: ReplayLaunchLink, matchId: string): ReplayLaunchLink {
  const match = matchId.replace(/-/g, '').toLowerCase();
  const uri = new URL(link.url);
  if (!/^[a-f0-9]{32}$/.test(match) || link.matchId.replace(/-/g, '').toLowerCase() !== match ||
      uri.protocol !== 'pickleverse:' || uri.hostname !== 'replay' || uri.username || uri.password || uri.port ||
      uri.pathname !== `/${match}` || uri.hash || Array.from(uri.searchParams.keys()).join(',') !== 'code' ||
      !/^[a-f0-9]{64}$/.test(uri.searchParams.get('code') || '') ||
      link.url !== `pickleverse://replay/${match}?code=${uri.searchParams.get('code')}` ||
      !Number.isFinite(Date.parse(link.expiresAt)) || Date.parse(link.expiresAt) <= Date.now()) {
    throw new Error('Liên kết mở replay không hợp lệ hoặc đã hết hạn.');
  }
  return link;
}

export async function createReplayLaunch(matchId: string, signal?: AbortSignal): Promise<ReplayLaunchLink> {
  const { data } = await apiClient.post<ReplayLaunchLink>(`/api/replay-launch/${encodeURIComponent(matchId)}`, undefined, { signal });
  return validateReplayLaunch(data, matchId);
}
