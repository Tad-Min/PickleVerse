import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import axios from 'axios';
import { apiClient } from '../../../api/client';
import { AdminUserDto } from '../../../types/api';
import { ProgressResetGroup } from '../../../types/status';
import { btnGhost, badgeBase, notice, sectionTitle, tone } from '../../../shared/ui/adminStyles';

interface Preview {
  revision: number; busy: boolean; effects: string[]; level: number; elo: number; tokenpad: number; elite: number;
  lessonRecords: number; masteryRecords: number; missionRecords: number; achievementRecords: number; ownedCosmetics: string[];
}

const choices: [ProgressResetGroup, string, string][] = [
  [ProgressResetGroup.Learning, 'Học tập', 'XP/cấp độ, bài học, mức thành thạo và huấn luyện'],
  [ProgressResetGroup.Ranking, 'Xếp hạng', 'Elo về 100 (mức khởi đầu) và xóa chuỗi xếp hạng'],
  [ProgressResetGroup.Missions, 'Nhiệm vụ & thành tựu', 'Bộ đếm nhiệm vụ, thành tựu và thống kê tiến trình'],
  [ProgressResetGroup.Economy, 'Kinh tế', 'Tiền và vật phẩm về tài khoản mới'],
];

export function ProgressResetDialog({ user, onClose, onDone }: { user: AdminUserDto; onClose: () => void; onDone: (message: string) => void }) {
  const [groups, setGroups] = useState<ProgressResetGroup[]>([ProgressResetGroup.Learning]);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [requestId, setRequestId] = useState(() => crypto.randomUUID());
  const [error, setError] = useState('');

  const showError = (cause: unknown) =>
    setError(axios.isAxiosError(cause) ? cause.response?.data?.message || cause.message : String(cause));

  const inspect = useMutation({
    mutationFn: async () =>
      (await apiClient.post<Preview>(`/api/Admin/users/${user.userId}/progress-reset/preview`, { groups })).data,
    onSuccess: (value) => {
      setPreview(value);
      setRequestId(crypto.randomUUID());
      setError('');
    },
    onError: showError,
  });

  const execute = useMutation({
    mutationFn: async () =>
      (await apiClient.post<{ message: string }>(`/api/Admin/users/${user.userId}/progress-reset`, {
        groups,
        expectedRevision: preview!.revision,
        requestId,
      })).data,
    onSuccess: (value) => onDone(value.message),
    onError: (cause) => {
      showError(cause);
      if (axios.isAxiosError(cause) && cause.response?.status === 409) setPreview(null);
    },
  });

  const busy = inspect.isPending || execute.isPending;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
      role="dialog"
      aria-modal="true"
      aria-label="Đặt lại tiến trình"
    >
      <section className="max-h-[90vh] w-full max-w-2xl space-y-5 overflow-y-auto rounded-xl border border-border bg-card p-6 shadow-2xl">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <h2 className={sectionTitle}>Đặt lại tiến trình</h2>
            <span className={`${badgeBase} ${tone.rose}`}>Không thể hoàn tác</span>
          </div>
          <p className="mt-1 text-xs text-muted-foreground">
            {user.playerName} · {user.email}
          </p>
        </div>

        <fieldset className="space-y-2" disabled={busy}>
          <legend className="mb-2 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            Chọn nhóm dữ liệu
          </legend>
          {choices.map(([key, title, description]) => (
            <label
              key={key}
              className={`flex cursor-pointer items-start gap-3 rounded-lg border p-3 transition-colors ${
                groups.includes(key) ? 'border-primary bg-primary/5' : 'border-border bg-background hover:bg-muted/50'
              }`}
            >
              <input
                type="checkbox"
                className="mt-0.5"
                checked={groups.includes(key)}
                onChange={(event) => {
                  setGroups(event.target.checked ? [...groups, key] : groups.filter((g) => g !== key));
                  setPreview(null);
                  setError('');
                }}
              />
              <span>
                <span className="block text-xs font-semibold text-foreground">{title}</span>
                <span className="block text-[11px] text-muted-foreground">{description}</span>
              </span>
            </label>
          ))}
        </fieldset>

        <p className="text-xs text-muted-foreground">
          Giữ tài khoản, bạn bè, lịch sử trận, quyết định xử lý và nhật ký thưởng. Tiền/cosmetic đã cấp không được nhận lại sau reset.
        </p>

        {preview && (
          <div className="space-y-3 rounded-lg border border-amber-500/30 bg-amber-500/5 p-4">
            <h3 className="text-sm font-semibold text-foreground">Tác động đã xem trước</h3>
            <ul className="list-disc space-y-1.5 pl-5 text-xs text-foreground">
              {preview.effects.map((effect) => <li key={effect}>{effect}</li>)}
            </ul>
            <div className="grid gap-2 text-xs text-muted-foreground md:grid-cols-2">
              <p>Cấp {preview.level} · Elo {preview.elo}</p>
              <p>{preview.tokenpad} TOKENPAD · {preview.elite} Token cao cấp</p>
              <p>{preview.ownedCosmetics.length} vật phẩm</p>
              <p>
                {preview.lessonRecords} kết quả bài · {preview.masteryRecords} mức thành thạo · {preview.missionRecords} nhiệm vụ ·{' '}
                {preview.achievementRecords} thành tựu
              </p>
            </div>
            {preview.busy && (
              <p className="text-xs text-rose-600">
                Người chơi đang tìm trận hoặc có trận chưa hoàn tất. Chưa thể reset.
              </p>
            )}
          </div>
        )}

        {error && <p role="alert" className={`${notice('error')} text-xs`}>{error}</p>}

        <footer className="flex flex-wrap justify-end gap-3 border-t border-border pt-4">
          <button onClick={onClose} disabled={busy} className={btnGhost}>
            Hủy
          </button>
          <button onClick={() => inspect.mutate()} disabled={busy || groups.length === 0} className={btnGhost}>
            {inspect.isPending ? 'Đang kiểm tra…' : 'Xem trước tác động'}
          </button>
          <button
            onClick={() => execute.mutate()}
            disabled={busy || !preview || preview.busy}
            className="px-4 py-1.5 text-xs font-semibold rounded-lg bg-amber-600 hover:bg-amber-500 text-white shadow-sm disabled:opacity-40"
          >
            {execute.isPending ? 'Đang đặt lại…' : 'Xác nhận đặt lại'}
          </button>
        </footer>
      </section>
    </div>
  );
}
