import { FormEvent, useState } from 'react';
import { TechniqueReference } from './TechniqueReferences';
import { Skill, TrainingConfig, skillTemplates, tierNames, blankDrill } from './contentTypes';
import { btnGhost, btnPrimary, fieldLabel, inputClass } from '../../../shared/ui/adminStyles';

export interface DrillChoice { code: string; title: string }

interface DrillFormProps {
  /** null = thêm bài tập mới. */
  initial: TrainingConfig | null;
  initialOrder: number;
  /** Các bài tập khác có thể chọn làm điều kiện mở khóa. */
  choices: DrillChoice[];
  references: TechniqueReference[];
  busy: boolean;
  onSave: (config: TrainingConfig, order: number) => void;
  onCancel: () => void;
}

const numeric: [keyof Pick<TrainingConfig, 'trials' | 'required' | 'reward' | 'xp' | 'requiredLevel' | 'targetRadius' | 'targetDepth'>, string, number, number, number][] = [
  ['trials', 'Số lượt', 1, 200, 1], ['required', 'Số lượt đạt tối thiểu', 1, 200, 1], ['reward', 'Thưởng TOKENPAD', 0, 100000, 1],
  ['xp', 'Thưởng XP', 0, 10000, 1], ['requiredLevel', 'Cấp độ tối thiểu', 1, 1000, 1], ['targetRadius', 'Bán kính đích (m)', .3, 2, .05],
  ['targetDepth', 'Độ sâu đích (m; 0 = theo mẫu)', 0, 6.4, .05],
];
const checkboxRow = 'flex items-start gap-2 text-xs text-foreground';
const sectionLabel = 'text-xs font-semibold uppercase tracking-wider text-muted-foreground';

