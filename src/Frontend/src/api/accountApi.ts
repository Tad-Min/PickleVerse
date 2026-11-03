import { apiClient } from './client';
import type { PasswordRecoveryReceipt, ResetPasswordRequest } from '../types/api';

// Same wire contract as the Unity AccountService; all responses are unwrapped by client.ts.
export const accountApi = {
  forgotPassword: async (email: string): Promise<PasswordRecoveryReceipt> =>
    (await apiClient.post<PasswordRecoveryReceipt>('/api/Auth/forgot-password', { email })).data,
  resetPassword: async (request: ResetPasswordRequest): Promise<{ message: string }> =>
    (await apiClient.post<{ message: string }>('/api/Auth/reset-password', request)).data,
};
