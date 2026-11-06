import { TechniqueReferences } from './TechniqueReferences';
import { LessonManager } from './LessonManager';
import React, { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { adminApi } from '../../../api/adminApi';
import {
  CreateCosmeticRequest,
  CreateMissionRequest,
} from '../../../types/api';
import { BookOpen, Target, ShoppingBag, Award, CheckCircle2, AlertCircle } from 'lucide-react';

export const CMS: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'LESSON' | 'DRILL' | 'COSMETIC' | 'MISSION'>('LESSON');
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  // Cosmetic Form State
  const [cosmeticForm, setCosmeticForm] = useState<CreateCosmeticRequest>({
    itemCode: '',
    cosmeticName: '',
    description: '',
    price: 100,
    type: 'PADDLE',
    currencyId: 1, // 1: Gold, 2: Gem
  });

  // Mission Form State
  const [missionForm, setMissionForm] = useState<CreateMissionRequest>({
    missionName: '',
    description: '',
    type: 'DAILY',
    target: 5,
    reward: { coins: 100, gems: 0, xp: 0 },
  });

  // Mutations
  const createCosmeticMutation = useMutation({
    mutationFn: (data: CreateCosmeticRequest) => adminApi.createCosmetic(data),
    onSuccess: (res) => {
      setFeedback({ type: 'success', text: `${res.message} (ID: ${res.cosmeticId})` });
      setCosmeticForm({ itemCode: '', cosmeticName: '', description: '', price: 100, type: 'PADDLE', currencyId: 1 });
    },
    onError: () => setFeedback({ type: 'error', text: 'Đăng bán vật phẩm thất bại.' }),
  });

  const createMissionMutation = useMutation({
    mutationFn: (data: CreateMissionRequest) => adminApi.createMission(data),
    onSuccess: (res) => {
      setFeedback({ type: 'success', text: `${res.message} (ID: ${res.missionId})` });
      setMissionForm({ missionName: '', description: '', type: 'DAILY', target: 5, reward: { coins: 100, gems: 0, xp: 0 } });
    },
    onError: () => setFeedback({ type: 'error', text: 'Tạo nhiệm vụ thất bại.' }),
  });

  return (
    <div className="space-y-6">
      <TechniqueReferences />
      {/* Title */}
      <div>
        <h1 className="text-2xl font-bold font-display text-foreground tracking-tight">Quản trị Nội dung (CMS)</h1>
        <p className="text-sm text-muted-foreground mt-1">
          Cập nhật bài học, drill kỹ thuật, mở bán vật phẩm và quản lý nhiệm vụ trực tiếp vào Game
        </p>
      </div>

      {/* Feedback Banner */}
      {feedback && (
        <div
          className={`p-3.5 rounded-lg flex items-center justify-between text-xs font-medium border ${
            feedback.type === 'success'
              ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20'
              : 'bg-rose-500/10 text-rose-400 border-rose-500/20'
          }`}
        >
          <div className="flex items-center gap-2">
            {feedback.type === 'success' ? <CheckCircle2 className="w-4 h-4" /> : <AlertCircle className="w-4 h-4" />}
            <span>{feedback.text}</span>
          </div>
          <button onClick={() => setFeedback(null)} className="text-muted-foreground hover:text-foreground text-xs">
            Đóng
          </button>
        </div>
      )}

      {/* Tabs Switcher */}
      <div className="flex items-center gap-2 border-b border-border pb-3">
        <button
          onClick={() => {
            setActiveTab('LESSON');
            setFeedback(null);
          }}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-semibold transition-all ${
            activeTab === 'LESSON'
              ? 'bg-primary text-white shadow-sm'
              : 'text-muted-foreground hover:text-foreground hover:bg-slate-800'
          }`}
        >
          <BookOpen className="w-4 h-4" />
          <span>1. Thêm Bài học (Lesson)</span>
        </button>

        <button
          onClick={() => {
            setActiveTab('DRILL');
            setFeedback(null);
          }}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-semibold transition-all ${
            activeTab === 'DRILL'
              ? 'bg-primary text-white shadow-sm'
              : 'text-muted-foreground hover:text-foreground hover:bg-slate-800'
          }`}
        >
          <Target className="w-4 h-4" />
          <span>2. Thêm Bài tập (Drill)</span>
        </button>

        <button
          onClick={() => {
            setActiveTab('COSMETIC');
            setFeedback(null);
          }}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-semibold transition-all ${
            activeTab === 'COSMETIC'
              ? 'bg-primary text-white shadow-sm'
              : 'text-muted-foreground hover:text-foreground hover:bg-slate-800'
          }`}
        >
          <ShoppingBag className="w-4 h-4" />
          <span>3. Đăng bán Vật phẩm (Shop)</span>
        </button>

        <button
          onClick={() => {
            setActiveTab('MISSION');
            setFeedback(null);
          }}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-semibold transition-all ${
            activeTab === 'MISSION'
              ? 'bg-primary text-white shadow-sm'
              : 'text-muted-foreground hover:text-foreground hover:bg-slate-800'
          }`}
        >
          <Award className="w-4 h-4" />
          <span>4. Tạo Nhiệm vụ (Mission)</span>
        </button>
      </div>

      {(activeTab === 'LESSON' || activeTab === 'DRILL') && <LessonManager />}

      {/* TAB CONTENT 3: COSMETIC */}
      {activeTab === 'COSMETIC' && (
        <div className="bg-card border border-border rounded-xl p-6 max-w-2xl shadow-sm">
          <h2 className="text-base font-bold font-display text-foreground mb-4">Đăng bán Vật phẩm vào Shop</h2>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              createCosmeticMutation.mutate(cosmeticForm);
            }}
            className="space-y-4"
          >
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-foreground mb-1.5">Mã vật phẩm (Item Code) *</label>
                <input
                  type="text"
                  value={cosmeticForm.itemCode}
                  onChange={(e) => setCosmeticForm({ ...cosmeticForm, itemCode: e.target.value })}
                  placeholder="VD: PADDLE_CYBER_2026"
                  required
                  className="w-full p-2 bg-slate-900 border border-border rounded-lg text-xs text-foreground font-mono"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-foreground mb-1.5">Phân loại (Type)</label>
                <select
                  value={cosmeticForm.type}
                  onChange={(e) => setCosmeticForm({ ...cosmeticForm, type: e.target.value })}
                  className="w-full p-2 bg-slate-900 border border-border rounded-lg text-xs text-foreground"
                >
                  <option value="PADDLE">Vợt Pickleball (PADDLE)</option>
                  <option value="OUTFIT">Trang phục thi đấu (OUTFIT)</option>
                  <option value="HAT">Mũ / Phụ kiện (HAT)</option>
                </select>
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-foreground mb-1.5">Tên vật phẩm *</label>
              <input
                type="text"
                value={cosmeticForm.cosmeticName}
                onChange={(e) => setCosmeticForm({ ...cosmeticForm, cosmeticName: e.target.value })}
                placeholder="VD: Vợt Cyber Neon Dragon"
                required
                className="w-full p-2.5 bg-slate-900 border border-border rounded-lg text-xs text-foreground focus:ring-2 focus:ring-primary"
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-foreground mb-1.5">Giá bán (Price)</label>
                <input
                  type="number"
                  min={0}
                  value={cosmeticForm.price}
                  onChange={(e) => setCosmeticForm({ ...cosmeticForm, price: Number(e.target.value) })}
                  className="w-full p-2 bg-slate-900 border border-border rounded-lg text-xs text-foreground"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-foreground mb-1.5">Loại tiền tệ</label>
                <select
                  value={cosmeticForm.currencyId}
                  onChange={(e) => setCosmeticForm({ ...cosmeticForm, currencyId: Number(e.target.value) })}
                  className="w-full p-2 bg-slate-900 border border-border rounded-lg text-xs text-foreground"
                >
                  <option value={1}>Gold (Vàng thưởng trong game)</option>
                  <option value={2}>Gem (Kim cương cao cấp)</option>
                </select>
              </div>
            </div>

            <button
              type="submit"
              disabled={createCosmeticMutation.isPending || !cosmeticForm.itemCode.trim()}
              className="py-2.5 px-5 bg-primary hover:bg-primary-hover text-white text-xs font-semibold rounded-lg shadow-sm disabled:opacity-50"
            >
              {createCosmeticMutation.isPending ? 'Đang đăng bán...' : 'Đăng bán vật phẩm'}
            </button>
          </form>
        </div>
      )}

      {/* TAB CONTENT 4: MISSION */}
      {activeTab === 'MISSION' && (
        <div className="bg-card border border-border rounded-xl p-6 max-w-2xl shadow-sm">
          <h2 className="text-base font-bold font-display text-foreground mb-4">Tạo Nhiệm vụ Sự kiện Mới</h2>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              createMissionMutation.mutate(missionForm);
            }}
            className="space-y-4"
          >
            <div>
              <label className="block text-xs font-semibold text-foreground mb-1.5">Tên nhiệm vụ *</label>
              <input
                type="text"
                value={missionForm.missionName}
                onChange={(e) => setMissionForm({ ...missionForm, missionName: e.target.value })}
                placeholder="VD: Chiến thắng 3 trận Singles trong ngày"
                required
                className="w-full p-2.5 bg-slate-900 border border-border rounded-lg text-xs text-foreground focus:ring-2 focus:ring-primary"
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-foreground mb-1.5">Chu kỳ nhiệm vụ</label>
                <select
                  value={missionForm.type}
                  onChange={(e) => setMissionForm({ ...missionForm, type: e.target.value })}
                  className="w-full p-2 bg-slate-900 border border-border rounded-lg text-xs text-foreground"
                >
                  <option value="DAILY">Hàng ngày (DAILY)</option>
                  <option value="WEEKLY">Hàng tuần (WEEKLY)</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-foreground mb-1.5">Mục tiêu số lần (Target)</label>
                <input
                  type="number"
                  min={1}
                  value={missionForm.target}
                  onChange={(e) => setMissionForm({ ...missionForm, target: Number(e.target.value) })}
                  className="w-full p-2 bg-slate-900 border border-border rounded-lg text-xs text-foreground"
                />
              </div>
            </div>

            <div className="grid grid-cols-3 gap-3">{(['coins', 'gems', 'xp'] as const).map(key => <label key={key} className="text-xs">
              {key === 'coins' ? 'TOKENPAD' : key === 'gems' ? 'Elite Token' : 'XP'}
              <input type="number" min={0} required value={missionForm.reward[key]}
                onChange={e => setMissionForm({ ...missionForm, reward: { ...missionForm.reward, [key]: +e.target.value } })}
                className="mt-1 w-full rounded-lg border border-border bg-background p-2" />
            </label>)}</div>

            <button
              type="submit"
              disabled={createMissionMutation.isPending || !missionForm.missionName.trim()}
              className="py-2.5 px-5 bg-primary hover:bg-primary-hover text-white text-xs font-semibold rounded-lg shadow-sm disabled:opacity-50"
            >
              {createMissionMutation.isPending ? 'Đang tạo...' : 'Tạo nhiệm vụ mới'}
            </button>
          </form>
        </div>
      )}
    </div>
  );
};
