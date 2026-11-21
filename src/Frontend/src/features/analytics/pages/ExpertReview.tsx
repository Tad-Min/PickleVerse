import { FormEvent, useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { apiClient } from '../../../api/client';
import { ClipboardCheck } from 'lucide-react';
import { PageHeader } from '../../../shared/ui/PageHeader';
import {
  badgeBase, btnGhost, btnPrimary, btnTab, card, fieldLabel, inputClass, notice, rowHover,
  sectionTitle, tableHead, th, td, tone,
} from '../../../shared/ui/adminStyles';

type Split = 'train' | 'calibration' | 'test';
type Tier = 'beginner' | 'intermediate' | 'advanced';
interface Session {
  batchId: string;
  sessionId: string;
  playerId: string;
  playerName: string;
  lessonTitle: string;
  catalogVersion: string;
  completedAt: string;
  reviewedVersion: number;
}
interface Shot {
  shotId: string;
  sequence: number;
  kind: string;
  technique: string;
  outcome: string;
  assisted: boolean;
  engineAimed: boolean;
  rawPressedAt?: number;
  rawReleasedAt?: number;
  rawTimingError?: number;
  rawTechnique?: string;
  rawInputMask?: number;
  rawCrossCourt?: boolean;
  rawPower?: boolean;
  rawPosition?: number[];
  contact?: number[];
  landing?: number[];
  leftFoot?: number[];
  rightFoot?: number[];
  netClearance?: number;
  referenceId?: string;
  recovered?: boolean;
}
interface DemoSession { sessionId: string; playerCode: string; tier: Tier; split: Split }
interface DemoEvidence { session: DemoSession; gameVersion: string; shots: Shot[] }
interface ShotLabel { shotId: string; code: string; present: boolean; evidence: string }
interface Review {
  expectedVersion: number;
  tier: Tier | null;
  split: Split | null;
  expertName: string;
  observation: string;
  confirmedHumanSession: boolean;
  shotLabels: ShotLabel[];
}
interface Evidence {
  session: Session;
  gameVersion: string;
  shots: Shot[];
  latestReview?: { version: number; reviewerId: string; createdAt: string; review: Review } | null;
}

const codes: Record<string, string> = {
  Early: 'Thả nút sớm', Late: 'Thả nút muộn', LateServe: 'Hết giờ giao bóng',
  Kitchen: 'Vô lê trong vùng cấm', Momentum: 'Đà vô lê vào vùng cấm',
  Net: 'Bóng chạm lưới', Out: 'Bóng ngoài sân', Target: 'Sai vùng đích',
  Movement: 'Chưa hồi vị', DinkTooHigh: 'Quỹ đạo Dink cao',
  ServeFeet: 'Vị trí chân giao bóng', WrongInput: 'Sai tổ hợp', Spacing: 'Sai khoảng cách đội hình',
  Miss: 'Đánh hụt', DoubleBounce: 'Bóng nảy hai lần',
};
const MSG64 = 'Hãy xác nhận phiên người thật, người đánh giá, bằng chứng và ít nhất một nhãn.';
const blank = (): Review => ({
  expectedVersion: 0, tier: null, split: null, expertName: '', observation: '',
  confirmedHumanSession: false, shotLabels: [],
});
const number = (value?: number) => (value == null ? 'Chưa ghi nhận' : value.toFixed(3));
const point = (value?: number[]) => value?.map((x) => x.toFixed(2)).join(', ') || 'Chưa ghi nhận';
const errorText = (error: unknown) =>
  (error as { response?: { data?: { message?: string } } })?.response?.data?.message ||
  (error as Error)?.message ||
  'Không thực hiện được yêu cầu.';
const tierNames: Record<Tier, string> = { beginner: 'Mới chơi', intermediate: 'Trung cấp', advanced: 'Nâng cao' };
const splitNames: Record<Split, string> = { train: 'Huấn luyện', calibration: 'Hiệu chỉnh', test: 'Kiểm thử (holdout)' };
const dateTime = (value: string) => new Date(value).toLocaleString('vi-VN');

const kvLabel = 'text-[11px] font-semibold uppercase tracking-wider text-muted-foreground';
const kvValue = 'text-xs text-foreground';

export function ExpertReview() {
  const [params, setParams] = useSearchParams();
  const demo = params.get('source') === 'demo';
  return (
    <div className="space-y-6">
      <PageHeader
        icon={ClipboardCheck}
        title="Gán nhãn phiên luyện tập"
        subtitle="Xác nhận trình độ và từng lỗi kỹ thuật từ telemetry kết hợp quan sát của chuyên gia"
      />

      <div className="flex flex-wrap items-center gap-2" aria-label="Nguồn phiên luyện tập">
        <button type="button" className={btnTab(!demo)} aria-pressed={!demo} onClick={() => setParams({})}>
          Phiên người chơi
        </button>
        <button type="button" className={btnTab(demo)} aria-pressed={demo} onClick={() => setParams({ source: 'demo' })}>
          Phiên mô phỏng
        </button>
      </div>

      {demo ? <SimulatedSessions /> : <ExpertReviewForm />}
    </div>
  );
}

function SimulatedSessions() {
  const [choice, setChoice] = useState('');
  const sessions = useQuery({
    queryKey: ['coaching-demo-sessions'],
    queryFn: async () => (await apiClient.get<DemoSession[]>('/api/Admin/coaching/demo-sessions')).data,
  });
  const selected = choice || sessions.data?.[0]?.sessionId || '';
  const detail = useQuery({
    queryKey: ['coaching-demo-evidence', selected],
    queryFn: async () =>
      (await apiClient.get<DemoEvidence>(`/api/Admin/coaching/demo-sessions/${encodeURIComponent(selected)}`)).data,
    enabled: selected !== '',
  });

  return (
    <section className={`${card} space-y-5`}>
      <div>
        <h2 className={sectionTitle}>Phiên mô phỏng</h2>
        <p className="text-xs text-muted-foreground mt-1">
          Dữ liệu minh họa, chỉ xem. Các phiên này được lưu riêng với phiên người thật.
        </p>
      </div>

      {sessions.error && <p role="alert" className="text-xs text-rose-600">{errorText(sessions.error)}</p>}
      {sessions.isLoading && <p className="text-xs text-muted-foreground">Đang tải danh sách…</p>}
      {sessions.data?.length === 0 && <p className="text-xs text-muted-foreground">Database này chưa nạp phiên mô phỏng.</p>}

      {Boolean(sessions.data?.length) && (
        <label className={`${fieldLabel} md:w-[28rem]`}>
          Chọn phiên ({sessions.data?.length} phiên)
          <select className={inputClass} value={selected} onChange={(event) => setChoice(event.target.value)}>
            {sessions.data?.map((session) => (
              <option key={session.sessionId} value={session.sessionId}>
                {session.playerCode} · {tierNames[session.tier]} · {splitNames[session.split]}
              </option>
            ))}
          </select>
        </label>
      )}

      {detail.error && <p role="alert" className="text-xs text-rose-600">{errorText(detail.error)}</p>}
      {selected && detail.isLoading && <p className="text-xs text-muted-foreground">Đang tải cú đánh…</p>}

      {detail.data && (
        <>
          <div className="flex flex-wrap items-center gap-2">
            <span className={`${badgeBase} ${tone.blue}`}>Volley</span>
            <span className={`${badgeBase} ${tone.neutral}`}>{detail.data.shots.length} lượt</span>
            <span className={`${badgeBase} ${tone.neutral}`}>{tierNames[detail.data.session.tier]}</span>
            <span className={`${badgeBase} ${tone.neutral}`}>{splitNames[detail.data.session.split]}</span>
            <span className="text-xs text-muted-foreground">
              Game {detail.data.gameVersion} · Có hỗ trợ tiếp xúc và tự ngắm
            </span>
          </div>
          <div className="overflow-x-auto rounded-lg border border-border">
            <table className="w-full whitespace-nowrap text-left text-xs">
              <thead className={tableHead}>
                <tr>
                  {['Lượt', 'Bấm / thả (s)', 'Sai lệch (s)', 'Kỹ thuật / mã nút', 'Vị trí trước hỗ trợ (m)', 'Kết quả', 'Hồi vị'].map(
                    (header) => (
                      <th key={header} scope="col" className={th}>
                        {header}
                      </th>
                    ),
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {detail.data.shots.map((shot) => (
                  <tr key={shot.shotId} className={rowHover}>
                    <td className={`${td} font-mono`}>{shot.sequence + 1}</td>
                    <td className={`${td} font-mono`}>
                      {number(shot.rawPressedAt)} / {number(shot.rawReleasedAt)}
                    </td>
                    <td className={`${td} font-mono`}>{number(shot.rawTimingError)}</td>
                    <td className={`${td} font-mono`}>
                      {shot.rawTechnique} / {shot.rawInputMask}
                    </td>
                    <td className={`${td} font-mono`}>{point(shot.rawPosition)}</td>
                    <td className={td}>
                      {shot.outcome === 'Contact' ? 'Có tiếp xúc bóng' : codes[shot.outcome] || shot.outcome}
                    </td>
                    <td className={td}>
                      {shot.recovered == null ? 'Chưa ghi nhận' : shot.recovered ? 'Đạt' : 'Chưa đạt'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </section>
  );
}

function ExpertReviewForm() {
  const client = useQueryClient();
  const [page, setPage] = useState(1);
  const [player, setPlayer] = useState('');
  const [selected, setSelected] = useState<string | null>(null);
  const [review, setReview] = useState<Review>(blank);
  const [shotId, setShotId] = useState('');
  const [code, setCode] = useState('Early');
  const [present, setPresent] = useState('');
  const [note, setNote] = useState('');
  const [message, setMessage] = useState('');

  const sessions = useQuery({
    queryKey: ['expert-sessions', player, page],
    queryFn: async () =>
      (await apiClient.get<Session[]>('/api/Admin/coaching/sessions', { params: { playerId: player || undefined, page } })).data,
  });
  const detail = useQuery({
    queryKey: ['expert-evidence', selected],
    queryFn: async () => (await apiClient.get<Evidence>(`/api/Admin/coaching/sessions/${selected}`)).data,
    enabled: selected !== null,
  });

  useEffect(() => {
    const latest = detail.data?.latestReview;
    setReview(latest ? { ...latest.review, expectedVersion: latest.version, confirmedHumanSession: false } : blank());
    setShotId(detail.data?.shots[0]?.shotId || '');
    setNote('');
    setPresent('');
  }, [detail.data]);

  const shot = detail.data?.shots.find((x) => x.shotId === shotId);
  const labelBlocked = !shot || (code === 'DinkTooHigh' && (shot.kind !== 'Dink' || shot.netClearance == null));

  const addLabel = () => {
    if (!shot || !present || note.trim().length < 3) return;
    setReview((previous) => ({
      ...previous,
      shotLabels: [
        ...previous.shotLabels.filter((x) => x.shotId !== shotId || x.code !== code),
        { shotId, code, present: present === 'yes', evidence: note.trim() },
      ],
    }));
    setNote('');
    setPresent('');
  };

  const save = useMutation({
    mutationFn: async () => apiClient.post(`/api/Admin/coaching/sessions/${selected}/review`, review),
    onSuccess: async () => {
      setMessage('Đã lưu phiên bản đánh giá của chuyên gia.');
      await client.invalidateQueries({ queryKey: ['expert-sessions'] });
      await client.invalidateQueries({ queryKey: ['expert-evidence', selected] });
    },
    onError: (error) => setMessage(errorText(error)),
  });

  const download = useMutation({
    mutationFn: async (kind: 'tier' | 'coaching') => {
      const response = await apiClient.get<Blob>(`/api/Admin/coaching/dataset/${kind}`, { responseType: 'blob' });
      const url = URL.createObjectURL(response.data);
      const anchor = document.createElement('a');
      anchor.href = url;
      const file = String(response.headers['content-disposition'] || '').match(/filename="?([^";]+)/)?.[1];
      anchor.download = file || `pickleverse-${kind}.json`;
      anchor.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      return response.headers['x-dataset-sha256'];
    },
    onSuccess: (digest) => setMessage(digest ? `Đã xuất dữ liệu. SHA-256 ${digest}` : 'Đã xuất dữ liệu.'),
    onError: (error) => setMessage(errorText(error)),
  });

  // MSG64 (SRS FR-33): tell the reviewer what is still missing instead of only disabling the save button.
  const reviewComplete =
    review.confirmedHumanSession &&
    review.expertName.trim().length >= 3 &&
    !!review.split &&
    review.observation.trim().length >= 3 &&
    (!!review.tier || review.shotLabels.length > 0);
  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (reviewComplete) save.mutate();
  };

  return (
    <div className="space-y-6">
      <div className="bg-card border border-border rounded-xl p-4 flex flex-col md:flex-row md:items-center justify-between gap-3">
        <p className="text-xs text-muted-foreground">
          Chọn phiên đã đồng bộ đầy đủ. Mọi phiên của một người phải dùng cùng nhóm dữ liệu.
        </p>
        <div className="flex flex-wrap gap-2">
          <button className={btnGhost} disabled={download.isPending} onClick={() => download.mutate('tier')}>
            Xuất dataset trình độ
          </button>
          <button className={btnGhost} disabled={download.isPending} onClick={() => download.mutate('coaching')}>
            Xuất dataset lỗi kỹ thuật
          </button>
        </div>
      </div>

      {message && (
        <div role="status" className={`${notice(message.startsWith('Đã') ? 'success' : 'error')} break-all`}>
          <span>{message}</span>
        </div>
      )}

      <div className="grid gap-5 lg:grid-cols-[300px_1fr]">
        {/* Session list */}
        <section className={`${card} space-y-3 self-start`}>
          <div className="flex items-center justify-between">
            <h2 className={sectionTitle}>Phiên luyện tập</h2>
          </div>
          <div className="flex flex-wrap gap-2">
            {selected && (
              <button
                className={btnGhost}
                onClick={() => {
                  setPlayer(detail.data?.session.playerId || '');
                  setPage(1);
                }}
              >
                Cùng người chơi
              </button>
            )}
            {player && (
              <button
                className={btnGhost}
                onClick={() => {
                  setPlayer('');
                  setPage(1);
                }}
              >
                Bỏ lọc người chơi
              </button>
            )}
          </div>

          {sessions.error && <p role="alert" className="text-xs text-rose-600">{errorText(sessions.error)}</p>}
          {sessions.isLoading && <p className="text-xs text-muted-foreground">Đang tải…</p>}
          {sessions.data?.length === 0 && (
            <p className="text-xs text-muted-foreground">Chưa có phiên đủ bằng chứng để gán nhãn.</p>
          )}

          <div className="max-h-[520px] space-y-2 overflow-y-auto pr-1">
            {sessions.data?.map((session) => (
              <button
                key={session.batchId}
                type="button"
                onClick={() => setSelected(session.batchId)}
                className={`block w-full rounded-lg border p-3 text-left transition-colors ${
                  selected === session.batchId ? 'border-primary bg-primary/5' : 'border-border hover:bg-muted/50'
                }`}
              >
                <span className="block text-xs font-semibold text-foreground">
                  {session.playerName} · {session.lessonTitle}
                </span>
                <span className="block text-[11px] text-muted-foreground mt-0.5">{dateTime(session.completedAt)}</span>
                <span
                  className={`${badgeBase} mt-2 ${session.reviewedVersion ? tone.emerald : tone.amber}`}
                >
                  {session.reviewedVersion ? `Đánh giá v${session.reviewedVersion}` : 'Chờ gán nhãn'}
                </span>
              </button>
            ))}
          </div>

          <div className="flex items-center justify-between pt-2 border-t border-border">
            <button className={btnGhost} disabled={page === 1} onClick={() => setPage(page - 1)}>
              Trước
            </button>
            <span className="text-xs text-muted-foreground">
              Trang <span className="font-semibold text-foreground">{page}</span>
            </span>
            <button className={btnGhost} disabled={sessions.data?.length !== 30} onClick={() => setPage(page + 1)}>
              Sau
            </button>
          </div>
        </section>

        {/* Review panel */}
        <section className={`${card} space-y-5`}>
          {detail.error && <p role="alert" className="text-xs text-rose-600">{errorText(detail.error)}</p>}
          {detail.isLoading && selected && <p className="text-xs text-muted-foreground">Đang tải bằng chứng…</p>}
          {!selected && (
            <div className="flex items-center justify-center py-16 text-sm text-muted-foreground">
              Chọn một phiên để xem cú đánh và gán nhãn.
            </div>
          )}

          {detail.data && (
            <form onSubmit={submit} className="space-y-5">
              <div>
                <h2 className={sectionTitle}>{detail.data.session.lessonTitle}</h2>
                <p className="text-xs text-muted-foreground mt-1">
                  {detail.data.session.playerName} · {detail.data.shots.length} cơ hội ghi nhận · Game {detail.data.gameVersion}
                </p>
                <p className="font-mono text-[11px] text-muted-foreground break-all mt-1">
                  Phiên {detail.data.session.sessionId} · Danh mục {detail.data.session.catalogVersion}
                </p>
                {detail.data.latestReview && (
                  <p className="text-xs text-muted-foreground mt-1">
                    Đánh giá v{detail.data.latestReview.version} · {detail.data.latestReview.review.expertName} ·{' '}
                    {dateTime(detail.data.latestReview.createdAt)}
                  </p>
                )}
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <label className={fieldLabel}>
                  Tên chuyên gia
                  <input
                    className={inputClass}
                    required
                    minLength={3}
                    maxLength={120}
                    value={review.expertName}
                    onChange={(e) => setReview({ ...review, expertName: e.target.value })}
                  />
                </label>
                <label className={fieldLabel}>
                  Nhóm dữ liệu
                  <select
                    className={inputClass}
                    required
                    value={review.split || ''}
                    onChange={(e) => setReview({ ...review, split: (e.target.value as Split) || null })}
                  >
                    <option value="">Chọn nhóm</option>
                    <option value="train">Huấn luyện</option>
                    <option value="calibration">Hiệu chỉnh</option>
                    <option value="test">Kiểm thử (holdout)</option>
                  </select>
                </label>
                <label className={fieldLabel}>
                  Trình độ đã quan sát
                  <select
                    className={inputClass}
                    value={review.tier || ''}
                    onChange={(e) => setReview({ ...review, tier: (e.target.value as Tier) || null })}
                  >
                    <option value="">Chưa đủ bằng chứng hoặc chỉ gán nhãn lỗi</option>
                    <option value="beginner">Mới chơi</option>
                    <option value="intermediate">Trung cấp</option>
                    <option value="advanced">Nâng cao</option>
                  </select>
                </label>
                <label className={fieldLabel}>
                  Quan sát và căn cứ
                  <textarea
                    className={inputClass}
                    rows={2}
                    required
                    minLength={3}
                    maxLength={2000}
                    value={review.observation}
                    onChange={(e) => setReview({ ...review, observation: e.target.value })}
                  />
                </label>
              </div>

              {/* Shot evidence */}
              <div className="space-y-3">
                <h3 className="text-sm font-semibold text-foreground">Bằng chứng theo cú đánh</h3>
                <select
                  className={inputClass}
                  value={shotId}
                  onChange={(e) => {
                    setShotId(e.target.value);
                    setNote('');
                    setPresent('');
                  }}
                >
                  {detail.data.shots.map((x) => (
                    <option value={x.shotId} key={x.shotId}>
                      #{x.sequence + 1} · {x.kind} · {x.outcome}
                    </option>
                  ))}
                </select>

                {shot && (
                  <div className="rounded-lg border border-border bg-background p-4 space-y-4">
                    <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                      <div>
                        <p className={kvLabel}>Bấm / thả (giây)</p>
                        <p className={`${kvValue} font-mono`}>
                          {number(shot.rawPressedAt)} / {number(shot.rawReleasedAt)}
                        </p>
                      </div>
                      <div>
                        <p className={kvLabel}>Sai lệch thả nút (giây)</p>
                        <p className={`${kvValue} font-mono`}>{number(shot.rawTimingError)}</p>
                      </div>
                      <div>
                        <p className={kvLabel}>Kỹ thuật / mã nút</p>
                        <p className={`${kvValue} font-mono`}>
                          {shot.rawTechnique || 'Chưa ghi nhận'} · {shot.rawInputMask ?? 'Chưa ghi nhận'}
                        </p>
                      </div>
                      <div>
                        <p className={kvLabel}>Hướng / lực</p>
                        <p className={kvValue}>
                          {shot.rawCrossCourt == null ? 'Chưa ghi nhận' : shot.rawCrossCourt ? 'Chéo' : 'Thẳng'} ·{' '}
                          {shot.rawPower == null ? 'Chưa ghi nhận' : shot.rawPower ? 'Có' : 'Không'}
                        </p>
                      </div>
                      <div>
                        <p className={kvLabel}>Vị trí trước hỗ trợ (m)</p>
                        <p className={`${kvValue} font-mono`}>{point(shot.rawPosition)}</p>
                      </div>
                      <div>
                        <p className={kvLabel}>Điểm chạm (m)</p>
                        <p className={`${kvValue} font-mono`}>{point(shot.contact)}</p>
                      </div>
                      <div>
                        <p className={kvLabel}>Chân trái / phải (m)</p>
                        <p className={`${kvValue} font-mono`}>
                          {point(shot.leftFoot)} / {point(shot.rightFoot)}
                        </p>
                      </div>
                      <div>
                        <p className={kvLabel}>Độ hở qua lưới (m)</p>
                        <p className={`${kvValue} font-mono`}>{number(shot.netClearance)}</p>
                      </div>
                      <div>
                        <p className={kvLabel}>Điểm rơi (m)</p>
                        <p className={`${kvValue} font-mono`}>{point(shot.landing)}</p>
                      </div>
                    </div>

                    <div className="flex flex-wrap gap-2">
                      <span className={`${badgeBase} ${shot.assisted ? tone.blue : tone.neutral}`}>
                        {shot.assisted ? 'Có hỗ trợ tiếp xúc' : 'Tiếp xúc không hỗ trợ'}
                      </span>
                      <span className={`${badgeBase} ${shot.engineAimed ? tone.indigo : tone.neutral}`}>
                        {shot.engineAimed ? 'Game chọn vùng đích' : 'Người chơi chọn vùng đích'}
                      </span>
                    </div>
                    <p className="font-mono text-[11px] text-muted-foreground break-all">
                      Tham chiếu {shot.referenceId || 'Chưa ghi nhận'}
                    </p>

                    <div className="grid gap-3 md:grid-cols-2">
                      <label className={fieldLabel}>
                        Loại lỗi
                        <select className={inputClass} value={code} onChange={(e) => setCode(e.target.value)}>
                          {Object.entries(codes).map(([key, label]) => (
                            <option
                              key={key}
                              value={key}
                              disabled={key === 'DinkTooHigh' && (shot.kind !== 'Dink' || shot.netClearance == null)}
                            >
                              {label}
                            </option>
                          ))}
                        </select>
                      </label>
                      <label className={fieldLabel}>
                        Quan sát
                        <select className={inputClass} value={present} onChange={(e) => setPresent(e.target.value)}>
                          <option value="">Chưa xác nhận</option>
                          <option value="yes">Có lỗi</option>
                          <option value="no">Không có lỗi</option>
                        </select>
                      </label>
                    </div>
                    <label className={fieldLabel}>
                      Căn cứ nhãn
                      <input className={inputClass} maxLength={1000} value={note} onChange={(e) => setNote(e.target.value)} />
                    </label>
                    <div className="flex justify-end">
                      <button
                        type="button"
                        className={btnPrimary}
                        onClick={addLabel}
                        disabled={!present || note.trim().length < 3 || labelBlocked}
                      >
                        Thêm nhãn vào bản đánh giá
                      </button>
                    </div>
                  </div>
                )}

                {review.shotLabels.length > 0 && (
                  <div className="space-y-2">
                    {review.shotLabels.map((label) => (
                      <div
                        key={`${label.shotId}-${label.code}`}
                        className="flex items-start justify-between gap-3 rounded-lg border border-border bg-background p-3"
                      >
                        <div className="text-xs text-foreground">
                          <span className="font-semibold">{codes[label.code]}</span>
                          <span
                            className={`${badgeBase} ml-2 ${label.present ? tone.rose : tone.emerald}`}
                          >
                            {label.present ? 'Có lỗi' : 'Không có lỗi'}
                          </span>
                          <p className="mt-1 text-muted-foreground">{label.evidence}</p>
                        </div>
                        <button
                          type="button"
                          className={btnGhost}
                          onClick={() =>
                            setReview({ ...review, shotLabels: review.shotLabels.filter((x) => x !== label) })
                          }
                        >
                          Bỏ nhãn
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              <div className="border-t border-border pt-4 space-y-4">
                <label className="flex items-start gap-2 text-xs text-foreground">
                  <input
                    type="checkbox"
                    className="mt-0.5"
                    checked={review.confirmedHumanSession}
                    onChange={(e) => setReview({ ...review, confirmedHumanSession: e.target.checked })}
                  />
                  Tôi xác nhận phiên do người thật thực hiện và đã xem căn cứ cho các nhãn này.
                </label>
                <div className="flex flex-wrap items-center justify-between gap-3">
                  {!reviewComplete ? (
                    <p className="text-xs text-rose-600" role="alert">
                      {MSG64}
                    </p>
                  ) : (
                    <span />
                  )}
                  <button className={btnPrimary} disabled={save.isPending || !reviewComplete}>
                    Lưu đánh giá chuyên gia
                  </button>
                </div>
              </div>
            </form>
          )}
        </section>
      </div>
    </div>
  );
}
