import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';

const BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080';

export const apiClient = axios.create({
  baseURL: BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 10000,
});

// Request Interceptor: Attach JWT Token
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const token = localStorage.getItem('pickleverse_token');
    if (token && config.headers) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error: AxiosError) => Promise.reject(error)
);

// Unwrap the API contract once so feature clients continue receiving their DTOs.
apiClient.interceptors.response.use(
  (response) => {
    const envelope = response.data;
    if (envelope && typeof envelope === 'object' && typeof envelope.success === 'boolean') {
      if (!envelope.success) {
        return Promise.reject(new AxiosError(envelope.message || 'Yêu cầu không thành công.', 'ERR_BAD_RESPONSE', response.config, response.request, response));
      }
      if (Object.prototype.hasOwnProperty.call(envelope, 'data')) {
        response.data = envelope.data;
      }
    }
    return response;
  },
  (error: AxiosError<{ message?: string }>) => {
    if (error.response?.status === 401) {
      // Clear invalid credentials
      localStorage.removeItem('pickleverse_token');
      localStorage.removeItem('pickleverse_user');
      if (window.location.pathname !== '/login') {
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);
