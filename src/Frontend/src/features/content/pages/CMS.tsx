import { TechniqueReferences } from './TechniqueReferences';
import { LessonManager } from './LessonManager';
import { ShopItemManager } from './ShopItemManager';
import React, { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { adminApi } from '../../../api/adminApi';
import { CreateMissionRequest } from '../../../types/api';
import { BookOpen, ShoppingBag, Award, CheckCircle2, AlertCircle, X, FolderKanban } from 'lucide-react';
import { PageHeader } from '../../../shared/ui/PageHeader';
import {
  btnPrimary, btnTab, card, fieldLabel, inputClass, notice, sectionTitle, toolbar,
} from '../../../shared/ui/adminStyles';

type CmsTab = 'LESSON' | 'COSMETIC' | 'MISSION';

const TABS: { key: CmsTab; label: string; icon: React.ElementType }[] = [
  { key: 'LESSON', label: '1. Bài học & Bài tập', icon: BookOpen },
  { key: 'COSMETIC', label: '2. Vật phẩm Cửa hàng', icon: ShoppingBag },
  { key: 'MISSION', label: '3. Tạo Nhiệm vụ', icon: Award },
];

const emptyMission = (): CreateMissionRequest => ({
  missionName: '', description: '', type: 'DAILY', target: 5, reward: { coins: 100, gems: 0, xp: 0 },
});

export const CMS: React.FC = () => {
  const [activeTab, setActiveTab] = useState<CmsTab>('LESSON');
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [missionForm, setMissionForm] = useState<CreateMissionRequest>(emptyMission);

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

      {activeTab === 'COSMETIC' && <ShopItemManager />}

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
