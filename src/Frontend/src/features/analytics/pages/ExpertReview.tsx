import { FormEvent, useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { apiClient } from '../../../api/client';

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
const input = 'block w-full border border-border bg-background p-2 rounded';
const button = 'bg-primary rounded px-4 py-2 text-sm disabled:opacity-40';
const blank = (): Review => ({
  expectedVersion: 0, tier: null, split: null, expertName: '', observation: '',
  confirmedHumanSession: false, shotLabels: [],
});
const number = (value?: number) => value == null ? 'Chưa ghi nhận' : value.toFixed(3);
const point = (value?: number[]) => value?.map(x => x.toFixed(2)).join(', ') || 'Chưa ghi nhận';
const errorText = (error: unknown) => (error as { response?: { data?: { message?: string } } })?.response?.data?.message
  || (error as Error)?.message || 'Không thực hiện được yêu cầu.';
const tierNames: Record<Tier, string> = { beginner: 'Mới chơi', intermediate: 'Trung cấp', advanced: 'Nâng cao' };
const splitNames: Record<Split, string> = { train: 'Huấn luyện', calibration: 'Hiệu chỉnh', test: 'Kiểm định' };

export function ExpertReview() {
  const [params, setParams] = useSearchParams();
  const demo = params.get('source') === 'demo';
  return <div className="space-y-6">
    <h1 className="text-2xl font-bold">Gán nhãn phiên luyện tập</h1>
    <div className="flex gap-3 flex-wrap" aria-label="Nguồn phiên luyện tập">
      <button type="button" className={demo ? 'border rounded px-4 py-2 text-sm' : button}
        aria-pressed={!demo} onClick={() => setParams({})}>Phiên người chơi</button>
      <button type="button" className={demo ? button : 'border rounded px-4 py-2 text-sm'}
        aria-pressed={demo} onClick={() => setParams({ source: 'demo' })}>Phiên mô phỏng</button>
    </div>
    {demo ? <SimulatedSessions /> : <ExpertReviewForm />}
  </div>;
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
    queryFn: async () => (await apiClient.get<DemoEvidence>(`/api/Admin/coaching/demo-sessions/${encodeURIComponent(selected)}`)).data,
    enabled: selected !== '',
  });
  return <section className="bg-card border rounded p-5 space-y-4">
    <h2 className="text-xl font-semibold">Phiên mô phỏng</h2>
    <p>Dữ liệu minh họa, chỉ xem. Các phiên này được lưu riêng với phiên người thật.</p>
    {sessions.error && <p role="alert">{errorText(sessions.error)}</p>}
    {sessions.isLoading && <p>Đang tải danh sách…</p>}
    {sessions.data?.length === 0 && <p>Database này chưa nạp phiên mô phỏng.</p>}
    {Boolean(sessions.data?.length) && <label>{sessions.data?.length} phiên · Chọn phiên
      <select className={input} value={selected} onChange={event => setChoice(event.target.value)}>
        {sessions.data?.map(session => <option key={session.sessionId} value={session.sessionId}>
          {session.playerCode} · {tierNames[session.tier]} · {splitNames[session.split]}
        </option>)}
      </select>
    </label>}
    {detail.error && <p role="alert">{errorText(detail.error)}</p>}
    {selected && detail.isLoading && <p>Đang tải cú đánh…</p>}
    {detail.data && <>
      <h3 className="font-semibold">Volley · {detail.data.shots.length} lượt · {tierNames[detail.data.session.tier]}</h3>
      <p className="text-sm">{splitNames[detail.data.session.split]} · {detail.data.gameVersion} · Có hỗ trợ tiếp xúc và tự ngắm</p>
      <div className="overflow-x-auto">
        <table className="w-full whitespace-nowrap text-left text-sm">
          <thead><tr className="border-b">{['Lượt', 'Bấm / thả (s)', 'Sai lệch (s)', 'Kỹ thuật / mã nút', 'Vị trí trước hỗ trợ (m)', 'Kết quả', 'Hồi vị']
            .map(header => <th key={header} scope="col" className="p-2">{header}</th>)}</tr></thead>
          <tbody>{detail.data.shots.map(shot => <tr className="border-b" key={shot.shotId}>
            <td className="p-2">{shot.sequence + 1}</td>
            <td className="p-2">{number(shot.rawPressedAt)} / {number(shot.rawReleasedAt)}</td>
            <td className="p-2">{number(shot.rawTimingError)}</td>
            <td className="p-2">{shot.rawTechnique} / {shot.rawInputMask}</td>
            <td className="p-2">{point(shot.rawPosition)}</td>
            <td className="p-2">{shot.outcome === 'Contact' ? 'Có tiếp xúc bóng' : codes[shot.outcome] || shot.outcome}</td>
            <td className="p-2">{shot.recovered == null ? 'Chưa ghi nhận' : shot.recovered ? 'Đạt' : 'Chưa đạt'}</td>
          </tr>)}</tbody>
        </table>
      </div>
    </>}
  </section>;
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
  const [notice, setNotice] = useState('');

  const sessions = useQuery({
    queryKey: ['expert-sessions', player, page],
    queryFn: async () => (await apiClient.get<Session[]>('/api/Admin/coaching/sessions', { params: { playerId: player || undefined, page } })).data,
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

  const shot = detail.data?.shots.find(x => x.shotId === shotId);
  const addLabel = () => {
    if (!shot || !present || note.trim().length < 3) return;
    setReview(previous => ({
      ...previous,
      shotLabels: [
        ...previous.shotLabels.filter(x => x.shotId !== shotId || x.code !== code),
        { shotId, code, present: present === 'yes', evidence: note.trim() },
      ],
    }));
    setNote('');
    setPresent('');
  };

  const save = useMutation({
    mutationFn: async () => apiClient.post(`/api/Admin/coaching/sessions/${selected}/review`, review),
    onSuccess: async () => {
      setNotice('Đã lưu phiên bản đánh giá của chuyên gia.');
      await client.invalidateQueries({ queryKey: ['expert-sessions'] });
      await client.invalidateQueries({ queryKey: ['expert-evidence', selected] });
    },
    onError: error => setNotice(errorText(error)),
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
    onSuccess: digest => setNotice(digest ? `Đã xuất dữ liệu. SHA-256 ${digest}` : 'Đã xuất dữ liệu.'),
    onError: error => setNotice(errorText(error)),
  });
  const submit = (event: FormEvent) => { event.preventDefault(); save.mutate(); };

  return <div className="space-y-6">
    <p>Chọn phiên đã đồng bộ đầy đủ. Dùng telemetry cùng quan sát của chuyên gia để xác nhận trình độ và từng lỗi. Mọi phiên của một người phải dùng cùng nhóm dữ liệu.</p>
    <div className="flex gap-3 flex-wrap">
      <button className={button} disabled={download.isPending} onClick={() => download.mutate('tier')}>Xuất dataset trình độ</button>
      <button className={button} disabled={download.isPending} onClick={() => download.mutate('coaching')}>Xuất dataset lỗi kỹ thuật</button>
    </div>
    {notice && <p role="status" className="break-all rounded border p-3">{notice}</p>}
    <div className="grid lg:grid-cols-[300px_1fr] gap-5">
      <section className="bg-card border rounded p-4 space-y-3">
        <h2 className="font-semibold">Phiên luyện tập</h2>
        {selected && <button className={button} onClick={() => { setPlayer(detail.data?.session.playerId || ''); setPage(1); }}>Xem các phiên cùng người chơi</button>}
        {player && <button className={button} onClick={() => { setPlayer(''); setPage(1); }}>Bỏ lọc người chơi</button>}
        {sessions.error && <p role="alert">{errorText(sessions.error)}</p>}
        {sessions.isLoading && <p>Đang tải…</p>}
        {sessions.data?.length === 0 && <p>Chưa có phiên đủ bằng chứng để gán nhãn.</p>}
        {sessions.data?.map(session => <button key={session.batchId} type="button"
          className={`block w-full p-3 text-left rounded border ${selected === session.batchId ? 'border-primary' : 'border-border'}`}
          onClick={() => setSelected(session.batchId)}>
          <strong>{session.playerName} · {session.lessonTitle}</strong><br />
          <span>{new Date(session.completedAt).toLocaleString('vi-VN')}</span><br />
          <span>{session.reviewedVersion ? `Đánh giá v${session.reviewedVersion}` : 'Chờ gán nhãn'}</span>
        </button>)}
        <div className="flex gap-2 items-center"><button className={button} disabled={page === 1} onClick={() => setPage(page - 1)}>Trước</button>
          <span>{page}</span><button className={button} disabled={sessions.data?.length !== 30} onClick={() => setPage(page + 1)}>Sau</button></div>
      </section>
      <section className="bg-card border rounded p-5 space-y-4">
        {detail.error && <p role="alert">{errorText(detail.error)}</p>}
        {detail.isLoading && selected && <p>Đang tải bằng chứng…</p>}
        {!selected && <p>Chọn một phiên để xem cú đánh và gán nhãn.</p>}
        {detail.data && <form onSubmit={submit} className="space-y-4">
          <h2 className="text-xl font-semibold">{detail.data.session.lessonTitle}</h2>
          <p className="text-sm">{detail.data.session.playerName} · {detail.data.shots.length} cơ hội ghi nhận · Game {detail.data.gameVersion}</p>
          <p className="font-mono text-xs break-all">Phiên {detail.data.session.sessionId} · Danh mục {detail.data.session.catalogVersion}</p>
          {detail.data.latestReview && <p>Đánh giá v{detail.data.latestReview.version} · {detail.data.latestReview.review.expertName} · {new Date(detail.data.latestReview.createdAt).toLocaleString('vi-VN')}</p>}
          <div className="grid md:grid-cols-2 gap-3">
            <label>Tên chuyên gia<input className={input} required minLength={3} maxLength={120} value={review.expertName}
              onChange={e => setReview({ ...review, expertName: e.target.value })} /></label>
            <label>Nhóm dữ liệu<select className={input} required value={review.split || ''} onChange={e => setReview({ ...review, split: e.target.value as Split || null })}>
              <option value="">Chọn nhóm</option><option value="train">Huấn luyện</option><option value="calibration">Hiệu chỉnh</option><option value="test">Holdout</option>
            </select></label>
          </div>
          <label>Trình độ đã quan sát<select className={input} value={review.tier || ''} onChange={e => setReview({ ...review, tier: e.target.value as Tier || null })}>
            <option value="">Chưa đủ bằng chứng hoặc chỉ gán nhãn lỗi</option><option value="beginner">Mới chơi</option>
            <option value="intermediate">Trung cấp</option><option value="advanced">Nâng cao</option>
          </select></label>
          <label>Quan sát và căn cứ<textarea className={input} required minLength={3} maxLength={2000} value={review.observation}
            onChange={e => setReview({ ...review, observation: e.target.value })} /></label>
          <h3 className="font-semibold">Bằng chứng theo cú đánh</h3>
          <select className={input} value={shotId} onChange={e => { setShotId(e.target.value); setNote(''); setPresent(''); }}>
            {detail.data.shots.map(x => <option value={x.shotId} key={x.shotId}>#{x.sequence + 1} · {x.kind} · {x.outcome}</option>)}
          </select>
          {shot && <div className="border rounded p-4 space-y-3 text-sm">
            <div className="grid md:grid-cols-2 gap-2">
              <p>Bấm / thả (giây) {number(shot.rawPressedAt)} / {number(shot.rawReleasedAt)}</p>
              <p>Sai lệch thả nút trước hỗ trợ (giây) {number(shot.rawTimingError)}</p>
              <p>Kỹ thuật đã chọn {shot.rawTechnique || 'Chưa ghi nhận'} · Mã nút {shot.rawInputMask ?? 'Chưa ghi nhận'}</p>
              <p>Hướng {shot.rawCrossCourt == null ? 'Chưa ghi nhận' : shot.rawCrossCourt ? 'Chéo' : 'Thẳng'} · Lực {shot.rawPower == null ? 'Chưa ghi nhận' : shot.rawPower ? 'Có' : 'Không'}</p>
              <p>Vị trí trước hỗ trợ (m) {point(shot.rawPosition)}</p><p>Điểm chạm (m) {point(shot.contact)}</p>
              <p>Chân trái / phải (m) {point(shot.leftFoot)} / {point(shot.rightFoot)}</p>
              <p>Độ hở qua lưới (m) {number(shot.netClearance)} · Điểm rơi {point(shot.landing)}</p>
            </div>
            <p>{shot.assisted ? 'Có hỗ trợ tiếp xúc' : 'Tiếp xúc không hỗ trợ'} · {shot.engineAimed ? 'Game chọn vùng đích' : 'Người chơi chọn vùng đích'}</p>
            <p className="font-mono text-xs break-all">Tham chiếu {shot.referenceId || 'Chưa ghi nhận'}</p>
            <div className="grid md:grid-cols-2 gap-3">
              <label>Loại lỗi<select className={input} value={code} onChange={e => setCode(e.target.value)}>
                {Object.entries(codes).map(([key, label]) => <option key={key} value={key}
                  disabled={key === 'DinkTooHigh' && (shot.kind !== 'Dink' || shot.netClearance == null)}>{label}</option>)}
              </select></label>
              <label>Quan sát<select className={input} value={present} onChange={e => setPresent(e.target.value)}>
                <option value="">Chưa xác nhận</option><option value="yes">Có lỗi</option><option value="no">Không có lỗi</option>
              </select></label>
            </div>
            <label>Căn cứ nhãn<input className={input} maxLength={1000} value={note} onChange={e => setNote(e.target.value)} /></label>
            <button type="button" className={button} onClick={addLabel}
              disabled={!present || note.trim().length < 3 || code === 'DinkTooHigh' && (shot.kind !== 'Dink' || shot.netClearance == null)}>Thêm nhãn vào bản đánh giá</button>
          </div>}
          {review.shotLabels.map(label => <div key={`${label.shotId}-${label.code}`} className="flex justify-between gap-3 border rounded p-3 text-sm">
            <p>{codes[label.code]} · {label.present ? 'Có lỗi' : 'Không có lỗi'} · {label.evidence}</p>
            <button type="button" onClick={() => setReview({ ...review, shotLabels: review.shotLabels.filter(x => x !== label) })}>Bỏ nhãn</button>
          </div>)}
          <label className="flex gap-2"><input type="checkbox" checked={review.confirmedHumanSession}
            onChange={e => setReview({ ...review, confirmedHumanSession: e.target.checked })} />Tôi xác nhận phiên do người thật thực hiện và đã xem căn cứ cho các nhãn này.</label>
          <button className={button} disabled={save.isPending || !review.confirmedHumanSession || !review.tier && !review.shotLabels.length}>Lưu đánh giá chuyên gia</button>
        </form>}
      </section>
    </div>
  </div>;
}
