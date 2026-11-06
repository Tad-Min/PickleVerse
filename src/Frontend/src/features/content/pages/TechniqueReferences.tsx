import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../../api/client';

export interface ReferenceParameters { contactWindowSeconds: number; earlyBufferSeconds: number; recoveryRadius: number; recoveryTimeoutSeconds: number }
export interface TechniqueReference { referenceId: string; techniqueId: number; technique: string; version: number; parameters: ReferenceParameters; createdAt: string }
export const loadReferences = async () => (await apiClient.get<TechniqueReference[]>('/api/Admin/cms/techniques/references')).data;
const initial: ReferenceParameters = { contactWindowSeconds: .17, earlyBufferSeconds: .23, recoveryRadius: .6, recoveryTimeoutSeconds: 4 };
const fields: [keyof ReferenceParameters, string, number, number, number][] = [
  ['contactWindowSeconds','Cửa sổ tiếp xúc (giây)',.05,.3,.01], ['earlyBufferSeconds','Hỗ trợ bấm sớm (giây)',0,.5,.01],
  ['recoveryRadius','Bán kính hồi vị (m)',.3,1.5,.05], ['recoveryTimeoutSeconds','Thời gian hồi vị (giây)',2,10,.1],
];
export function TechniqueReferences() {
  const client = useQueryClient(); const query = useQuery({ queryKey: ['cms-technique-references'], queryFn: loadReferences });
  const [technique, setTechnique] = useState<number>(0); const [parameters, setParameters] = useState(initial); const [message, setMessage] = useState('');
  const latest = query.data?.filter(x => x.techniqueId === technique).sort((a,b) => b.version-a.version)[0];
  const techniques = [...new Map(query.data?.map(x => [x.techniqueId,x.technique])).entries()];
  const create = useMutation({ mutationFn: async () => apiClient.post(`/api/Admin/cms/techniques/${technique}/references`, { expectedLatestVersion: latest?.version || 0, parameters }),
    onSuccess: () => { setMessage('Đã tạo phiên bản mới. Chọn phiên bản này trong bài học và xuất bản để áp dụng.'); client.invalidateQueries({ queryKey: ['cms-technique-references'] }); },
    onError: error => setMessage((error as { response?: { data?: { message?: string } } }).response?.data?.message || (error as Error).message) });
  return <details className="rounded-xl border border-border bg-card p-4 my-5"><summary className="font-semibold cursor-pointer">Dữ liệu kỹ thuật tham chiếu có phiên bản</summary>
    <p className="text-sm my-3">Các giá trị này điều chỉnh luyện tập từ mẫu Unity hiện có. Ngưỡng coaching AI cần được hiệu chỉnh và nghiệm thu riêng với chuyên gia. Phiên bản cũ được giữ nguyên cho các lượt đã bắt đầu.</p>
    {query.error && <p role="alert">{(query.error as Error).message}</p>}{message && <p role="status">{message}</p>}
    <form className="space-y-4" onSubmit={e => { e.preventDefault(); create.mutate(); }}>
      <select className="bg-background border rounded p-2" value={technique} onChange={e => { const id=Number(e.target.value); setTechnique(id); setParameters(query.data?.find(x => x.techniqueId===id)?.parameters || initial); }} required>
        <option value={0}>Chọn kỹ thuật</option>{techniques.map(([id,name]) => <option value={id} key={id}>{name}</option>)}
      </select>
      <div className="grid md:grid-cols-4 gap-3">{fields.map(([key,label,min,max,step]) => <label key={key} className="text-sm">{label}<input className="block bg-background border p-2 rounded w-full" type="number" value={parameters[key]} min={min} max={max} step={step} required onChange={e => setParameters(old => ({ ...old, [key]: Number(e.target.value) }))} /></label>)}</div>
      <button className="bg-primary px-4 py-2 rounded disabled:opacity-40" disabled={!latest || create.isPending}>Tạo phiên bản {latest ? latest.version + 1 : ''}</button>
    </form>
    <table className="w-full text-left text-sm mt-4"><thead><tr><th>Phiên bản</th><th>Tiếp xúc</th><th>Bấm sớm</th><th>Hồi vị</th><th>Thời gian</th></tr></thead><tbody>{query.data?.filter(x => x.techniqueId===technique).map(x => <tr key={x.referenceId} className="border-t"><td className="py-2">{x.version}</td><td>{x.parameters.contactWindowSeconds}s</td><td>{x.parameters.earlyBufferSeconds}s</td><td>{x.parameters.recoveryRadius}m</td><td>{x.parameters.recoveryTimeoutSeconds}s</td></tr>)}</tbody></table>
  </details>;
}
