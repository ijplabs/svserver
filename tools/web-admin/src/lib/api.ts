import { LoginRequest, RegisterRequest, WebSessionInfo, AuthMeResponse } from '../types/auth';
import { ServerStatus, PlayersResponse, FarmCropsSnapshot, CabinsResponse } from '../types/farm';

const API_BASE = import.meta.env.VITE_API_URL || '';

function getToken(): string | null {
  return localStorage.getItem('sv_auth_token');
}

export function setToken(token: string | null) {
  if (token) {
    localStorage.setItem('sv_auth_token', token);
  } else {
    localStorage.removeItem('sv_auth_token');
  }
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const token = getToken();
  const headers = new Headers(options.headers || {});
  
  if (!headers.has('Content-Type') && options.body && typeof options.body === 'string') {
    headers.set('Content-Type', 'application/json');
  }

  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  const url = `${API_BASE}${path}`;
  const res = await fetch(url, {
    ...options,
    headers,
  });

  if (!res.ok) {
    let errorMsg = `HTTP ${res.status}: ${res.statusText}`;
    try {
      const errJson = await res.json();
      if (errJson && errJson.error) {
        errorMsg = errJson.error;
      } else if (errJson && errJson.message) {
        errorMsg = errJson.message;
      }
    } catch {
      // Non-JSON error body
    }
    throw new Error(errorMsg);
  }

  const text = await res.text();
  return text ? (JSON.parse(text) as T) : ({} as T);
}

export const api = {
  auth: {
    login: (body: LoginRequest) =>
      request<WebSessionInfo>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify(body),
      }),
    register: (body: RegisterRequest) =>
      request<WebSessionInfo>('/api/auth/register', {
        method: 'POST',
        body: JSON.stringify(body),
      }),
    me: () => request<AuthMeResponse>('/api/auth/me'),
  },
  server: {
    getStatus: () => request<ServerStatus>('/status'),
    getPlayers: () => request<PlayersResponse>('/players'),
    getCabins: () => request<CabinsResponse>('/cabins'),
    getCrops: () => request<FarmCropsSnapshot>('/farm/crops'),
    setTime: (value: number) =>
      request<{ success: boolean }>(`/time?value=${value}`, {
        method: 'POST',
      }),
    setClockSpeed: (multiplier: number) =>
      request<{ success: boolean }>(`/clock-speed?multiplier=${multiplier}`, {
        method: 'POST',
      }),
    setRendering: (fps: number) =>
      request<{ success: boolean }>(`/rendering?fps=${fps}`, {
        method: 'POST',
      }),
    reloadServer: () =>
      request<{ success: boolean }>('/reload', {
        method: 'POST',
      }),
  },
};
