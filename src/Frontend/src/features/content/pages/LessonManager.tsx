import { loadReferences, ReferenceParameters } from './TechniqueReferences';
import { FormEvent, useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { apiClient } from '../../../api/client';
import { ContentPublishStatus } from '../../../types/status';

type Skill = 'Serve' | 'Drive' | 'Dink' | 'Volley' | 'Modifiers' | 'Drop' | 'Reset' | 'Smash' | 'Footwork' | 'Approach' | 'DoublesCover' | 'DoublesTransition';
interface TrainingConfig {
  referenceId?: string; reference?: ReferenceParameters;
  id: string; title: string; explanation: string; coaching: string; skill: Skill;
  tier: number; trials: number; required: number; reward: number; xp: number;
  requiredLevel: number; targetRadius: number; targetDepth: number;
  alternateTargets: boolean; requireRecovery: boolean; masks: number[]; prerequisites: string[];
}
interface LessonContent {
  lessonId: number; code: string; status: ContentPublishStatus; version: number; order: number;
  draft: TrainingConfig; published: TrainingConfig | null;
}
const templates: Record<Skill, { label: string; masks: number[] }> = {
  Serve: { label: 'Giao bóng', masks: [1] }, Drive: { label: 'Drive', masks: [1] }, Dink: { label: 'Dink', masks: [2] },
  Volley: { label: 'Volley', masks: [4] }, Modifiers: { label: 'Lực và hướng', masks: [9,17,10,18,12,20] },
  Drop: { label: 'Third-shot drop', masks: [3] }, Reset: { label: 'Reset bóng', masks: [6] }, Smash: { label: 'Smash', masks: [5] },
  Footwork: { label: 'Di chuyển', masks: [1,17] }, Approach: { label: 'Drop và tiến lên', masks: [3] },
  DoublesCover: { label: 'Đội hình đôi', masks: [4,20] }, DoublesTransition: { label: 'Phối hợp đôi', masks: [3,2,4] },
};
const names: Record<ContentPublishStatus, string> = { DRAFT: 'Bản nháp', PUBLISHED: 'Đang xuất bản', UNPUBLISHED: 'Đã gỡ' };
const blank = (): TrainingConfig => ({ id: '', title: '', explanation: '', coaching: '', skill: 'Serve', tier: 0, trials: 8,
  required: 6, reward: 150, xp: 5, requiredLevel: 1, targetRadius: .9, targetDepth: 0, alternateTargets: false,
  requireRecovery: false, masks: [1], prerequisites: [] });
const input = 'w-full rounded-lg border border-border bg-background px-3 py-2 text-sm';
const button = 'rounded-lg border border-border px-4 py-2 text-sm hover:bg-accent disabled:opacity-40';

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
  const action = useMutation({ mutationFn: async (kind: 'save' | 'publish' | 'unpublish') => {
    if (kind === 'save' && !current) {
      const result = await apiClient.post<{ lessonId: number }>('/api/Admin/cms/lessons', { code: config.id, lessonName: config.title,
        description: config.explanation, requiredLevel: config.requiredLevel, order, config });
      setSelected(result.data.lessonId);
    } else if (current) {
      const route = `/api/Admin/cms/lessons/${current.lessonId}`;
      if (kind === 'save') await apiClient.put(route, { expectedVersion: current.version, order, config });
      else await apiClient.post(`${route}/${kind}`, { expectedVersion: current.version });
    }
    return kind;
  }, onSuccess: async kind => {
    setFeedback(kind === 'save' ? 'Đã lưu bản nháp. Xuất bản để đưa thay đổi vào game.' : kind === 'publish' ? 'Đã xuất bản bài học.' : 'Đã gỡ bài khỏi danh mục mới.');
    await client.invalidateQueries({ queryKey: ['cms-lessons'] });
  }, onError: error => setFeedback(axios.isAxiosError(error) ? error.response?.data?.message || error.message : String(error)) });
  const submit = (event: FormEvent) => { event.preventDefault(); action.mutate('save'); };
  const numeric: [keyof Pick<TrainingConfig, 'trials'|'required'|'reward'|'xp'|'requiredLevel'|'targetRadius'|'targetDepth'>, string, number, number, number][] = [
    ['trials','Số lượt',1,200,1], ['required','Số lượt đạt tối thiểu',1,200,1], ['reward','Thưởng TOKENPAD',0,100000,1],
    ['xp','Thưởng XP',0,10000,1], ['requiredLevel','Cấp độ tối thiểu',1,1000,1], ['targetRadius','Bán kính đích (m)',.3,2,.05],
    ['targetDepth','Độ sâu đích (m; 0 = theo mẫu)',0,6.4,.05],
  ];
  return <section className="grid gap-5 lg:grid-cols-[290px_1fr]">
    <aside className="space-y-3 rounded-xl border border-border bg-card p-4">
      <div className="flex items-center justify-between"><h2 className="font-semibold">Danh mục ({query.data?.length ?? 0})</h2>
        <button type="button" className={button} onClick={() => { setSelected(null); setConfig(blank()); setFeedback(''); }}>Tạo bài</button></div>
      {query.isLoading && <p>Đang tải…</p>}
      {query.isError && <p className="text-red-400">Không tải được danh mục. <button onClick={() => query.refetch()}>Thử lại</button></p>}
      <div className="max-h-[700px] space-y-2 overflow-y-auto">{query.data?.map(row => <button key={row.lessonId} type="button"
        disabled={action.isPending} onClick={() => { setSelected(row.lessonId); setFeedback(''); }}
        className={`w-full rounded-lg border p-3 text-left ${selected === row.lessonId ? 'border-primary bg-primary/10' : 'border-border'}`}>
        <span className="block text-sm font-medium">{row.draft.title}</span><span className="text-xs text-muted-foreground">{names[row.status]} · Phiên bản {row.version}</span>
      </button>)}</div>
    </aside>
    <form onSubmit={submit} className="space-y-5 rounded-xl border border-border bg-card p-5">
        <label className="block text-sm">Phiên bản tham chiếu kỹ thuật
          <select className={input} value={config.referenceId || ''} onChange={e => update('referenceId', e.target.value)}>
            <option value="">Tự chọn phiên bản mới nhất khi xuất bản</option>
            {references.data?.filter(x => x.technique === config.skill).map(x => <option value={x.referenceId} key={x.referenceId}>{x.technique} · v{x.version}</option>)}
          </select>
        </label>
      <div><h2 className="text-lg font-semibold">{current ? 'Chỉnh sửa bài học và drill' : 'Tạo bài học từ mẫu'}</h2>
        <p className="mt-1 text-xs text-muted-foreground">Lưu tạo bản nháp. Xuất bản áp dụng cấu hình cho lượt chơi mới; lượt đang tập giữ phiên bản cũ.</p></div>
      {feedback && <p role="status" className="rounded-lg border border-border bg-background p-3 text-sm">{feedback}</p>}
      <div className="grid gap-4 md:grid-cols-2">
        <label className="space-y-1 text-sm">Mã bài<input className={input} required pattern="[a-z][a-z0-9-]{1,49}" value={config.id} disabled={!!current} onChange={e => update('id',e.target.value)} /></label>
        <label className="space-y-1 text-sm">Tên bài<input className={input} required maxLength={254} value={config.title} onChange={e => update('title',e.target.value)} /></label>
        <label className="space-y-1 text-sm">Mẫu kỹ thuật<select className={input} value={config.skill} onChange={e => {
          const skill = e.target.value as Skill; setConfig(old => ({ ...old, skill, masks: templates[skill].masks, alternateTargets: false }));
        }}>{Object.entries(templates).map(([key,t]) => <option key={key} value={key}>{t.label}</option>)}</select></label>
        <label className="space-y-1 text-sm">Nhóm độ khó<select className={input} value={config.tier} onChange={e => update('tier',+e.target.value)}>
          {['Cơ bản','Kiểm soát','Nâng cao','Đánh đôi'].map((name,i) => <option key={name} value={i}>{name}</option>)}</select></label>
        {numeric.map(([key,label,min,max,step]) => <label key={key} className="space-y-1 text-sm">{label}<input className={input} type="number" required min={min} max={max} step={step} value={config[key]} onChange={e => update(key,+e.target.value)} /></label>)}
        <label className="space-y-1 text-sm">Thứ tự<input className={input} type="number" value={order} onChange={e => setOrder(+e.target.value)} /></label>
      </div>
      {config.skill === 'Dink' && <label className="flex gap-2 text-sm"><input type="checkbox" checked={config.masks.every(m => m === 18)}
        onChange={e => update('masks',e.target.checked ? [18] : [2])} />Chỉ luyện Dink chéo sân</label>}
      {config.skill === 'Serve' && <label className="flex gap-2 text-sm"><input type="checkbox" checked={config.alternateTargets} onChange={e => update('alternateTargets',e.target.checked)} />Luân phiên hai ô giao</label>}
      <label className="flex gap-2 text-sm"><input type="checkbox" checked={config.requireRecovery} onChange={e => update('requireRecovery',e.target.checked)} />Yêu cầu trở về vòng hồi vị sau cú đánh</label>
      <label className="block space-y-1 text-sm">Mục tiêu bài<textarea className={input} required maxLength={254} rows={2} value={config.explanation} onChange={e => update('explanation',e.target.value)} /></label>
      <label className="block space-y-1 text-sm">Hướng dẫn và góp ý<textarea className={input} required maxLength={2000} rows={3} value={config.coaching} onChange={e => update('coaching',e.target.value)} /></label>
      <fieldset className="space-y-2"><legend className="text-sm font-medium">Bài điều kiện</legend><div className="grid gap-2 md:grid-cols-2">{query.data?.filter(row => row.code !== config.id).map(row => <label key={row.code} className="flex items-center gap-2 text-xs">
        <input type="checkbox" checked={config.prerequisites.includes(row.code)} onChange={e => update('prerequisites',e.target.checked ? [...config.prerequisites,row.code] : config.prerequisites.filter(id => id !== row.code))} />{row.draft.title}
      </label>)}</div></fieldset>
      <div className="flex flex-wrap gap-3 border-t border-border pt-4">
        <button className={`${button} bg-primary text-primary-foreground`} disabled={action.isPending}>Lưu bản nháp</button>
        {current && <button className={button} type="button" disabled={action.isPending} onClick={() => action.mutate('publish')}>Xuất bản bản đã lưu</button>}
        {current?.status === ContentPublishStatus.Published && <button className={button} type="button" disabled={action.isPending} onClick={() => action.mutate('unpublish')}>Gỡ xuất bản</button>}
      </div>
    </form>
  </section>;
}
