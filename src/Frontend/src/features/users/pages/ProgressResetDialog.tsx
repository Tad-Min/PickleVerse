import { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import axios from 'axios';
import { apiClient } from '../../../api/client';
import { AdminUserDto } from '../../../types/api';
import { ProgressResetGroup } from '../../../types/status';

interface Preview {
  revision: number; busy: boolean; effects: string[]; level: number; elo: number; tokenpad: number; elite: number;
  lessonRecords: number; masteryRecords: number; missionRecords: number; achievementRecords: number; ownedCosmetics: string[];
}
const choices: [ProgressResetGroup,string][] = [[ProgressResetGroup.Learning,'Học tập: XP/cấp độ, bài học, mastery và coaching'],
  [ProgressResetGroup.Ranking,'Xếp hạng: Elo về 2000 và xóa chuỗi xếp hạng'],
  [ProgressResetGroup.Missions,'Bộ đếm nhiệm vụ/thành tựu và thống kê tiến trình'],
  [ProgressResetGroup.Economy,'Kinh tế: tiền và vật phẩm về tài khoản mới']];
export function ProgressResetDialog({ user,onClose,onDone }: { user: AdminUserDto; onClose:()=>void; onDone:(message:string)=>void }) {
  const [groups,setGroups]=useState<ProgressResetGroup[]>([ProgressResetGroup.Learning]);
  const [preview,setPreview]=useState<Preview|null>(null);
  const [requestId,setRequestId]=useState(()=>crypto.randomUUID());
  const [error,setError]=useState('');
  const showError=(cause:unknown)=>setError(axios.isAxiosError(cause)?cause.response?.data?.message||cause.message:String(cause));
  const inspect=useMutation({ mutationFn:async()=> (await apiClient.post<Preview>(`/api/Admin/users/${user.userId}/progress-reset/preview`,{groups})).data,
    onSuccess:value=>{setPreview(value);setRequestId(crypto.randomUUID());setError('');},onError:showError });
  const execute=useMutation({ mutationFn:async()=> (await apiClient.post<{message:string}>(`/api/Admin/users/${user.userId}/progress-reset`,{
    groups,expectedRevision:preview!.revision,requestId})).data,
    onSuccess:value=>onDone(value.message),onError:cause=>{showError(cause);if(axios.isAxiosError(cause)&&cause.response?.status===409)setPreview(null);} });
  const busy=inspect.isPending||execute.isPending;
  return <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4 backdrop-blur-sm" role="dialog" aria-modal="true" aria-label="Đặt lại tiến trình">
    <section className="max-h-[90vh] w-full max-w-2xl space-y-5 overflow-y-auto rounded-xl border border-border bg-card p-6 shadow-2xl">
      <div><h2 className="text-xl font-semibold">Đặt lại tiến trình</h2><p className="mt-1 text-sm text-muted-foreground">{user.playerName} · {user.email}</p></div>
      <fieldset className="space-y-3" disabled={busy}><legend className="mb-2 text-sm font-medium">Chọn nhóm dữ liệu</legend>{choices.map(([key,label])=><label key={key} className="flex items-center gap-3 rounded-lg border border-border p-3 text-sm">
        <input type="checkbox" checked={groups.includes(key)} onChange={event=>{setGroups(event.target.checked?[...groups,key]:groups.filter(g=>g!==key));setPreview(null);setError('');}}/>{label}
      </label>)}</fieldset>
      <p className="text-xs text-muted-foreground">Giữ tài khoản, bạn bè, lịch sử trận, quyết định xử lý và nhật ký thưởng. Tiền/cosmetic đã cấp không được nhận lại sau reset.</p>
      {preview&&<div className="space-y-3 rounded-lg border border-amber-500/40 bg-amber-500/5 p-4">
        <h3 className="font-semibold">Tác động đã xem trước</h3><ul className="list-disc space-y-2 pl-5 text-sm">{preview.effects.map(effect=><li key={effect}>{effect}</li>)}</ul>
        <p className="text-xs text-muted-foreground">Hiện tại: cấp {preview.level}, Elo {preview.elo}; {preview.tokenpad} TOKENPAD, {preview.elite} Elite Token; {preview.ownedCosmetics.length} vật phẩm.
          {' '}{preview.lessonRecords} kết quả bài, {preview.masteryRecords} mastery, {preview.missionRecords} nhiệm vụ, {preview.achievementRecords} thành tựu.</p>
        {preview.busy&&<p className="text-sm text-red-400">Người chơi đang tìm trận hoặc có trận chưa hoàn tất. Chưa thể reset.</p>}
      </div>}
      {error&&<p role="alert" className="rounded-lg bg-red-500/10 p-3 text-sm text-red-400">{error}</p>}
      <footer className="flex flex-wrap justify-end gap-3 border-t border-border pt-4">
        <button onClick={onClose} disabled={busy} className="rounded-lg border border-border px-4 py-2 text-sm disabled:opacity-40">Hủy</button>
        <button onClick={()=>inspect.mutate()} disabled={busy||groups.length===0} className="rounded-lg border border-border px-4 py-2 text-sm disabled:opacity-40">{inspect.isPending?'Đang kiểm tra…':'Xem trước tác động'}</button>
        <button onClick={()=>execute.mutate()} disabled={busy||!preview||preview.busy} className="rounded-lg bg-amber-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-40">{execute.isPending?'Đang đặt lại…':'Xác nhận đặt lại'}</button>
      </footer>
    </section>
  </div>;
}
