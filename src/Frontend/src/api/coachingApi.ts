import { apiClient } from './client';

export interface CoachingAnalysis {
  sessionId: string;
  ruleVersion: string;
  // Optional while an older API/AI release is still deployed.
  summary?: string;
  findings: Array<{ shotId: string; technique: string; code: string; tip: string; ruleVersion: string; evidence: string; severity: number }>;
  metrics: Array<{ technique: string; code: string; opportunities: number; errors: number; severity: number }>;
  recommendations: Array<{ id: string; title: string; reason: string }>;
  tier: { status: string; tier: string | null; modelVersion: string | null; confidence: number; opportunities: number };
}

export interface CoachingSummary {
  status: string;
  updatedAt: string | null;
  analysis: CoachingAnalysis | null;
}

export const coachingApi = {
  summary: async (sessionId?: string, sessionType?: 'match' | 'training'): Promise<CoachingSummary> =>
    (await apiClient.get<CoachingSummary>('/api/AiCoaching/summary', { params: { sessionId, sessionType } })).data,
};
