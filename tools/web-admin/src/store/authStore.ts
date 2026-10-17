import { create } from 'zustand';
import { AuthMeResponse, LoginRequest, RegisterRequest } from '../types/auth';
import { api, setToken } from '../lib/api';

interface AuthState {
  user: AuthMeResponse | null;
  token: string | null;
  isLoading: boolean;
  error: string | null;
  login: (req: LoginRequest) => Promise<boolean>;
  register: (req: RegisterRequest) => Promise<boolean>;
  logout: () => void;
  checkAuth: () => Promise<void>;
  clearError: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  token: localStorage.getItem('sv_auth_token'),
  isLoading: false,
  error: null,

  clearError: () => set({ error: null }),

  login: async (req: LoginRequest) => {
    set({ isLoading: true, error: null });
    try {
      const session = await api.auth.login(req);
      setToken(session.token);
      set({
        token: session.token,
        user: {
          authenticated: true,
          farmerId: session.farmerId,
          username: session.username,
          role: session.role,
        },
        isLoading: false,
      });
      return true;
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Login failed';
      set({ error: msg, isLoading: false });
      return false;
    }
  },

  register: async (req: RegisterRequest) => {
    set({ isLoading: true, error: null });
    try {
      const session = await api.auth.register(req);
      setToken(session.token);
      set({
        token: session.token,
        user: {
          authenticated: true,
          farmerId: session.farmerId,
          username: session.username,
          role: session.role,
        },
        isLoading: false,
      });
      return true;
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Registration failed';
      set({ error: msg, isLoading: false });
      return false;
    }
  },

  logout: () => {
    setToken(null);
    set({ token: null, user: null, error: null });
  },

  checkAuth: async () => {
    const token = localStorage.getItem('sv_auth_token');
    if (!token) {
      set({ user: null, isLoading: false });
      return;
    }
    set({ isLoading: true });
    try {
      const user = await api.auth.me();
      set({ user, isLoading: false });
    } catch {
      setToken(null);
      set({ token: null, user: null, isLoading: false });
    }
  },
}));
