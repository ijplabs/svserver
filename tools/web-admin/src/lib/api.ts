import { LoginRequest, RegisterRequest, WebSessionInfo, AuthMeResponse } from '../types/auth';
import { ServerStatus, PlayersResponse, FarmCropsSnapshot, CabinsResponse } from '../types/farm';
import { BackupEntry } from '../types/backup';
import { EconomyOverview } from '../types/economy';
import { AutomationStatus } from '../types/automation';

export function getApiBase(): string {
  // 1. Explicit build-time or runtime environment
  const envUrl = import.meta.env.VITE_API_URL;
  if (envUrl && typeof envUrl === 'string' && envUrl.trim() !== '') {
    return envUrl.trim().replace(/\/+$/, '');
  }

  if (typeof window !== 'undefined') {
    // 2. Local storage override (for testing or manual configuration)
    const customApi = localStorage.getItem('sv_api_url');
    if (customApi && customApi.trim() !== '') {
      return customApi.trim().replace(/\/+$/, '');
    }

    // 3. Auto-detection for ijplabs.dev domain
    // When accessing through svmonitor.ijplabs.dev, route directly to live backend stardew-api.ijplabs.dev
    const host = window.location.hostname;
    if (host.endsWith('ijplabs.dev')) {
      return 'https://stardew-api.ijplabs.dev';
    }
  }

  return '';
}

export const API_BASE = getApiBase();

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

  const base = getApiBase();
  const url = `${base}${path}`;
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
    pauseGame: (reason?: string) =>
      request<{ success: boolean; message: string; isPaused: boolean }>('/api/server/pause', {
        method: 'POST',
        body: JSON.stringify({ reason: reason || 'Dijeda via Web Admin' }),
      }),
    resumeGame: () =>
      request<{ success: boolean; message: string; isPaused: boolean }>('/api/server/resume', {
        method: 'POST',
      }),
    getPauseStatus: () =>
      request<{ isPaused: boolean; reason: string; pausedBy: string; remainingSeconds: number }>(
        '/api/server/pause/status'
      ),
  },
  backups: {
    list: () => request<BackupEntry[]>('/api/backups'),
    create: () => request<BackupEntry>('/api/backup/create', { method: 'POST' }),
    delete: (file: string) =>
      request<{ success: boolean; deleted: string }>(`/api/backup/delete?file=${encodeURIComponent(file)}`, {
        method: 'DELETE',
      }),
    getDownloadUrl: (file: string) => {
      const base = getApiBase();
      const token = getToken();
      const authParam = token ? `&token=${encodeURIComponent(token)}` : '';
      return `${base}/api/backup/download?file=${encodeURIComponent(file)}${authParam}`;
    },
  },
  economy: {
    getOverview: () => request<EconomyOverview>('/api/economy/overview'),
  },
  automation: {
    getStatus: () => request<AutomationStatus>('/api/automation/status'),
  },
  chat: {
    getHistory: () => request<import('../types/chat').ChatHistoryEntry[]>('/api/chat/history'),
    sendMessage: (message: string) =>
      request<import('../types/chat').ChatSendResponse>('/api/chat/send', {
        method: 'POST',
        body: JSON.stringify({ message }),
      }),
  },
  animals: {
    getAnimals: () => request<import('../types/animals').FarmAnimalInfo[]>('/api/farm/animals'),
  },
  console: {
    execute: (command: string) =>
      request<import('../types/console').ConsoleExecuteResponse>('/api/console/execute', {
        method: 'POST',
        body: JSON.stringify({ command }),
      }),
  },
  admin: {
    getPendingPlayers: () => request<import('../types/user').PendingPlayer[]>('/api/admin/pending-players'),
    approvePlayer: (playerId: number) =>
      request<{ success: boolean; message: string }>(`/api/admin/approve-player?playerId=${playerId}`, {
        method: 'POST',
      }),
    getUsers: () => request<import('../types/user').WebUser[]>('/api/admin/users'),
    getAvailableFarmers: () => request<import('../types/user').AvailableFarmer[]>('/api/admin/available-farmers'),
    createUser: (body: import('../types/user').CreateUserRequest) =>
      request<{ success: boolean; message: string }>('/api/admin/users/create', {
        method: 'POST',
        body: JSON.stringify(body),
      }),
    deleteUser: (farmerId: number) =>
      request<{ success: boolean; message: string }>(`/api/admin/users/delete?farmerId=${farmerId}`, {
        method: 'DELETE',
      }),
  },
  farmer: {
    getMyProfile: () => request<import('../types/character').FarmerProfile>('/api/farmer/me'),
  },
  map: {
    getTracking: () => request<import('../types/map').MapTrackingResponse>('/api/map/tracking'),
  },
  mods: {
    getInstalled: () => request<import('../types/mods').ServerInstalledMod[]>('/api/mods/installed'),
    getPolicy: () => request<import('../types/mods').ModPolicyResponse>('/api/mods/policy'),
    updatePolicy: (policy: import('../types/mods').ModPolicy) =>
      request<{ success: boolean; policy: import('../types/mods').ModPolicy }>('/api/mods/policy', {
        method: 'POST',
        body: JSON.stringify(policy),
      }),
    syncSve: () =>
      request<{ success: boolean; addedCount: number; message: string; policy: import('../types/mods').ModPolicy }>(
        '/api/mods/policy/sync-sve',
        {
          method: 'POST',
        }
      ),
    getSyncManifest: () =>
      request<{
        serverVersion: string;
        modCount: number;
        totalSizeBytes: number;
        mods: Array<{
          uniqueId: string;
          name: string;
          version: string;
          author: string;
          folderName: string;
          sizeBytes: number;
          downloadUrl: string;
        }>;
      }>('/api/mods/sync/manifest'),
    uploadMod: async (file: File) => {
      const token = getToken();
      const headers: Record<string, string> = {
        'X-File-Name': encodeURIComponent(file.name),
        'Content-Type': 'application/zip',
      };
      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }
      const base = getApiBase();
      const res = await fetch(`${base}/api/mods/upload`, {
        method: 'POST',
        headers,
        body: file,
      });
      if (!res.ok) {
        const errJson = await res.json().catch(() => ({}));
        throw new Error(errJson.error || `Upload gagal (${res.status})`);
      }
      return res.json() as Promise<{
        success: boolean;
        message: string;
        installedMods?: import('../types/mods').CustomModItem[];
      }>;
    },
    getCustomMods: () =>
      request<import('../types/mods').CustomModItem[]>('/api/mods/custom/list'),
    deleteCustomMod: (folderName: string) =>
      request<{ success: boolean; deleted: string }>(
        `/api/mods/custom/${encodeURIComponent(folderName)}`,
        { method: 'DELETE' }
      ),
  },
};
