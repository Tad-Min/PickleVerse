import { FormEvent, useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { Plus, Pencil, Trash2, Layers } from 'lucide-react';
import { lessonApi } from '../../../api/lessonApi';
import { ContentPublishStatus } from '../../../types/status';
import { loadReferences } from './TechniqueReferences';
import { AdminDrill, TrainingConfig, skillTemplates, statusNames, tierNames } from './contentTypes';
import { DrillForm } from './DrillForm';
import {
  badgeBase, btnGhost, btnPrimary, card, fieldLabel, inputClass, notice, rowHover, sectionTitle, tone,
} from '../../../shared/ui/adminStyles';

const statusTone: Record<ContentPublishStatus, string> = {
  DRAFT: tone.amber, PUBLISHED: tone.emerald, UNPUBLISHED: tone.neutral,
} as Record<ContentPublishStatus, string>;

type Selection = number | 'new' | null;
const errorOf = (error: unknown) => axios.isAxiosError(error) ? error.response?.data?.message || error.message : String(error);

/**
 * Quản trị bài học và bài tập. Một bài học chứa nhiều bài tập; xuất bản theo bài học
 * để mọi bài tập đang soạn của bài học được áp dụng cùng lúc.
 */
export function LessonManager() {
  const client = useQueryClient();
  const references = useQuery({ queryKey: ['cms-technique-references'], queryFn: loadReferences });
  const query = useQuery({
    queryKey: ['cms-lessons'],
    queryFn: lessonApi.list,
    refetchOnWindowFocus: false,
  });

  const [selected, setSelected] = useState<Selection>(null);
  const [editing, setEditing] = useState<number | 'new' | null>(null);
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [form, setForm] = useState({ code: '', name: '', description: '', order: 1 });

  const lessons = query.data ?? [];
  const current = typeof selected === 'number' ? lessons.find(l => l.lessonId === selected) : undefined;
  const allDrills = lessons.flatMap(l => l.drills);
  const editingDrill: AdminDrill | undefined = typeof editing === 'number' ? current?.drills.find(d => d.drillId === editing) : undefined;

  useEffect(() => {
    if (current) setForm({ code: current.code, name: current.name, description: current.description, order: current.order });
  }, [current]);

  const refresh = () => client.invalidateQueries({ queryKey: ['cms-lessons'] });
  const ok = (text: string) => setFeedback({ type: 'success', text });
  const fail = (error: unknown) => setFeedback({ type: 'error', text: errorOf(error) });

  const createLesson = useMutation({
    mutationFn: () => lessonApi.create({ code: form.code, name: form.name, description: form.description, order: form.order }),
    onSuccess: async lesson => { setSelected(lesson.lessonId); ok('Đã tạo bài học. Hãy thêm bài tập rồi xuất bản.'); await refresh(); },
    onError: fail,
  });
  const saveLesson = useMutation({
    mutationFn: () => lessonApi.save(current!.lessonId, current!.version, { name: form.name, description: form.description, order: form.order }),
    onSuccess: async () => { ok('Đã lưu thông tin bài học.'); await refresh(); },
    onError: fail,
  });
  const saveDrill = useMutation({
    mutationFn: (input: { config: TrainingConfig; order: number }) => editingDrill
      ? lessonApi.saveDrill(current!.lessonId, editingDrill.drillId, current!.version, input)
      : lessonApi.addDrill(current!.lessonId, current!.version, input),
    onSuccess: async () => { setEditing(null); ok('Đã lưu bài tập vào bản nháp. Xuất bản bài học để áp dụng cho game.'); await refresh(); },
    onError: fail,
  });
  const deleteDrill = useMutation({
    mutationFn: (drill: AdminDrill) => lessonApi.deleteDrill(current!.lessonId, drill.drillId, current!.version),
    onSuccess: async () => { ok('Đã xóa bài tập.'); await refresh(); },
    onError: fail,
  });
  const publish = useMutation({
    mutationFn: async (kind: 'publish' | 'unpublish') => {
      await lessonApi.publish(current!.lessonId, current!.version, kind);
      return kind;
    },
    onSuccess: async kind => {
      ok(kind === 'publish' ? 'Đã xuất bản bài học và toàn bộ bài tập của bài học.' : 'Đã gỡ bài học khỏi danh mục mới.');
      await refresh();
    },
    onError: fail,
  });
  const busy = createLesson.isPending || saveLesson.isPending || saveDrill.isPending || deleteDrill.isPending || publish.isPending;

  const choose = (value: Selection) => {
    setSelected(value); setEditing(null); setFeedback(null);
    if (value === 'new') setForm({ code: '', name: '', description: '', order: lessons.length + 1 });
  };
  const submitLesson = (event: FormEvent) => {
    event.preventDefault();
    if (selected === 'new') createLesson.mutate(); else saveLesson.mutate();
  };
  const changedCount = current?.drills.filter(d => d.hasChanges).length ?? 0;

  return (
    <section className="grid items-start gap-5 lg:grid-cols-[300px_1fr]">
      {/* Danh sách bài học */}
      <aside className={`${card} space-y-3 lg:sticky lg:top-6`}>
        <div className="flex items-center justify-between gap-2">
          <h2 className={sectionTitle}>Bài học ({lessons.length})</h2>
          <button type="button" className={`${btnPrimary} inline-flex items-center gap-1`} onClick={() => choose('new')}>
            <Plus className="h-3.5 w-3.5" /> Thêm
          </button>
        </div>
        {query.isLoading && <p className="text-xs text-muted-foreground">Đang tải…</p>}
        {query.isError && (
          <p className="text-xs text-rose-600">
            Không tải được danh mục. <button className="underline" onClick={() => query.refetch()}>Thử lại</button>
          </p>
        )}
        <div className="max-h-[640px] space-y-2 overflow-y-auto pr-1">
          {lessons.map(row => (
            <button key={row.lessonId} type="button" disabled={busy} onClick={() => choose(row.lessonId)}
              className={`w-full rounded-lg border p-3 text-left transition-colors ${
                selected === row.lessonId ? 'border-primary bg-primary/5' : `border-border ${rowHover}`}`}>
              <span className="block text-xs font-semibold text-foreground">{row.name}</span>
              <span className="mt-1 flex flex-wrap items-center gap-2 text-[11px] text-muted-foreground">
                <span className={`${badgeBase} ${statusTone[row.status]}`}>{statusNames[row.status]}</span>
                {row.drills.length} bài tập
              </span>
            </button>
          ))}
          {query.data && lessons.length === 0 && <p className="text-xs text-muted-foreground">Chưa có bài học nào. Bấm "Thêm" để tạo bài học đầu tiên.</p>}
        </div>
      </aside>

      {/* Chi tiết bài học */}
      {selected === null ? (
        <div className="flex min-h-[240px] items-center justify-center rounded-2xl border border-dashed border-border bg-card p-10 text-sm text-muted-foreground">
          Chọn một bài học ở bên trái hoặc bấm "Thêm" để tạo bài học mới.
        </div>
      ) : (
        <div className="space-y-5">
          {feedback && (
            <p role="status" className={`${notice(feedback.type)} text-xs`}>{feedback.text}</p>
          )}

          <form onSubmit={submitLesson} className={`${card} space-y-4`}>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h2 className={sectionTitle}>{selected === 'new' ? 'Tạo bài học mới' : 'Thông tin bài học'}</h2>
              {current && <span className={`${badgeBase} ${statusTone[current.status]}`}>{statusNames[current.status]}</span>}
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              <label className={fieldLabel}>
                Mã bài học
                <input className={`${inputClass} font-mono`} required pattern="[a-z][a-z0-9-]{1,49}" value={form.code}
                  disabled={selected !== 'new'} onChange={e => setForm(f => ({ ...f, code: e.target.value }))} />
              </label>
              <label className={fieldLabel}>
                Tên bài học
                <input className={inputClass} required maxLength={254} value={form.name}
                  onChange={e => setForm(f => ({ ...f, name: e.target.value }))} />
              </label>
              <label className={`${fieldLabel} md:col-span-2`}>
                Mô tả
                <textarea className={inputClass} rows={2} maxLength={254} value={form.description}
                  onChange={e => setForm(f => ({ ...f, description: e.target.value }))} />
              </label>
              <label className={fieldLabel}>
                Thứ tự
                <input className={inputClass} type="number" value={form.order}
                  onChange={e => setForm(f => ({ ...f, order: +e.target.value }))} />
              </label>
            </div>
            <div className="flex justify-end">
              <button className={btnPrimary} disabled={busy}>
                {selected === 'new' ? 'Tạo bài học' : 'Lưu thông tin bài học'}
              </button>
            </div>
          </form>

          {current && (
            <div className={`${card} space-y-4`}>
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h2 className={`${sectionTitle} flex items-center gap-2`}><Layers className="h-4 w-4 text-primary" /> Bài tập ({current.drills.length})</h2>
                  <p className="mt-1 text-xs text-muted-foreground">Mỗi bài tập là một lượt luyện của người chơi trong bài học này.</p>
                </div>
                <button type="button" className={`${btnGhost} inline-flex items-center gap-1`} disabled={busy}
                  onClick={() => { setEditing('new'); setFeedback(null); }}>
                  <Plus className="h-3.5 w-3.5" /> Thêm bài tập
                </button>
              </div>

              {current.drills.length === 0 && editing !== 'new' && (
                <p className="rounded-lg border border-dashed border-border p-6 text-center text-xs text-muted-foreground">
                  Bài học chưa có bài tập nào. Bấm "Thêm bài tập" để bắt đầu.
                </p>
              )}

              <div className="space-y-2">
                {current.drills.map((drill, index) => (
                  <div key={drill.drillId} className={`flex flex-wrap items-center justify-between gap-3 rounded-xl border p-3 ${
                    editing === drill.drillId ? 'border-primary bg-primary/5' : 'border-border'}`}>
                    <div className="flex items-center gap-3">
                      <span className="flex h-7 w-7 items-center justify-center rounded-full bg-muted text-xs font-bold text-muted-foreground">{index + 1}</span>
                      <div>
                        <p className="text-xs font-semibold text-foreground">{drill.draft.title}</p>
                        <p className="mt-0.5 flex flex-wrap items-center gap-2 text-[11px] text-muted-foreground">
                          <span className="font-mono">{drill.code}</span>
                          · {skillTemplates[drill.draft.skill]?.label ?? drill.draft.skill} · {tierNames[drill.draft.tier] ?? '—'}
                          · đạt {drill.draft.required}/{drill.draft.trials} lượt
                          {drill.hasChanges && <span className={`${badgeBase} ${tone.amber}`}>{drill.published ? 'Có thay đổi' : 'Chưa xuất bản'}</span>}
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <button type="button" className={`${btnGhost} inline-flex items-center gap-1`} disabled={busy}
                        onClick={() => { setEditing(drill.drillId); setFeedback(null); }}>
                        <Pencil className="h-3.5 w-3.5" /> Sửa
                      </button>
                      <button type="button" className="inline-flex items-center gap-1 rounded-lg border border-rose-500/20 bg-rose-500/10 px-3 py-1.5 text-xs font-semibold text-rose-600 hover:bg-rose-500/20 disabled:opacity-40"
                        disabled={busy} onClick={() => { if (window.confirm(`Xóa bài tập "${drill.draft.title}"?`)) deleteDrill.mutate(drill); }}>
                        <Trash2 className="h-3.5 w-3.5" /> Xóa
                      </button>
                    </div>
                  </div>
                ))}
              </div>

              {editing !== null && (
                <DrillForm
                  key={editingDrill?.drillId ?? 'new'}
                  initial={editingDrill?.draft ?? null}
                  initialOrder={editingDrill?.order ?? current.drills.length + 1}
                  choices={allDrills.map(d => ({ code: d.code, title: d.draft.title }))}
                  references={references.data ?? []}
                  busy={saveDrill.isPending}
                  onSave={(config, order) => saveDrill.mutate({ config, order })}
                  onCancel={() => setEditing(null)}
                />
              )}

              <div className="flex flex-wrap items-center justify-between gap-3 border-t border-border pt-5">
                <p className="text-xs text-muted-foreground">
                  {changedCount > 0 ? `${changedCount} bài tập có thay đổi chưa xuất bản.` : 'Toàn bộ bài tập đã được xuất bản.'}
                </p>
                <div className="flex flex-wrap items-center gap-3">
                  {current.status === ContentPublishStatus.Published && (
                    <button className={btnGhost} type="button" disabled={busy} onClick={() => publish.mutate('unpublish')}>Gỡ xuất bản</button>
                  )}
                  <button className={btnPrimary} type="button" disabled={busy || current.drills.length === 0} onClick={() => publish.mutate('publish')}>
                    {publish.isPending ? 'Đang xuất bản…' : 'Xuất bản bài học'}
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      )}
    </section>
  );
}
