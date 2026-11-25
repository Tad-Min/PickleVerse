import { FormEvent, useEffect, useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import axios from 'axios';
import { AlertCircle, Plus } from 'lucide-react';
import { adminApi } from '../../../api/adminApi';
import { SaveShopItemRequest, ShopItem, ShopItemState } from '../../../types/api';
import { MAX_COLLECTION, MAX_DESCRIPTION, MAX_PRICE, MAX_TITLE, draftOf, priceLabel, problemOf, requestOf, stateNames } from './shopItems';
import {
  badgeBase, btnGhost, btnPrimary, btnSmall, card, fieldLabel, inputClass, notice, rowHover, sectionTitle, tableCard, tableHead, tableText, td, th, thRight, tone,
} from '../../../shared/ui/adminStyles';

const stateTone: Record<ShopItemState, string> = {
  Default: tone.neutral, OnSale: tone.emerald, Stopped: tone.amber, Incomplete: tone.rose,
};
const errorOf = (error: unknown) => axios.isAxiosError(error) ? error.response?.data?.message || error.message : error instanceof Error ? error.message : String(error);

/** Vật phẩm của cửa hàng: đăng bán vợt đã có model trong game, sửa nội dung và giá, ngừng bán. */
export function ShopItemManager() {
  const client = useQueryClient();
  const query = useQuery({ queryKey: ['cms-shop-items'], queryFn: adminApi.getShopItems, refetchOnWindowFocus: false });
  const [editing, setEditing] = useState<ShopItem | null>(null);
  const [form, setForm] = useState<SaveShopItemRequest>(draftOf());
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const message = useRef<HTMLParagraphElement>(null);
  const formBox = useRef<HTMLFormElement>(null);
  useEffect(() => { if (feedback) message.current?.scrollIntoView({ block: 'center' }); }, [feedback]);

  const items = query.data?.items ?? [];
  const skins = query.data?.availableSkins ?? [];
  const creating = editing === null;
  const set = (change: Partial<SaveShopItemRequest>) => setForm(f => ({ ...f, ...change }));
  const reset = () => { setEditing(null); setForm(draftOf()); };
  const refresh = () => client.invalidateQueries({ queryKey: ['cms-shop-items'] });
  const fail = (error: unknown) => setFeedback({ type: 'error', text: errorOf(error) });

  const save = useMutation({
    mutationFn: (input: { id: number | null; body: SaveShopItemRequest }) =>
      input.id === null ? adminApi.createShopItem(input.body) : adminApi.updateShopItem(input.id, input.body),
    onSuccess: async (item, input) => {
      setFeedback({ type: 'success', text: input.id === null
        ? `Đã đăng bán ${item.title}: ${priceLabel(item)}. Người chơi thấy vật phẩm khi mở lại game.`
        : item.state === 'Stopped' ? `Đã ngừng bán ${item.title}. Người đã sở hữu vẫn dùng được.` : `Đã lưu ${item.title}: ${priceLabel(item)}.` });
      reset();
      await refresh();
    },
    onError: fail,
  });
  const remove = useMutation({
    mutationFn: (item: ShopItem) => adminApi.deleteShopItem(item.cosmeticId),
    onSuccess: async (_, item) => { setFeedback({ type: 'success', text: `Đã xóa ${item.code}.` }); if (editing?.cosmeticId === item.cosmeticId) reset(); await refresh(); },
    onError: fail,
  });
  const busy = save.isPending || remove.isPending;

  const edit = (item: ShopItem) => { setEditing(item); setForm(requestOf(item)); setFeedback(null); formBox.current?.scrollIntoView({ block: 'nearest' }); };
  const stop = (item: ShopItem) => {
    if (window.confirm(`Ngừng bán "${item.title}"? Người đã sở hữu vẫn dùng được. Muốn bán lại, bấm Sửa rồi nhập giá.`))
      save.mutate({ id: item.cosmeticId, body: { ...requestOf(item), tokenpadPrice: 0, elitePrice: 0 } });
  };
  const submit = (event: FormEvent) => {
    event.preventDefault();
    const problem = problemOf(form, creating);
    if (problem) { setFeedback({ type: 'error', text: problem }); return; }
    setFeedback(null);
    save.mutate({ id: editing?.cosmeticId ?? null, body: form });
  };
  const price = (key: 'tokenpadPrice' | 'elitePrice', label: string) => (
    <label className={fieldLabel}>
      {label}
      <input type="number" min={0} max={MAX_PRICE} step={1} placeholder="0" className={inputClass} value={form[key] || ''}
        onChange={e => set({ [key]: Math.trunc(Number(e.target.value)) || 0 })} />
    </label>
  );

  return (
    <section className="grid items-start gap-5 xl:grid-cols-[1fr_400px]">
      <div className="space-y-5">
        {feedback && <p ref={message} role="status" className={notice(feedback.type)}>{feedback.text}</p>}
        <div className={tableCard}>
          <div className="flex items-center justify-between gap-3 p-5 pb-3">
            <h2 className={sectionTitle}>Vật phẩm cửa hàng ({items.length})</h2>
            {!creating && (
              <button type="button" className={`${btnGhost} inline-flex items-center gap-1`} onClick={reset}>
                <Plus className="h-3.5 w-3.5" /> Đăng bán vợt khác
              </button>
            )}
          </div>
          {query.isLoading && <p className="px-5 pb-5 text-xs text-muted-foreground">Đang tải…</p>}
          {query.isError && (
            <p className="px-5 pb-5 text-xs text-rose-600">
              Không tải được vật phẩm: {errorOf(query.error)} <button className="underline" onClick={() => query.refetch()}>Thử lại</button>
            </p>
          )}
          {query.data && (
            <div className="overflow-x-auto">
              <table className={tableText}>
                <thead className={tableHead}>
                  <tr>
                    <th className={th}>Vật phẩm</th><th className={th}>Giá bán</th><th className={th}>Trạng thái</th>
                    <th className={`${thRight} whitespace-nowrap`}>Sở hữu</th><th className={thRight}>Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {items.map(item => (
                    <tr key={item.cosmeticId} className={`${rowHover} ${editing?.cosmeticId === item.cosmeticId ? 'bg-primary-soft/60' : ''}`}>
                      <td className={td}>
                        <span className="block font-semibold text-foreground">{item.title}</span>
                        <span className="block font-mono text-[11px] text-muted-foreground">{item.code}</span>
                      </td>
                      <td className={td}>{priceLabel(item)}</td>
                      <td className={td}>
                        <span className={`${badgeBase} ${stateTone[item.state]}`}>{stateNames[item.state]}</span>
                        {item.issue && <span className="mt-1 block max-w-[260px] text-[11px] text-rose-600">{item.issue}</span>}
                      </td>
                      <td className={`${td} text-right tabular-nums`}>{item.owners}</td>
                      <td className={`${td} text-right`}>
                        <div className="flex justify-end gap-1.5 whitespace-nowrap">
                          {item.canEdit && <button type="button" className={btnSmall} disabled={busy} onClick={() => edit(item)}>Sửa</button>}
                          {item.state === 'OnSale' && item.canEdit && <button type="button" className={btnSmall} disabled={busy} onClick={() => stop(item)}>Ngừng bán</button>}
                          {item.canDelete && (
                            <button type="button" className={`${btnSmall} text-rose-600`} disabled={busy}
                              onClick={() => { if (window.confirm(`Xóa vật phẩm "${item.code}"? Không khôi phục được.`)) remove.mutate(item); }}>Xóa</button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                  {items.length === 0 && <tr><td className={td} colSpan={5}>Chưa có vật phẩm nào.</td></tr>}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      <form ref={formBox} onSubmit={submit} className={`${card} space-y-4 xl:sticky xl:top-6`}>
        <div>
          <h2 className={sectionTitle}>{creating ? 'Đăng bán vợt mới' : `Sửa ${editing.title}`}</h2>
          <p className="mt-1 text-xs text-muted-foreground">
            {creating ? 'Chỉ đăng bán được vợt đã có model trong game.' : 'Để cả hai giá bằng 0 nếu muốn ngừng bán.'}
          </p>
        </div>
        {creating && query.data && skins.length === 0 ? (
          <p className="rounded-xl border border-border bg-background p-4 text-xs text-muted-foreground">
            Mọi vợt có trong game đều đã ở cửa hàng. Bấm Sửa ở danh sách để đổi nội dung, giá hoặc bán lại.
          </p>
        ) : (
          <>
            <label className={fieldLabel}>
              Vợt *
              {creating ? (
                <select className={inputClass} value={form.code} required
                  onChange={e => setForm(draftOf(skins.find(s => s.code === e.target.value)))}>
                  <option value="">Chọn vợt có model trong game</option>
                  {skins.map(s => <option key={s.code} value={s.code}>{s.title} ({s.code})</option>)}
                </select>
              ) : <input className={`${inputClass} font-mono`} value={form.code} disabled readOnly />}
            </label>
            <label className={fieldLabel}>
              Tên vật phẩm *
              <input className={inputClass} value={form.title} maxLength={MAX_TITLE} required onChange={e => set({ title: e.target.value })} />
            </label>
            <label className={fieldLabel}>
              Bộ sưu tập *
              <input className={inputClass} value={form.collection} maxLength={MAX_COLLECTION} required onChange={e => set({ collection: e.target.value })} />
            </label>
            <label className={fieldLabel}>
              Mô tả * <span className="font-normal text-muted-foreground">({form.description.length}/{MAX_DESCRIPTION})</span>
              <textarea className={`${inputClass} min-h-[88px]`} value={form.description} maxLength={MAX_DESCRIPTION} required onChange={e => set({ description: e.target.value })} />
            </label>
            <div className="grid gap-4 sm:grid-cols-2">
              {price('tokenpadPrice', 'Giá Tokenpad')}
              {price('elitePrice', 'Giá Elite Token')}
            </div>
            <p className="text-[11px] text-muted-foreground">
              Để trống hoặc 0 nghĩa là không bán bằng loại tiền đó. Người chơi sẽ thấy: <b className="text-foreground">{priceLabel(form)}</b>
            </p>
            <label className="flex items-center gap-2 text-xs text-foreground">
              <input type="checkbox" checked={form.premium} onChange={e => set({ premium: e.target.checked })} /> Viền vàng (vật phẩm cao cấp)
            </label>
            {creating && (
              <p className={`flex gap-2 rounded-xl border p-3 text-[11px] ${tone.amber}`}>
                <AlertCircle className="h-4 w-4 shrink-0" />
                Chỉ đăng bán sau khi mọi người đã có bản game chứa vợt này. Bản build trước 25/11/2026 không tải được hồ sơ khi gặp vợt lạ.
              </p>
            )}
            <div className="flex justify-end gap-2">
              {!creating && <button type="button" className={btnGhost} onClick={reset} disabled={busy}>Hủy</button>}
              <button type="submit" className={btnPrimary} disabled={busy || !form.code}>
                {save.isPending ? 'Đang lưu…' : creating ? 'Đăng bán vật phẩm' : 'Lưu thay đổi'}
              </button>
            </div>
          </>
        )}
      </form>
    </section>
  );
}
