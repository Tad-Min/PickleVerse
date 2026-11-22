import { loadReferences, ReferenceParameters } from './TechniqueReferences';
import { FormEvent, useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { apiClient } from '../../../api/client';
import { ContentPublishStatus } from '../../../types/status';
import {
  badgeBase, btnGhost, btnPrimary, card, fieldLabel, inputClass, notice, rowHover, sectionTitle, tone,
} from '../../../shared/ui/adminStyles';

type Skill = 'Serve' | 'Drive' | 'Dink' | 'Volley' | 'Modifiers' | 'Drop' | 'Reset' | 'Smash' | 'Footwork' | 'Approach' | 'DoublesCover' | 'DoublesTransition';
interface TrainingConfig {
  referenceId?: string; reference?: ReferenceParameters;
  id: string; title: string; explanation: string; coaching: string; skill: Skill;
  tier: number; trials: number; required: number; reward: number; xp: number;
  requiredLevel: number; targetRadius: number; targetDepth: number;
  alternateTargets: boolean; requireRecovery: boolean; masks: number[]; prerequisites: string[];
}
export interface LessonContent {
  lessonId: number; code: string; status: ContentPublishStatus; version: number; order: number;
  draft: TrainingConfig; published: TrainingConfig | null;
}
const templates: Record<Skill, { label: string; masks: number[] }> = {
  Serve: { label: 'Giao bóng', masks: [1] }, Drive: { label: 'Drive', masks: [1] }, Dink: { label: 'Dink', masks: [2] },
  Volley: { label: 'Volley', masks: [4] }, Modifiers: { label: 'Lực và hướng', masks: [9,17,10,18,12,20] },
  Drop: { label: 'Thả bóng cú ba', masks: [3] }, Reset: { label: 'Reset bóng', masks: [6] }, Smash: { label: 'Smash', masks: [5] },
  Footwork: { label: 'Di chuyển', masks: [1,17] }, Approach: { label: 'Drop và tiến lên', masks: [3] },
  DoublesCover: { label: 'Đội hình đôi', masks: [4,20] }, DoublesTransition: { label: 'Phối hợp đôi', masks: [3,2,4] },
};
export const names: Record<ContentPublishStatus, string> = { DRAFT: 'Bản nháp', PUBLISHED: 'Đang xuất bản', UNPUBLISHED: 'Đã gỡ' };
const statusTone: Record<ContentPublishStatus, string> = {
  DRAFT: tone.amber, PUBLISHED: tone.emerald, UNPUBLISHED: tone.neutral,
} as Record<ContentPublishStatus, string>;
const blank = (): TrainingConfig => ({ id: '', title: '', explanation: '', coaching: '', skill: 'Serve', tier: 0, trials: 8,
  required: 6, reward: 150, xp: 5, requiredLevel: 1, targetRadius: .9, targetDepth: 0, alternateTargets: false,
  requireRecovery: false, masks: [1], prerequisites: [] });
const checkboxRow = 'flex items-start gap-2 text-xs text-foreground';

export function LessonManager() {
  const client = useQueryClient();
  const references = useQuery({ queryKey: ['cms-technique-references'], queryFn: loadReferences });
  const query = useQuery({ queryKey: ['cms-lessons'], queryFn: async () => (await apiClient.get<LessonContent[]>('/api/Admin/cms/lessons')).data,
    refetchOnWindowFocus: false });
  const [selected, setSelected] = useState<number | null>(null);
  const [config, setConfig] = useState<TrainingConfig>(blank);
  const [order, setOrder] = useState(1);
  const [feedback, setFeedback] = useState('');
  const current = query.data?.find(x => x.lessonId === selected);
  useEffect(() => {
    if (current) { setConfig({ ...blank(), ...current.draft }); setOrder(current.order); }
  }, [current]);
  const update = <K extends keyof TrainingConfig>(key: K, value: TrainingConfig[K]) => setConfig(old => ({ ...old, [key]: value }));
  // "Xuất bản": lưu nội dung hiện tại rồi xuất bản ngay (tạo mới nếu chưa có bài).
  const action = useMutation({ mutationFn: async (kind: 'publish' | 'unpublish') => {
    if (kind === 'unpublish') {
      await apiClient.post(`/api/Admin/cms/lessons/${current!.lessonId}/unpublish`, { expectedVersion: current!.version });
      return kind;
    }
    let lessonId = current?.lessonId;
    if (!current) {
      const result = await apiClient.post<{ lessonId: number }>('/api/Admin/cms/lessons', { code: config.id, lessonName: config.title,
        description: config.explanation, requiredLevel: config.requiredLevel, order, config });
      lessonId = result.data.lessonId;
      setSelected(lessonId);
    } else {
      await apiClient.put(`/api/Admin/cms/lessons/${current.lessonId}`, { expectedVersion: current.version, order, config });
    }
    // Lấy phiên bản mới nhất sau khi lưu rồi xuất bản.
    const fresh = (await query.refetch()).data?.find(x => x.lessonId === lessonId);
    if (!fresh) throw new Error('Không tìm thấy bài học sau khi lưu.');
    await apiClient.post(`/api/Admin/cms/lessons/${lessonId}/publish`, { expectedVersion: fresh.version });
    return kind;
  }, onSuccess: async kind => {
    setFeedback(kind === 'publish' ? 'Đã xuất bản bài học và đưa thay đổi vào game.' : 'Đã gỡ bài khỏi danh mục mới.');
    await client.invalidateQueries({ queryKey: ['cms-lessons'] });
  }, onError: error => setFeedback(axios.isAxiosError(error) ? error.response?.data?.message || error.message : String(error)) });
  const submit = (event: FormEvent) => { event.preventDefault(); action.mutate('publish'); };
  const numeric: [keyof Pick<TrainingConfig, 'trials'|'required'|'reward'|'xp'|'requiredLevel'|'targetRadius'|'targetDepth'>, string, number, number, number][] = [
    ['trials','Số lượt',1,200,1], ['required','Số lượt đạt tối thiểu',1,200,1], ['reward','Thưởng TOKENPAD',0,100000,1],
    ['xp','Thưởng XP',0,10000,1], ['requiredLevel','Cấp độ tối thiểu',1,1000,1], ['targetRadius','Bán kính đích (m)',.3,2,.05],
    ['targetDepth','Độ sâu đích (m; 0 = theo mẫu)',0,6.4,.05],
  ];

  return (
    <section className="grid gap-5 lg:grid-cols-[300px_1fr] items-start">
      {/* Catalog */}
      <aside className={`${card} space-y-3 lg:sticky lg:top-6`}>
        <div className="flex items-center justify-between gap-2">
          <h2 className={sectionTitle}>Danh mục ({query.data?.length ?? 0})</h2>
        </div>
        {query.isLoading && <p className="text-xs text-muted-foreground">Đang tải…</p>}
        {query.isError && (
          <p className="text-xs text-rose-600">
            Không tải được danh mục.{' '}
            <button className="underline" onClick={() => query.refetch()}>Thử lại</button>
          </p>
        )}
        <div className="max-h-[640px] space-y-2 overflow-y-auto pr-1">
          {query.data?.map(row => (
            <button
              key={row.lessonId}
              type="button"
              disabled={action.isPending}
              onClick={() => { setSelected(row.lessonId); setFeedback(''); }}
              className={`w-full rounded-lg border p-3 text-left transition-colors ${
                selected === row.lessonId ? 'border-primary bg-primary/5' : `border-border ${rowHover}`
              }`}
            >
              <span className="block text-xs font-semibold text-foreground">{row.draft.title}</span>
              <span className="mt-1 flex flex-wrap items-center gap-2 text-[11px] text-muted-foreground">
                <span className={`${badgeBase} ${statusTone[row.status]}`}>{names[row.status]}</span>
                Phiên bản {row.version}
              </span>
            </button>
          ))}
          {query.data && query.data.length === 0 && (
            <p className="text-xs text-muted-foreground">Chưa có bài học nào. Nhập form bên phải để tạo bài đầu tiên.</p>
          )}
        </div>
      </aside>

      {/* Form */}
      <form onSubmit={submit} className={`${card} space-y-6`}>
        <div>
          <h2 className={sectionTitle}>{current ? 'Chỉnh sửa bài học và drill' : 'Tạo bài học từ mẫu'}</h2>
          <p className="mt-1 text-xs text-muted-foreground">
            Lưu tạo bản nháp. Xuất bản áp dụng cấu hình cho lượt chơi mới; lượt đang tập giữ phiên bản cũ.
          </p>
        </div>

        {feedback && <p role="status" className={`${notice('success')} text-xs`}>{feedback}</p>}

        {/* Basic info */}
        <div className="space-y-4">
          <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Thông tin chung</h3>
          <div className="grid gap-4 md:grid-cols-2">
            <label className={fieldLabel}>
              Mã bài
              <input
                className={`${inputClass} font-mono`}
                required
                pattern="[a-z][a-z0-9-]{1,49}"
                value={config.id}
                disabled={!!current}
                onChange={e => update('id', e.target.value)}
              />
            </label>
            <label className={fieldLabel}>
              Tên bài
              <input className={inputClass} required maxLength={254} value={config.title} onChange={e => update('title', e.target.value)} />
            </label>
            <label className={fieldLabel}>
              Mẫu kỹ thuật
              <select
                className={inputClass}
                value={config.skill}
                onChange={e => {
                  const skill = e.target.value as Skill;
                  setConfig(old => ({ ...old, skill, masks: templates[skill].masks, alternateTargets: false }));
                }}
              >
                {Object.entries(templates).map(([key, t]) => <option key={key} value={key}>{t.label}</option>)}
              </select>
            </label>
            <label className={fieldLabel}>
              Nhóm độ khó
              <select className={inputClass} value={config.tier} onChange={e => update('tier', +e.target.value)}>
                {['Cơ bản', 'Kiểm soát', 'Nâng cao', 'Đánh đôi'].map((name, i) => <option key={name} value={i}>{name}</option>)}
              </select>
            </label>
            <label className={fieldLabel}>
              Phiên bản tham chiếu kỹ thuật
              <select
                className={inputClass}
                value={config.referenceId || ''}
                onChange={e => update('referenceId', e.target.value)}
              >
                <option value="">Tự chọn phiên bản mới nhất khi xuất bản</option>
                {references.data?.filter(x => x.technique === config.skill).map(x => (
                  <option value={x.referenceId} key={x.referenceId}>{x.technique} · v{x.version}</option>
                ))}
              </select>
            </label>
            <label className={fieldLabel}>
              Thứ tự
              <input className={inputClass} type="number" value={order} onChange={e => setOrder(+e.target.value)} />
            </label>
          </div>
        </div>

        {/* Training parameters */}
        <div className="space-y-4">
          <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Thông số luyện tập</h3>
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {numeric.map(([key, label, min, max, step]) => (
              <label key={key} className={fieldLabel}>
                {label}
                <input
                  className={inputClass}
                  type="number"
                  required
                  min={min}
                  max={max}
                  step={step}
                  value={config[key]}
                  onChange={e => update(key, +e.target.value)}
                />
              </label>
            ))}
          </div>
          <div className="space-y-2">
            {config.skill === 'Dink' && (
              <label className={checkboxRow}>
                <input
                  type="checkbox"
                  className="mt-0.5"
                  checked={config.masks.every(m => m === 18)}
                  onChange={e => update('masks', e.target.checked ? [18] : [2])}
                />
                Chỉ luyện Dink chéo sân
              </label>
            )}
            {config.skill === 'Serve' && (
              <label className={checkboxRow}>
                <input
                  type="checkbox"
                  className="mt-0.5"
                  checked={config.alternateTargets}
                  onChange={e => update('alternateTargets', e.target.checked)}
                />
                Luân phiên hai ô giao
              </label>
            )}
            <label className={checkboxRow}>
              <input
                type="checkbox"
                className="mt-0.5"
                checked={config.requireRecovery}
                onChange={e => update('requireRecovery', e.target.checked)}
              />
              Yêu cầu trở về vòng hồi vị sau cú đánh
            </label>
          </div>
        </div>

        {/* Text */}
        <div className="space-y-4">
          <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Nội dung hướng dẫn</h3>
          <label className={fieldLabel}>
            Mục tiêu bài
            <textarea
              className={inputClass}
              required
              maxLength={254}
              rows={2}
              value={config.explanation}
              onChange={e => update('explanation', e.target.value)}
            />
          </label>
          <label className={fieldLabel}>
            Hướng dẫn và góp ý
            <textarea
              className={inputClass}
              required
              maxLength={2000}
              rows={3}
              value={config.coaching}
              onChange={e => update('coaching', e.target.value)}
            />
          </label>
        </div>

        {/* Prerequisites */}
        <fieldset className="space-y-2">
          <legend className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">Bài điều kiện</legend>
          <div className="grid gap-2 md:grid-cols-2">
            {query.data?.filter(row => row.code !== config.id).map(row => (
              <label key={row.code} className={`${checkboxRow} rounded-lg border border-border bg-background p-2.5`}>
                <input
                  type="checkbox"
                  className="mt-0.5"
                  checked={config.prerequisites.includes(row.code)}
                  onChange={e => update(
                    'prerequisites',
                    e.target.checked
                      ? [...config.prerequisites, row.code]
                      : config.prerequisites.filter(id => id !== row.code),
                  )}
                />
                {row.draft.title}
              </label>
            ))}
          </div>
        </fieldset>

        {/* Actions */}
        <div className="flex flex-wrap items-center justify-end gap-3 border-t border-border pt-5">
          {current?.status === ContentPublishStatus.Published && (
            <button className={btnGhost} type="button" disabled={action.isPending} onClick={() => action.mutate('unpublish')}>
              Gỡ xuất bản
            </button>
          )}
          <button className={btnPrimary} disabled={action.isPending}>
            {action.isPending ? 'Đang xuất bản…' : 'Xuất bản'}
          </button>
        </div>
      </form>
    </section>
  );
}
