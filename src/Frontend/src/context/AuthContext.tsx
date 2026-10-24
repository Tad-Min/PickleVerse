import React, { createContext, useContext, useState, useEffect } from 'react';
import { adminApi } from '../api/adminApi';
import { isMockMode } from '../api/mockMode';
import { UserSession } from '../types/api';

interface AuthContextType {
  user: UserSession | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  loading: boolean;
  login: (emailOrUsername: string, password: string) => Promise<{ success: boolean; message?: string }>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<UserSession | null>(null);
  const [loading, setLoading] = useState<boolean>(true);

  useEffect(() => {
    if (isMockMode) {
      setUser({
        userId: 'demo-admin-001',
        username: 'CourtAdmin',
        email: 'admin@pickleverse.local',
        role: 'Admin',
        token: 'pickleverse-local-demo-token',
      });
      setLoading(false);
      return;
    }

    try {
      const storedToken = localStorage.getItem('pickleverse_token');
      const storedUser = localStorage.getItem('pickleverse_user');
      if (storedToken && storedUser) {
        const parsed = JSON.parse(storedUser) as UserSession;
        setUser(parsed);
      }
    } catch {
      localStorage.removeItem('pickleverse_token');
      localStorage.removeItem('pickleverse_user');
    } finally {
      setLoading(false);
    }
  }, []);

  const login = async (emailOrUsername: string, password: string): Promise<{ success: boolean; message?: string }> => {
    try {
      // In PickleVerse backend, AuthController expects password or hash. Send directly:
      const res = await adminApi.login({
        emailOrUsername,
        passwordHash: password,
      });

      if (res.role !== 'Admin') {
        return {
          success: false,
          message: 'Tài khoản của bạn không có vai trò Quản trị viên (Admin). Truy cập bị từ chối.',
        };
      }

      const session: UserSession = {
        userId: res.userId,
        username: res.username,
        email: res.email,
        role: res.role,
        token: res.token,
      };

      localStorage.setItem('pickleverse_token', res.token);
      localStorage.setItem('pickleverse_user', JSON.stringify(session));
      setUser(session);

      return { success: true };
    } catch (err: unknown) {
      const error = err as { response?: { data?: { message?: string } } };
      return {
        success: false,
        message: error.response?.data?.message || 'Đăng nhập thất bại. Vui lòng kiểm tra lại thông tin.',
      };
    }
  };

  const logout = () => {
    if (isMockMode) return;
    localStorage.removeItem('pickleverse_token');
    localStorage.removeItem('pickleverse_user');
    setUser(null);
    window.location.href = '/login';
  };

  const value: AuthContextType = {
    user,
    isAuthenticated: !!user && user.role === 'Admin',
    isAdmin: user?.role === 'Admin',
    loading,
    login,
    logout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
