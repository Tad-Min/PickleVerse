import { TechniqueReferences } from './TechniqueReferences';
import { LessonManager } from './LessonManager';
import { DrillManager } from './DrillManager';
import React, { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { adminApi } from '../../../api/adminApi';
import { CreateCosmeticRequest, CreateMissionRequest } from '../../../types/api';
import { BookOpen, Target, ShoppingBag, Award, CheckCircle2, AlertCircle, X, FolderKanban } from 'lucide-react';
import { PageHeader } from '../../../shared/ui/PageHeader';
import {
  btnPrimary, btnTab, card, fieldLabel, inputClass, notice, sectionTitle, toolbar,
} from '../../../shared/ui/adminStyles';

type CmsTab = 'LESSON' | 'DRILL' | 'COSMETIC' | 'MISSION';

const TABS: { key: CmsTab; label: string; icon: React.ElementType }[] = [
  { key: 'LESSON', label: '1. Thêm Bài học', icon: BookOpen },
  { key: 'DRILL', label: '2. Thêm Bài tập', icon: Target },
  { key: 'COSMETIC', label: '3. Đăng bán Vật phẩm (Cửa hàng)', icon: ShoppingBag },
  { key: 'MISSION', label: '4. Tạo Nhiệm vụ', icon: Award },
];

const emptyCosmetic = (): CreateCosmeticRequest => ({
  itemCode: '', cosmeticName: '', description: '', price: 100, type: 'PADDLE', currencyId: 1,
});
const emptyMission = (): CreateMissionRequest => ({
  missionName: '', description: '', type: 'DAILY', target: 5, reward: { coins: 100, gems: 0, xp: 0 },
});

export const CMS: React.FC = () => {
  const [activeTab, setActiveTab] = useState<CmsTab>('LESSON');
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [cosmeticForm, setCosmeticForm] = useState<CreateCosmeticRequest>(emptyCosmetic);
  const [missionForm, setMissionForm] = useState<CreateMissionRequest>(emptyMission);

  const createCosmeticMutation = useMutation({
    mutationFn: (data: CreateCosmeticRequest) => adminApi.createCosmetic(data),
    onSuccess: (res) => {
      setFeedback({ type: 'success', text: `${res.message} (ID: ${res.cosmeticId})` });
      setCosmeticForm(emptyCosmetic());
    },
    onError: () => setFeedback({ type: 'error', text: 'Đăng bán vật phẩm thất bại.' }),
  });

  const createMissionMutation = useMutation({
    mutationFn: (data: CreateMissionRequest) => adminApi.createMission(data),
    onSuccess: (res) => {
      setFeedback({ type: 'success', text: `${res.message} (ID: ${res.missionId})` });
      setMissionForm(emptyMission());
    },
    onError: () => setFeedback({ type: 'error', text: 'Tạo nhiệm vụ thất bại.' }),
  });

  const selectTab = (tab: CmsTab) => {
    setActiveTab(tab);
    setFeedback(null);
  };

  return (
    <div className="space-y-6">
      <TechniqueReferences />

      {/* Title */}
      <PageHeader
        icon={FolderKanban}
        title="Quản trị Nội dung (CMS)"
        subtitle="Cập nhật bài học, drill kỹ thuật, mở bán vật phẩm và quản lý nhiệm vụ trực tiếp vào Game"
      />

      {/* Feedback */}
      {feedback && (
        <div role="status" className={notice(feedback.type)}>
          <div className="flex items-center gap-2">
            {feedback.type === 'success' ? <CheckCircle2 className="w-4 h-4" /> : <AlertCircle className="w-4 h-4" />}
            <span>{feedback.text}</span>
          </div>
          <button onClick={() => setFeedback(null)} className="p-1 hover:opacity-80" aria-label="Đóng thông báo">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {/* Tabs */}
      <div className={toolbar}>
        <div className="flex flex-wrap items-center gap-2">
          {TABS.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              onClick={() => selectTab(key)}
              className={`${btnTab(activeTab === key)} inline-flex items-center gap-2`}
            >
              <Icon className="w-3.5 h-3.5" />
              <span>{label}</span>
            </button>
          ))}
        </div>
      </div>

      {activeTab === 'LESSON' && <LessonManager />}
      {activeTab === 'DRILL' && <DrillManager />}

      {/* Cosmetic */}
      {activeTab === 'COSMETIC' && (
        <section className={`${card} max-w-3xl space-y-5`}>
          <div>
            <h2 className={sectionTitle}>Đăng bán Vật phẩm vào Cửa hàng</h2>
            <p className="text-xs text-muted-foreground mt-1">
              Vật phẩm mới sẽ hiện trong cửa hàng của game ngay sau khi đăng bán.
            </p>
          </div>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              createCosmeticMutation.mutate(cosmeticForm);
            }}
            className="space-y-4"
          >
            <div className="grid gap-4 md:grid-cols-2">
              <label className={fieldLabel}>
                Mã vật phẩm *
                <input
                  type="text"
                  value={cosmeticForm.itemCode}
                  onChange={(e) => setCosmeticForm({ ...cosmeticForm, itemCode: e.target.value })}
                  placeholder="VD: PADDLE_CYBER_2026"
                  required
                  className={`${inputClass} font-mono`}
                />
              </label>
              <label className={fieldLabel}>
                Phân loại
                <select
                  value={cosmeticForm.type}
                  onChange={(e) => setCosmeticForm({ ...cosmeticForm, type: e.target.value })}
                  className={inputClass}
                >
                  <option value="PADDLE">Vợt Pickleball</option>
                  <option value="OUTFIT">Trang phục thi đấu</option>
                  <option value="HAT">Mũ / Phụ kiện</option>
                </select>
              </label>
            </div>

            <label className={fieldLabel}>
              Tên vật phẩm *
              <input
                type="text"
                value={cosmeticForm.cosmeticName}
                onChange={(e) => setCosmeticForm({ ...cosmeticForm, cosmeticName: e.target.value })}
                placeholder="VD: Vợt Cyber Neon Dragon"
                required
                className={inputClass}
              />
            </label>

            <div className="grid gap-4 md:grid-cols-2">
              <label className={fieldLabel}>
                Giá bán
                <input
                  type="number"
                  min={0}
                  value={cosmeticForm.price}
                  onChange={(e) => setCosmeticForm({ ...cosmeticForm, price: Number(e.target.value) })}
                  className={inputClass}
                />
              </label>
              <label className={fieldLabel}>
                Loại tiền tệ
                <select
                  value={cosmeticForm.currencyId}
                  onChange={(e) => setCosmeticForm({ ...cosmeticForm, currencyId: Number(e.target.value) })}
                  className={inputClass}
                >
                  <option value={1}>Vàng (thưởng trong game)</option>
                  <option value={2}>Kim cương (cao cấp)</option>
                </select>
              </label>
            </div>

            <div className="flex justify-end pt-2">
              <button
                type="submit"
                disabled={createCosmeticMutation.isPending || !cosmeticForm.itemCode.trim()}
                className={btnPrimary}
              >
                {createCosmeticMutation.isPending ? 'Đang đăng bán…' : 'Đăng bán vật phẩm'}
              </button>
            </div>
          </form>
        </section>
      )}

      {/* Mission */}
      {activeTab === 'MISSION' && (
        <section className={`${card} max-w-3xl space-y-5`}>
          <div>
            <h2 className={sectionTitle}>Tạo Nhiệm vụ Sự kiện Mới</h2>
            <p className="text-xs text-muted-foreground mt-1">
              Nhiệm vụ và phần thưởng sẽ áp dụng cho người chơi theo chu kỳ đã chọn.
            </p>
          </div>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              createMissionMutation.mutate(missionForm);
            }}
            className="space-y-4"
          >
            <label className={fieldLabel}>
              Tên nhiệm vụ *
              <input
                type="text"
                value={missionForm.missionName}
                onChange={(e) => setMissionForm({ ...missionForm, missionName: e.target.value })}
                placeholder="VD: Chiến thắng 3 trận Đơn trong ngày"
                required
                className={inputClass}
              />
            </label>

            <div className="grid gap-4 md:grid-cols-2">
              <label className={fieldLabel}>
                Chu kỳ nhiệm vụ
                <select
                  value={missionForm.type}
                  onChange={(e) => setMissionForm({ ...missionForm, type: e.target.value })}
                  className={inputClass}
                >
                  <option value="DAILY">Hàng ngày</option>
                  <option value="WEEKLY">Hàng tuần</option>
                </select>
              </label>
              <label className={fieldLabel}>
                Mục tiêu số lần
                <input
                  type="number"
                  min={1}
                  value={missionForm.target}
                  onChange={(e) => setMissionForm({ ...missionForm, target: Number(e.target.value) })}
                  className={inputClass}
                />
              </label>
            </div>

            <div>
              <p className="text-xs font-medium text-foreground mb-2">Phần thưởng</p>
              <div className="grid gap-3 md:grid-cols-3">
                {(['coins', 'gems', 'xp'] as const).map((key) => (
                  <label key={key} className={fieldLabel}>
                    {key === 'coins' ? 'TOKENPAD' : key === 'gems' ? 'Token cao cấp' : 'XP'}
                    <input
                      type="number"
                      min={0}
                      required
                      value={missionForm.reward[key]}
                      onChange={(e) =>
                        setMissionForm({ ...missionForm, reward: { ...missionForm.reward, [key]: +e.target.value } })
                      }
                      className={inputClass}
                    />
                  </label>
                ))}
              </div>
            </div>

            <div className="flex justify-end pt-2">
              <button
                type="submit"
                disabled={createMissionMutation.isPending || !missionForm.missionName.trim()}
                className={btnPrimary}
              >
                {createMissionMutation.isPending ? 'Đang tạo…' : 'Tạo nhiệm vụ mới'}
              </button>
            </div>
          </form>
        </section>
      )}
    </div>
  );
};
