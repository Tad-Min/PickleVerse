import { apiClient } from './client';
import { isMockMode } from './mockMode';
import { AdminDrill, AdminLesson, TrainingConfig } from '../features/content/pages/contentTypes';
import { ContentPublishStatus } from '../types/status';

export interface LessonBody { name: string; description: string; order: number }
export interface CreateLessonBody extends LessonBody { code: string }
export interface DrillBody { order: number; config: TrainingConfig }

const base = '/api/Admin/cms/lessons';

// ---- Bản xem thử trong bộ nhớ (chỉ dùng khi VITE_USE_MOCK_DATA=true) ----
const cfg = (id: string, title: string, skill: TrainingConfig['skill'], tier: number, prerequisites: string[] = []): TrainingConfig => ({
  id, title, explanation: `Mục tiêu của bài ${title}.`, coaching: 'Giữ nhịp thở đều và thả nút đúng lúc.', skill, tier, trials: 8, required: 6,
  reward: 150, xp: 5, requiredLevel: 1, targetRadius: .9, targetDepth: 0, alternateTargets: false, requireRecovery: false, masks: [1], prerequisites,
});
const drill = (drillId: number, order: number, config: TrainingConfig, published: boolean): AdminDrill => ({
  drillId, code: config.id, order, draft: config, published: published ? config : null, hasChanges: !published,
});
let mockLessons: AdminLesson[] = [
  { lessonId: 1, code: 'giao-bong', name: 'Giao bóng', description: 'Làm chủ cú giao bóng đúng ô.', status: ContentPublishStatus.Published, version: 3, order: 1,
    drills: [drill(1, 1, cfg('serve', 'Giao bóng đúng ô', 'Serve', 0), true), drill(2, 2, cfg('serve-alternating', 'Giao bóng luân phiên hai ô', 'Serve', 1, ['serve']), true)] },
  { lessonId: 2, code: 'dink', name: 'Dink và kiểm soát', description: 'Đánh nhẹ, kiểm soát vùng cấm vô lê.', status: ContentPublishStatus.Published, version: 5, order: 2,
    drills: [drill(3, 1, cfg('dink', 'Bóng ngắn kiểm soát', 'Dink', 0), true), drill(4, 2, cfg('dink-cross', 'Dink chéo sân', 'Dink', 1, ['dink']), true),
      drill(5, 3, cfg('dink-power', 'Dink có lực', 'Dink', 2, ['dink']), false)] },
];
let nextLesson = 3; let nextDrill = 6;
const wait = <T>(value: T) => new Promise<T>(resolve => setTimeout(() => resolve(structuredClone(value)), 250));
const find = (id: number) => { const lesson = mockLessons.find(l => l.lessonId === id); if (!lesson) throw new Error('Không tìm thấy bài học.'); return lesson; };
const guard = (lesson: AdminLesson, version: number) => { if (lesson.version !== version) throw new Error('Nội dung đã thay đổi. Hãy tải lại trước khi lưu.'); };

export const lessonApi = {
  list: async (): Promise<AdminLesson[]> =>
    isMockMode ? wait([...mockLessons].sort((a, b) => a.order - b.order)) : (await apiClient.get<AdminLesson[]>(base)).data,

  create: async (body: CreateLessonBody): Promise<AdminLesson> => {
    if (!isMockMode) return (await apiClient.post<AdminLesson>(base, body)).data;
    const lesson: AdminLesson = { lessonId: nextLesson++, code: body.code, name: body.name, description: body.description,
      status: ContentPublishStatus.Draft, version: 1, order: body.order, drills: [] };
    mockLessons = [...mockLessons, lesson];
    return wait(lesson);
  },

  save: async (id: number, version: number, body: LessonBody): Promise<AdminLesson> => {
    if (!isMockMode) return (await apiClient.put<AdminLesson>(`${base}/${id}`, { expectedVersion: version, ...body })).data;
    const lesson = find(id); guard(lesson, version);
    Object.assign(lesson, body, { version: lesson.version + 1 });
    return wait(lesson);
  },

  addDrill: async (id: number, version: number, body: DrillBody): Promise<AdminLesson> => {
    if (!isMockMode) return (await apiClient.post<AdminLesson>(`${base}/${id}/drills`, { expectedVersion: version, ...body })).data;
    const lesson = find(id); guard(lesson, version);
    if (mockLessons.some(l => l.drills.some(d => d.code === body.config.id))) throw new Error('Mã bài tập đã tồn tại.');
    lesson.drills = [...lesson.drills, { drillId: nextDrill++, code: body.config.id, order: body.order, draft: body.config, published: null, hasChanges: true }]
      .sort((a, b) => a.order - b.order);
    lesson.version++;
    return wait(lesson);
  },

  saveDrill: async (id: number, drillId: number, version: number, body: DrillBody): Promise<AdminLesson> => {
    if (!isMockMode) return (await apiClient.put<AdminLesson>(`${base}/${id}/drills/${drillId}`, { expectedVersion: version, ...body })).data;
    const lesson = find(id); guard(lesson, version);
    const target = lesson.drills.find(d => d.drillId === drillId); if (!target) throw new Error('Không tìm thấy bài tập trong bài học.');
    target.draft = body.config; target.order = body.order; target.hasChanges = JSON.stringify(body.config) !== JSON.stringify(target.published);
    lesson.drills = [...lesson.drills].sort((a, b) => a.order - b.order); lesson.version++;
    return wait(lesson);
  },

  deleteDrill: async (id: number, drillId: number, version: number): Promise<AdminLesson> => {
    if (!isMockMode) return (await apiClient.delete<AdminLesson>(`${base}/${id}/drills/${drillId}`, { params: { expectedVersion: version } })).data;
    const lesson = find(id); guard(lesson, version);
    const target = lesson.drills.find(d => d.drillId === drillId); if (!target) throw new Error('Không tìm thấy bài tập trong bài học.');
    if (lesson.status === ContentPublishStatus.Published && target.published) throw new Error('Hãy gỡ xuất bản bài học trước khi xóa bài tập đã xuất bản.');
    lesson.drills = lesson.drills.filter(d => d.drillId !== drillId); lesson.version++;
    return wait(lesson);
  },

  publish: async (id: number, version: number, kind: 'publish' | 'unpublish'): Promise<void> => {
    if (!isMockMode) { await apiClient.post(`${base}/${id}/${kind}`, { expectedVersion: version }); return; }
    const lesson = find(id); guard(lesson, version);
    if (kind === 'publish') {
      if (lesson.drills.length === 0) throw new Error('Bài học cần có ít nhất một bài tập trước khi xuất bản.');
      lesson.drills.forEach(d => { d.published = d.draft; d.hasChanges = false; });
      lesson.status = ContentPublishStatus.Published;
    } else lesson.status = ContentPublishStatus.Unpublished;
    lesson.version++;
    await wait(null);
  },
};
