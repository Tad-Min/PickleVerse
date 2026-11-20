import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '../../../api/client';
import { btnPrimary, card, fieldLabel, inputClass, notice, rowHover, sectionTitle, tableHead, th, td, tone } from '../../../shared/ui/adminStyles';

export interface ReferenceParameters { contactWindowSeconds: number; earlyBufferSeconds: number; recoveryRadius: number; recoveryTimeoutSeconds: number }
export interface TechniqueReference { referenceId: string; techniqueId: number; technique: string; version: number; parameters: ReferenceParameters; createdAt: string }
export const loadReferences = async () => (await apiClient.get<TechniqueReference[]>('/api/Admin/cms/techniques/references')).data;

const initial: ReferenceParameters = { contactWindowSeconds: .17, earlyBufferSeconds: .23, recoveryRadius: .6, recoveryTimeoutSeconds: 4 };
const fields: [keyof ReferenceParameters, string, number, number, number][] = [
  ['contactWindowSeconds', 'Cửa sổ tiếp xúc (giây)', .05, .3, .01],
  ['earlyBufferSeconds', 'Hỗ trợ bấm sớm (giây)', 0, .5, .01],
  ['recoveryRadius', 'Bán kính hồi vị (m)', .3, 1.5, .05],
  ['recoveryTimeoutSeconds', 'Thời gian hồi vị (giây)', 2, 10, .1],
];

export function TechniqueReferences() {
  const client = useQueryClient();
  const query = useQuery({ queryKey: ['cms-technique-references'], queryFn: loadReferences });
  const [technique, setTechnique] = useState<number>(0);
  const [parameters, setParameters] = useState(initial);
  const [message, setMessage] = useState('');

  const latest = query.data?.filter(x => x.techniqueId === technique).sort((a, b) => b.version - a.version)[0];
  const techniques = [...new Map(query.data?.map(x => [x.techniqueId, x.technique])).entries()];
  const versions = query.data?.filter(x => x.techniqueId === technique) ?? [];

  const create = useMutation({
    mutationFn: async () =>
      apiClient.post(`/api/Admin/cms/techniques/${technique}/references`, { expectedLatestVersion: latest?.version || 0, parameters }),
    onSuccess: () => {
      setMessage('Đã tạo phiên bản mới. Chọn phiên bản này trong bài học và xuất bản để áp dụng.');
      client.invalidateQueries({ queryKey: ['cms-technique-references'] });
    },
    onError: error => setMessage((error as { response?: { data?: { message?: string } } }).response?.data?.message || (error as Error).message),
  });

  return (
    <details className={`${card} group`}>
      <summary className="flex cursor-pointer list-none items-center justify-between gap-3">
        <span className={sectionTitle}>Dữ liệu kỹ thuật tham chiếu có phiên bản</span>
        <span className="text-xs text-muted-foreground group-open:hidden">Mở rộng</span>
        <span className="hidden text-xs text-muted-foreground group-open:inline">Thu gọn</span>
      </summary>

      <div className="mt-4 space-y-5">
        <p className="text-xs text-muted-foreground">
          Các giá trị này điều chỉnh luyện tập từ mẫu Unity hiện có. Ngưỡng coaching AI cần được hiệu chỉnh và nghiệm thu riêng với chuyên gia. Phiên bản cũ được giữ nguyên cho các lượt đã bắt đầu.
        </p>

        {query.error && <p role="alert" className="text-xs text-rose-600">{(query.error as Error).message}</p>}
        {message && <p role="status" className={`${notice('success')} text-xs`}>{message}</p>}

        <form
          className="space-y-4"
          onSubmit={e => { e.preventDefault(); create.mutate(); }}
        >
          <label className={`${fieldLabel} md:w-72`}>
            Kỹ thuật
            <select
              className={inputClass}
              value={technique}
              required
              onChange={e => {
                const id = Number(e.target.value);
                setTechnique(id);
                setParameters(query.data?.find(x => x.techniqueId === id)?.parameters || initial);
              }}
            >
              <option value={0}>Chọn kỹ thuật</option>
              {techniques.map(([id, name]) => <option value={id} key={id}>{name}</option>)}
            </select>
          </label>

          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            {fields.map(([key, label, min, max, step]) => (
              <label key={key} className={fieldLabel}>
                {label}
                <input
                  className={inputClass}
                  type="number"
                  value={parameters[key]}
                  min={min}
                  max={max}
                  step={step}
                  required
                  onChange={e => setParameters(old => ({ ...old, [key]: Number(e.target.value) }))}
                />
              </label>
            ))}
          </div>

          <div className="flex justify-end">
            <button className={btnPrimary} disabled={!latest || create.isPending}>
              Tạo phiên bản {latest ? latest.version + 1 : ''}
            </button>
          </div>
        </form>

        <div className="overflow-x-auto rounded-lg border border-border">
          <table className="w-full text-left text-xs">
            <thead className={tableHead}>
              <tr>
                <th className={th}>Phiên bản</th>
                <th className={th}>Tiếp xúc</th>
                <th className={th}>Bấm sớm</th>
                <th className={th}>Hồi vị</th>
                <th className={th}>Thời gian</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {versions.map((x, index) => (
                <tr key={x.referenceId} className={rowHover}>
                  <td className={td}>
                    <span className={`inline-flex items-center rounded border px-2 py-0.5 font-mono text-[11px] ${index === 0 ? tone.blue : tone.neutral}`}>
                      v{x.version}{index === 0 ? ' · mới nhất' : ''}
                    </span>
                  </td>
                  <td className={`${td} font-mono`}>{x.parameters.contactWindowSeconds}s</td>
                  <td className={`${td} font-mono`}>{x.parameters.earlyBufferSeconds}s</td>
                  <td className={`${td} font-mono`}>{x.parameters.recoveryRadius}m</td>
                  <td className={`${td} font-mono`}>{x.parameters.recoveryTimeoutSeconds}s</td>
                </tr>
              ))}
              {technique !== 0 && versions.length === 0 && (
                <tr>
                  <td colSpan={5} className="py-6 px-4 text-center text-muted-foreground">Kỹ thuật này chưa có phiên bản tham chiếu.</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </details>
  );
}
