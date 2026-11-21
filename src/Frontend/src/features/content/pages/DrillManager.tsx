import { FormEvent, useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { apiClient } from '../../../api/client';
import { ContentPublishStatus } from '../../../types/status';
import { loadReferences } from './TechniqueReferences';
import { LessonContent, names } from './LessonManager';
import {
  badgeBase, btnGhost, btnPrimary, card, fieldLabel, inputClass, notice, rowHover, sectionTitle, tone,
} from '../../../shared/ui/adminStyles';

interface DrillForm {
  drillName: string;
  description: string;
  target: number;
}

const errorMessage = (error: unknown) =>
  axios.isAxiosError(error) ? error.response?.data?.message || error.message : String(error);

const statusTone: Record<ContentPublishStatus, string> = {
  [ContentPublishStatus.Draft]: tone.amber,
  [ContentPublishStatus.Published]: tone.emerald,
  [ContentPublishStatus.Unpublished]: tone.neutral,
};

/**
 * Form "Thêm bài tập (Drill)". Backend gắn drill vào một bài học có sẵn,
 * nên người dùng chọn bài học ở danh mục bên trái rồi sửa drill của bài đó.
 */
export function DrillManager() {
  const client = useQueryClient();
  const references = useQuery({ queryKey: ['cms-technique-references'], queryFn: loadReferences });
  const query = useQuery({
    queryKey: ['cms-lessons'],
    queryFn: async () => (await apiClient.get<LessonContent[]>('/api/Admin/cms/lessons')).data,
    refetchOnWindowFocus: false,
  });
  const [selected, setSelected] = useState<number | null>(null);
  const [form, setForm] = useState<DrillForm>({ drillName: '', description: '', target: 6 });
  const [feedback, setFeedback] = useState('');

  const current = query.data?.find((x) => x.lessonId === selected);
  // Kỹ thuật của drill lấy theo mẫu kỹ thuật của bài học; backend yêu cầu khớp với drill hiện có.
  const techniqueId = references.data?.find((x) => x.technique === current?.draft.skill)?.techniqueId;

  useEffect(() => {
    if (current) {
      setForm({
        drillName: current.draft.title,
        description: current.draft.explanation,
        target: current.draft.required,
      });
    }
  }, [current]);

  const save = useMutation({
    mutationFn: async () => {
      if (!current || techniqueId === undefined) throw new Error('Chưa chọn bài học hợp lệ.');
      await apiClient.post('/api/Admin/cms/drills', {
        lessonId: current.lessonId,
        techniqueId,
        drillName: form.drillName,
        description: form.description,
        target: form.target,
        order: current.order,
      });
    },
    onSuccess: async () => {
      setFeedback('Đã lưu bài tập vào bản nháp. Xuất bản bài học để áp dụng cho game.');
      await client.invalidateQueries({ queryKey: ['cms-lessons'] });
    },
    onError: (error) => setFeedback(errorMessage(error)),
  });

  const publish = useMutation({
    mutationFn: async (kind: 'publish' | 'unpublish') => {
      if (!current) throw new Error('Chưa chọn bài học.');
      await apiClient.post(`/api/Admin/cms/lessons/${current.lessonId}/${kind}`, { expectedVersion: current.version });
      return kind;
    },
    onSuccess: async (kind) => {
      setFeedback(kind === 'publish' ? 'Đã xuất bản bài học và bài tập.' : 'Đã gỡ bài khỏi danh mục mới.');
      await client.invalidateQueries({ queryKey: ['cms-lessons'] });
    },
    onError: (error) => setFeedback(errorMessage(error)),
  });

  const busy = save.isPending || publish.isPending;
  const canSave = !!current && techniqueId !== undefined && !busy;

  const submit = (event: FormEvent) => {
    event.preventDefault();
    save.mutate();
  };

  return (
    <section className="grid gap-5 lg:grid-cols-[300px_1fr] items-start">
      {/* Lesson picker */}
      <aside className={`${card} space-y-3 lg:sticky lg:top-6`}>
        <h2 className={sectionTitle}>Chọn bài học ({query.data?.length ?? 0})</h2>
        {query.isLoading && <p className="text-xs text-muted-foreground">Đang tải…</p>}
        {query.isError && (
          <p className="text-xs text-rose-600">
            Không tải được danh mục.{' '}
            <button className="underline" onClick={() => query.refetch()}>Thử lại</button>
          </p>
        )}
        {query.data && query.data.length === 0 && (
          <p className="text-xs text-muted-foreground">Chưa có bài học nào. Hãy tạo bài học ở tab 1 trước.</p>
        )}
        <div className="max-h-[640px] space-y-2 overflow-y-auto pr-1">
          {query.data?.map((row) => (
            <button
              key={row.lessonId}
              type="button"
              disabled={busy}
              onClick={() => {
                setSelected(row.lessonId);
                setFeedback('');
              }}
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
        </div>
      </aside>

      {!current ? (
        <div className="flex min-h-[240px] items-center justify-center rounded-xl border border-dashed border-border bg-card p-10 text-sm text-muted-foreground">
          Chọn một bài học ở danh mục bên trái để thêm hoặc sửa bài tập.
        </div>
      ) : (
        <form onSubmit={submit} className={`${card} space-y-6`}>
          <div>
            <h2 className={sectionTitle}>Thêm bài tập (Drill)</h2>
            <p className="mt-1 text-xs text-muted-foreground">
              Bài tập thuộc bài học đã chọn. Lưu tạo bản nháp; xuất bản bài học để áp dụng cho lượt chơi mới.
            </p>
          </div>

          {/* Lesson summary */}
          <div className="rounded-lg border border-border bg-background p-4 text-xs">
            <div className="grid gap-3 md:grid-cols-3">
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">Bài học</p>
                <p className="mt-1 font-semibold text-foreground">{current.draft.title}</p>
                <p className="font-mono text-[11px] text-muted-foreground">{current.code}</p>
              </div>
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">Mẫu kỹ thuật</p>
                <p className="mt-1 font-semibold text-foreground">{current.draft.skill}</p>
              </div>
              <div>
                <p className="text-[11px] font-semibold uppercase tracking-wider text-muted-foreground">Trạng thái</p>
                <span className={`${badgeBase} mt-1 ${statusTone[current.status]}`}>{names[current.status]}</span>
              </div>
            </div>
            {techniqueId === undefined && (
              <p className={`${notice('error')} mt-3`}>
                Chưa có phiên bản tham chiếu kỹ thuật cho mẫu {current.draft.skill}. Hãy tạo ở mục Dữ liệu kỹ thuật tham chiếu trước.
              </p>
            )}
          </div>

          {feedback && <p role="status" className={`${notice(feedback.startsWith('Đã') ? 'success' : 'error')} text-xs`}>{feedback}</p>}

          <div className="grid gap-4 md:grid-cols-2">
            <label className={fieldLabel}>
              Tên bài tập
              <input
                className={inputClass}
                required
                maxLength={254}
                value={form.drillName}
                onChange={(e) => setForm((old) => ({ ...old, drillName: e.target.value }))}
              />
            </label>
            <label className={fieldLabel}>
              Số lượt đạt tối thiểu
              <input
                className={inputClass}
                type="number"
                required
                min={1}
                max={200}
                step={1}
                value={form.target}
                onChange={(e) => setForm((old) => ({ ...old, target: +e.target.value }))}
              />
            </label>
          </div>

          <label className={fieldLabel}>
            Mô tả bài tập
            <textarea
              className={inputClass}
              maxLength={254}
              rows={3}
              value={form.description}
              onChange={(e) => setForm((old) => ({ ...old, description: e.target.value }))}
            />
          </label>

          <div className="flex flex-wrap items-center justify-end gap-3 border-t border-border pt-5">
            {current.status === ContentPublishStatus.Published && (
              <button type="button" className={btnGhost} disabled={busy} onClick={() => publish.mutate('unpublish')}>
                Gỡ xuất bản
              </button>
            )}
            <button type="button" className={btnGhost} disabled={busy} onClick={() => publish.mutate('publish')}>
              Xuất bản bài học
            </button>
            <button className={btnPrimary} disabled={!canSave}>
              Lưu bản nháp
            </button>
          </div>
        </form>
      )}
    </section>
  );
}
