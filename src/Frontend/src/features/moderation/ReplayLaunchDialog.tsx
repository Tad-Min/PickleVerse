import { useEffect, useState } from 'react';
import { ExternalLink, RefreshCw, X } from 'lucide-react';
import { createReplayLaunch, ReplayLaunchLink } from '../../api/replayLaunchApi';

export function ReplayLaunchDialog({ matchId, onClose }: { matchId: string; onClose: () => void }) {
  const [link, setLink] = useState<ReplayLaunchLink | null>(null);
  const [error, setError] = useState('');
  const [request, setRequest] = useState(0);
  const [busy, setBusy] = useState(true);
  useEffect(() => {
    const controller = new AbortController();
    setBusy(true); setLink(null); setError('');
    createReplayLaunch(matchId, controller.signal).then((value) => {
      if (controller.signal.aborted) return;
      setLink(value);
      // The browser/OS owns its Open Application confirmation. The visible link
      // below also works when an async navigation needs a fresh user gesture.
      window.location.assign(value.url);
    }).catch((e) => {
      if (!controller.signal.aborted) setError(e?.response?.data?.message || e?.message || 'Không tạo được vé xem replay.');
    }).finally(() => { if (!controller.signal.aborted) setBusy(false); });
    return () => controller.abort();
  }, [matchId, request]);
  return (
    <div className="fixed inset-0 z-[80] flex items-center justify-center bg-black/55 p-4" role="dialog" aria-modal="true" aria-labelledby="replay-launch-title">
      <div className="w-full max-w-lg rounded-xl border border-border bg-card p-6 shadow-xl">
        <div className="flex items-center justify-between gap-4">
          <h2 id="replay-launch-title" className="text-lg font-semibold text-foreground">Xem replay trong PickleVerse</h2>
          <button aria-label="Đóng" className="rounded p-1 text-muted-foreground hover:bg-muted" onClick={onClose}><X size={20} /></button>
        </div>
        <p className="mt-3 text-xs text-muted-foreground break-all">Trận: {matchId}</p>
        {busy && <p className="mt-4 text-sm text-foreground" role="status">Đang tạo vé xem replay…</p>}
        {error && <p className="mt-4 text-sm text-rose-600" role="alert">{error}</p>}
        {link && <>
          <p className="mt-4 text-sm text-foreground">Chọn <strong>Mở PickleVerse</strong> trong hộp thoại của trình duyệt để xem trận này.</p>
          <p className="mt-3 text-xs leading-relaxed text-muted-foreground">Cần bản PickleVerse Windows mới nhất. Chạy game ít nhất một lần để đăng ký liên kết mở ứng dụng. Vé có hiệu lực 2 phút và dùng một lần; tài khoản trong game được giữ nguyên.</p>
          <a href={link.url} className="mt-5 inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white hover:bg-primary-hover"><ExternalLink size={16} />Mở PickleVerse</a>
        </>}
        <div className="mt-5 flex justify-end gap-2">
          <button disabled={busy} className="inline-flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-xs text-foreground hover:bg-muted disabled:opacity-40" onClick={() => setRequest((value) => value + 1)}><RefreshCw size={14} />{error ? 'Thử lại' : 'Tạo vé mới'}</button>
          <button className="rounded-lg border border-border px-3 py-2 text-xs text-foreground hover:bg-muted" onClick={onClose}>Đóng</button>
        </div>
      </div>
    </div>
  );
}
