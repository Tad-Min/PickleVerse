import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../context/AuthContext';
import { isMockMode } from '../../../api/mockMode';
import { Shield, Lock, User, AlertCircle, ArrowRight, ShieldCheck } from 'lucide-react';

const field =
  'w-full pl-10 pr-4 py-3 bg-background border border-border rounded-xl text-sm text-foreground placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-shadow disabled:opacity-60';

export const Login: React.FC = () => {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!username.trim() || !password.trim()) {
      setError('Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.');
      return;
    }

    setError(null);
    setSubmitting(true);

    const result = await login(username, password);
    setSubmitting(false);

    if (result.success) {
      navigate('/');
    } else {
      setError(result.message || 'Đăng nhập không thành công.');
    }
  };

  return (
    <div className="flex min-h-screen select-none items-center justify-center p-6">
      <div className="w-full max-w-md animate-fade-up">
        <div className="mb-8 text-center">
          <div className="mx-auto mb-5 flex h-14 w-14 items-center justify-center rounded-2xl bg-gradient-to-br from-primary to-blue-500 text-white shadow-glow">
            <Shield className="h-7 w-7" />
          </div>
          <h1 className="font-display text-3xl font-bold tracking-tight text-foreground">Cổng PickleVerse</h1>
          <p className="mt-2 text-sm text-muted-foreground">Đăng nhập hệ thống Quản trị & Điều hành</p>
        </div>

        <div className="rounded-3xl border border-border bg-card p-8 shadow-lift">
          {isMockMode && (
            <div className="mb-5 rounded-xl border border-blue-500/20 bg-blue-500/10 px-4 py-3 text-xs text-blue-700">
              <div className="font-semibold">Chế độ xem thử — không kết nối backend</div>
              <div className="mt-1">Tài khoản: <code>admin@pickleverse.local</code></div>
              <div>Mật khẩu: <code>Admin123!</code></div>
            </div>
          )}

          {error && (
            <div role="alert" className="mb-5 flex items-start gap-3 rounded-xl border border-rose-500/20 bg-rose-500/10 p-3.5 text-xs text-rose-600">
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-5">
            <div>
              <label className="mb-2 block text-xs font-semibold uppercase tracking-wider text-foreground">
                Tài khoản hoặc Email
              </label>
              <div className="relative">
                <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-muted-foreground">
                  <User className="h-4 w-4" />
                </div>
                <input
                  type="text"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  placeholder="admin@pickleverse.vn"
                  disabled={submitting}
                  className={field}
                  required
                />
              </div>
            </div>

            <div>
              <label className="mb-2 block text-xs font-semibold uppercase tracking-wider text-foreground">
                Mật khẩu bảo mật
              </label>
              <div className="relative">
                <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-muted-foreground">
                  <Lock className="h-4 w-4" />
                </div>
                <input
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••••••"
                  disabled={submitting}
                  className={field}
                  required
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={submitting}
              className="flex w-full items-center justify-center gap-2 rounded-xl bg-primary px-4 py-3.5 text-sm font-semibold text-white shadow-glow transition-all hover:bg-primary-hover active:scale-[0.99] disabled:cursor-not-allowed disabled:opacity-60"
            >
              {submitting ? (
                <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
              ) : (
                <>
                  <span>Xác thực & Truy cập</span>
                  <ArrowRight className="h-4 w-4" />
                </>
              )}
            </button>
          </form>

          <div className="mt-6 flex items-start gap-3 border-t border-border pt-5">
            <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-emerald-600" />
            <p className="text-[11px] leading-relaxed text-muted-foreground">
              Chỉ tài khoản được cấp quyền <span className="font-semibold text-emerald-600">Quản trị viên</span> mới có quyền đăng nhập. Mọi lượt truy cập trái phép đều được ghi nhận vào biên bản an ninh.
            </p>
          </div>
        </div>
      </div>
    </div>
  );
};
