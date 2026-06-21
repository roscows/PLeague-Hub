import type { AuthResponse, LoginRequest, RegisterRequest } from '../types/api';
import { api } from './api';

export const authApi = {
  async login(request: LoginRequest) {
    const response = await api.post<AuthResponse>('/api/auth/login', request);
    return response.data;
  },
  async register(request: RegisterRequest) {
    const response = await api.post<AuthResponse>('/api/auth/register', request);
    return response.data;
  }
};