/** Form cấu hình một bài tập: kỹ thuật, độ khó, thông số, hướng dẫn và điều kiện mở khóa. */
export function DrillForm({ initial, initialOrder, choices, references, busy, onSave, onCancel }: DrillFormProps) {
  const [config, setConfig] = useState<TrainingConfig>(() => ({ ...blankDrill(), ...(initial ?? {}) }));
  const [order, setOrder] = useState(initialOrder);
  const isNew = initial === null;
  const update = <K extends keyof TrainingConfig>(key: K, value: TrainingConfig[K]) => setConfig(old => ({ ...old, [key]: value }));
  const submit = (event: FormEvent) => { event.preventDefault(); onSave(config, order); };

  return (
    <form onSubmit={submit} className="space-y-6 rounded-xl border border-primary/30 bg-primary-soft/40 p-5">
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-display text-base font-bold text-foreground">{isNew ? 'Thêm bài tập mới' : `Sửa bài tập: ${initial?.title}`}</h3>
        <span className="text-[11px] text-muted-foreground">Lưu tạo bản nháp; xuất bản bài học để áp dụng cho game.</span>
      </div>

      <div className="space-y-4">
        <h4 className={sectionLabel}>Thông tin chung</h4>
        <div className="grid gap-4 md:grid-cols-2">
          <label className={fieldLabel}>
            Mã bài tập
            <input className={`${inputClass} font-mono`} required pattern="[a-z][a-z0-9-]{1,49}" value={config.id}
              disabled={!isNew} onChange={e => update('id', e.target.value)} />
          </label>
          <label className={fieldLabel}>
            Tên bài tập
            <input className={inputClass} required maxLength={254} value={config.title} onChange={e => update('title', e.target.value)} />
          </label>
          <label className={fieldLabel}>
            Mẫu kỹ thuật
            <select className={inputClass} value={config.skill} onChange={e => {
              const skill = e.target.value as Skill;
              setConfig(old => ({ ...old, skill, masks: skillTemplates[skill].masks, alternateTargets: false }));
            }}>
              {Object.entries(skillTemplates).map(([key, t]) => <option key={key} value={key}>{t.label}</option>)}
            </select>
          </label>
          <label className={fieldLabel}>
            Nhóm độ khó
            <select className={inputClass} value={config.tier} onChange={e => update('tier', +e.target.value)}>
              {tierNames.map((name, i) => <option key={name} value={i}>{name}</option>)}
            </select>
          </label>
          <label className={fieldLabel}>
            Phiên bản tham chiếu kỹ thuật
            <select className={inputClass} value={config.referenceId || ''} onChange={e => update('referenceId', e.target.value)}>
              <option value="">Tự chọn phiên bản mới nhất khi xuất bản</option>
              {references.filter(x => x.technique === config.skill).map(x => (
                <option value={x.referenceId} key={x.referenceId}>{x.technique} · v{x.version}</option>
              ))}
            </select>
          </label>
          <label className={fieldLabel}>
            Thứ tự trong bài học
            <input className={inputClass} type="number" value={order} onChange={e => setOrder(+e.target.value)} />
          </label>
        </div>
      </div>

      <div className="space-y-4">
        <h4 className={sectionLabel}>Thông số luyện tập</h4>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {numeric.map(([key, label, min, max, step]) => (
            <label key={key} className={fieldLabel}>
              {label}
              <input className={inputClass} type="number" required min={min} max={max} step={step}
                value={config[key]} onChange={e => update(key, +e.target.value)} />
            </label>
          ))}
        </div>
        <div className="space-y-2">
          {config.skill === 'Dink' && (
            <label className={checkboxRow}>
              <input type="checkbox" className="mt-0.5" checked={config.masks.every(m => m === 18)}
                onChange={e => update('masks', e.target.checked ? [18] : [2])} />
              Chỉ luyện Dink chéo sân
            </label>
          )}
          {config.skill === 'Serve' && (
            <label className={checkboxRow}>
              <input type="checkbox" className="mt-0.5" checked={config.alternateTargets}
                onChange={e => update('alternateTargets', e.target.checked)} />
              Luân phiên hai ô giao
            </label>
          )}
          <label className={checkboxRow}>
            <input type="checkbox" className="mt-0.5" checked={config.requireRecovery}
              onChange={e => update('requireRecovery', e.target.checked)} />
            Yêu cầu trở về vòng hồi vị sau cú đánh
          </label>
        </div>
      </div>

      <div className="space-y-4">
        <h4 className={sectionLabel}>Nội dung hướng dẫn</h4>
        <label className={fieldLabel}>
          Mục tiêu bài tập
          <textarea className={inputClass} required maxLength={254} rows={2} value={config.explanation}
            onChange={e => update('explanation', e.target.value)} />
        </label>
        <label className={fieldLabel}>
          Hướng dẫn và góp ý
          <textarea className={inputClass} required maxLength={2000} rows={3} value={config.coaching}
            onChange={e => update('coaching', e.target.value)} />
        </label>
      </div>

      <fieldset className="space-y-2">
        <legend className={sectionLabel}>Bài tập điều kiện</legend>
        <div className="grid gap-2 md:grid-cols-2">
          {choices.filter(c => c.code !== config.id).map(c => (
            <label key={c.code} className={`${checkboxRow} rounded-lg border border-border bg-card p-2.5`}>
              <input type="checkbox" className="mt-0.5" checked={config.prerequisites.includes(c.code)}
                onChange={e => update('prerequisites', e.target.checked
                  ? [...config.prerequisites, c.code]
                  : config.prerequisites.filter(id => id !== c.code))} />
              {c.title}
            </label>
          ))}
          {choices.length === 0 && <p className="text-xs text-muted-foreground">Chưa có bài tập nào khác.</p>}
        </div>
      </fieldset>

      <div className="flex flex-wrap items-center justify-end gap-3 border-t border-border pt-4">
        <button type="button" className={btnGhost} disabled={busy} onClick={onCancel}>Hủy</button>
        <button className={btnPrimary} disabled={busy}>{busy ? 'Đang lưu…' : isNew ? 'Thêm bài tập' : 'Lưu bài tập'}</button>
      </div>
    </form>
  );
}
