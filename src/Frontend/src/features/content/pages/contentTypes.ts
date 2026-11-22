import { ContentPublishStatus } from '../../../types/status';
import { ReferenceParameters } from './TechniqueReferences';

export type Skill =
  | 'Serve' | 'Drive' | 'Dink' | 'Volley' | 'Modifiers' | 'Drop' | 'Reset' | 'Smash'
  | 'Footwork' | 'Approach' | 'DoublesCover' | 'DoublesTransition';

/** Cấu hình luyện tập của một bài tập (khớp TrainingDrill ở backend và game). */
export interface TrainingConfig {
  referenceId?: string; reference?: ReferenceParameters;
  id: string; title: string; explanation: string; coaching: string; skill: Skill;
  tier: number; trials: number; required: number; reward: number; xp: number;
  requiredLevel: number; targetRadius: number; targetDepth: number;
  alternateTargets: boolean; requireRecovery: boolean; masks: number[]; prerequisites: string[];
}

export interface AdminDrill {
  drillId: number;
  code: string;
  order: number;
  draft: TrainingConfig;
  published: TrainingConfig | null;
  hasChanges: boolean;
}

/** Bài học: nhóm nhiều bài tập. */
export interface AdminLesson {
  lessonId: number;
  code: string;
  name: string;
  description: string;
  status: ContentPublishStatus;
  version: number;
  order: number;
  drills: AdminDrill[];
}

export const skillTemplates: Record<Skill, { label: string; masks: number[] }> = {
  Serve: { label: 'Giao bóng', masks: [1] }, Drive: { label: 'Drive', masks: [1] }, Dink: { label: 'Dink', masks: [2] },
  Volley: { label: 'Volley', masks: [4] }, Modifiers: { label: 'Lực và hướng', masks: [9, 17, 10, 18, 12, 20] },
  Drop: { label: 'Thả bóng cú ba', masks: [3] }, Reset: { label: 'Reset bóng', masks: [6] }, Smash: { label: 'Smash', masks: [5] },
  Footwork: { label: 'Di chuyển', masks: [1, 17] }, Approach: { label: 'Drop và tiến lên', masks: [3] },
  DoublesCover: { label: 'Đội hình đôi', masks: [4, 20] }, DoublesTransition: { label: 'Phối hợp đôi', masks: [3, 2, 4] },
};

export const tierNames = ['Cơ bản', 'Kiểm soát', 'Nâng cao', 'Đánh đôi'];

export const statusNames: Record<ContentPublishStatus, string> = {
  DRAFT: 'Bản nháp', PUBLISHED: 'Đang xuất bản', UNPUBLISHED: 'Đã gỡ',
};

export const blankDrill = (): TrainingConfig => ({
  id: '', title: '', explanation: '', coaching: '', skill: 'Serve', tier: 0, trials: 8,
  required: 6, reward: 150, xp: 5, requiredLevel: 1, targetRadius: .9, targetDepth: 0, alternateTargets: false,
  requireRecovery: false, masks: [1], prerequisites: [],
});
