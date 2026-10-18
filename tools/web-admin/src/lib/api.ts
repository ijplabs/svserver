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
  const data = text ? (JSON.parse(text) as T) : ({} as T);
  if (data && typeof data === 'object' && 'error' in data && (data as any).error) {
    throw new Error(String((data as any).error));
  }
  return data;
}

export interface UploadProgressData {
  loadedBytes: number;
  totalBytes: number;
  percentage: number;
  currentChunk?: number;
  totalChunks?: number;
  stage: 'uploading' | 'assembling' | 'complete';
  statusText: string;
}

function uploadChunkWithRetry(
  chunkBlob: Blob,
  url: string,
  token: string,
  onProgress?: (loaded: number) => void,
  retries = 3
): Promise<any> {
  return new Promise((resolve, reject) => {
    const attempt = (remainingRetries: number) => {
      const xhr = new XMLHttpRequest();
      xhr.open('POST', url);
      xhr.setRequestHeader('Content-Type', 'application/octet-stream');
      xhr.setRequestHeader('Authorization', `Bearer ${token}`);

      if (xhr.upload && onProgress) {
        xhr.upload.onprogress = (e) => {
          if (e.lengthComputable) {
            onProgress(e.loaded);
          }
        };
      }

      xhr.onload = () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          try {
            const parsed = JSON.parse(xhr.responseText);
            if (parsed && typeof parsed === 'object' && parsed.error) {
              reject(new Error(parsed.error));
              return;
            }
            resolve(parsed);
          } catch {
            resolve({ success: true });
          }
        } else if (xhr.status === 404) {
          reject(new Error('HTTP 404'));
        } else if (xhr.status === 401 || xhr.status === 403) {
          let authErr = `Akses ditolak (HTTP ${xhr.status}). Silakan login kembali sebagai Administrator.`;
          try {
            const errObj = JSON.parse(xhr.responseText);
            if (errObj?.error) authErr = errObj.error;
          } catch {}
          reject(new Error(authErr));
        } else {
          let errMsg = `Upload bagian gagal (HTTP ${xhr.status})`;
          try {
            const errObj = JSON.parse(xhr.responseText);
            if (errObj?.error) errMsg = errObj.error;
          } catch {}

          if (remainingRetries > 0 && xhr.status >= 500) {
            setTimeout(() => attempt(remainingRetries - 1), 1500);
          } else {
            reject(new Error(errMsg));
          }
        }
      };

      xhr.onerror = () => {
        if (remainingRetries > 0) {
          setTimeout(() => attempt(remainingRetries - 1), 1500);
        } else {
          reject(new Error('Koneksi terputus saat mengunggah bagian mod.'));
        }
      };

      xhr.ontimeout = () => {
        if (remainingRetries > 0) {
          setTimeout(() => attempt(remainingRetries - 1), 1500);
        } else {
          reject(new Error('Waktu upload habis (timeout).'));
        }
      };

      xhr.timeout = 45000;
      xhr.send(chunkBlob);
    };

    attempt(retries);
  });
}

function uploadSingleFileXHR(
  file: File,
  base: string,
  token: string,
  onProgress?: (data: UploadProgressData) => void
): Promise<{ success: boolean; message: string; installedMods?: import('../types/mods').CustomModItem[] }> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    const url = `${base}/api/mods/upload?filename=${encodeURIComponent(file.name)}`;
    xhr.open('POST', url);
    xhr.setRequestHeader('Content-Type', 'application/zip');
    xhr.setRequestHeader('Authorization', `Bearer ${token}`);

    if (xhr.upload && onProgress) {
      xhr.upload.onprogress = (e) => {
        if (e.lengthComputable) {
          const pct = Math.min(99, Math.round((e.loaded / e.total) * 100));
          onProgress({
            loadedBytes: e.loaded,
            totalBytes: e.total,
            percentage: pct,
            stage: e.loaded === e.total ? 'assembling' : 'uploading',
            statusText: e.loaded === e.total
              ? 'File terunggah. Server sedang mengekstrak mod...'
              : `Mengunggah (${(e.loaded / (1024 * 1024)).toFixed(1)} MB / ${(e.total / (1024 * 1024)).toFixed(1)} MB - ${pct}%)...`
          });
        }
      };
    }

    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        try {
          const res = JSON.parse(xhr.responseText);
          onProgress?.({
            loadedBytes: file.size,
            totalBytes: file.size,
            percentage: 100,
            stage: 'complete',
            statusText: 'Selesai! Mod berhasil dipasang di server.'
          });
          resolve(res);
        } catch {
          resolve({ success: true, message: 'Mod berhasil diunggah' });
        }
      } else {
        let errMsg = `Upload gagal (HTTP ${xhr.status})`;
        try {
          const errObj = JSON.parse(xhr.responseText);
          if (errObj?.error) errMsg = errObj.error;
          else if (errObj?.message) errMsg = errObj.message;
        } catch {}
        reject(new Error(errMsg));
      }
    };

    xhr.onerror = () => {
      reject(new Error('Koneksi terputus saat mengunggah mod (Network Error).'));
    };

    xhr.ontimeout = () => {
      reject(new Error('Waktu upload habis (timeout).'));
    };

    xhr.timeout = 300000;
    xhr.send(file);
  });
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
    approvePlayer: (playerId: string | number) =>
      request<{ success: boolean; message: string }>(`/api/admin/approve-player?playerId=${encodeURIComponent(String(playerId))}`, {
        method: 'POST',
      }),
    getUsers: () => request<import('../types/user').WebUser[]>('/api/admin/users'),
    getAvailableFarmers: () => request<import('../types/user').AvailableFarmer[]>('/api/admin/available-farmers'),
    createUser: (body: import('../types/user').CreateUserRequest) =>
      request<{ success: boolean; message: string }>('/api/admin/users/create', {
        method: 'POST',
        body: JSON.stringify(body),
      }),
    deleteUser: (farmerId: string | number) =>
      request<{ success: boolean; message: string }>(`/api/admin/users/delete?farmerId=${encodeURIComponent(String(farmerId))}`, {
        method: 'DELETE',
      }),
  },
  farmer: {
    getMyProfile: () => request<import('../types/character').FarmerProfile>('/api/farmer/me'),
    getSocial: (farmerId?: number) =>
      request<import('../types/social').FarmerSocialResponse>(
        farmerId ? `/api/farmer/social?farmerId=${farmerId}` : '/api/farmer/social'
      ),
    getQuests: (farmerId?: number) =>
      request<import('../types/quests').FarmerQuestsResponse>(
        farmerId ? `/api/farmer/quests?farmerId=${farmerId}` : '/api/farmer/quests'
      ),
    getCommunityCenter: () =>
      request<import('../types/quests').CommunityCenterResponse>('/api/farmer/community-center'),
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
    uploadMod: async (
      file: File,
      onProgress?: (data: UploadProgressData) => void
    ) => {
      const token = getToken();
      if (!token) {
        throw new Error('Sesi login telah berakhir. Silakan login kembali sebagai Administrator/Sysadmin.');
      }

      const base = getApiBase();
      const CHUNK_SIZE = 2 * 1024 * 1024; // 2 MB per chunk for fast, steady tunnel transfer
      const totalBytes = file.size;

      // Small file (< 2 MB): upload directly with real progress
      if (totalBytes < CHUNK_SIZE) {
        return uploadSingleFileXHR(file, base, token, onProgress);
      }

      // Large file (>= 2 MB): Chunked Upload with resilient retry and byte-accurate progress
      const totalChunks = Math.ceil(totalBytes / CHUNK_SIZE);
      const uploadId = `up_${Date.now()}_${Math.random().toString(36).substring(2, 8)}`;

      try {
        let finalResult: any = null;
        for (let i = 0; i < totalChunks; i++) {
          const start = i * CHUNK_SIZE;
          const end = Math.min(start + CHUNK_SIZE, totalBytes);
          const chunkBlob = file.slice(start, end);
          const isLast = i === totalChunks - 1;

          const chunkUrl = `${base}/api/mods/upload/chunk?uploadId=${encodeURIComponent(uploadId)}&chunkIndex=${i}&totalChunks=${totalChunks}&filename=${encodeURIComponent(file.name)}&totalSize=${totalBytes}&token=${encodeURIComponent(token)}`;

          const res = await uploadChunkWithRetry(chunkBlob, chunkUrl, token, (chunkLoaded) => {
            const currentLoaded = start + chunkLoaded;
            const pct = Math.min(99, Math.round((currentLoaded / totalBytes) * 100));
            onProgress?.({
              loadedBytes: currentLoaded,
              totalBytes,
              percentage: pct,
              currentChunk: i + 1,
              totalChunks,
              stage: isLast && chunkLoaded === chunkBlob.size ? 'assembling' : 'uploading',
              statusText: isLast && chunkLoaded === chunkBlob.size
                ? 'Semua bagian terunggah. Server sedang menggabungkan & mengekstrak mod...'
                : `Mengunggah bagian ${i + 1} dari ${totalChunks} (${(currentLoaded / (1024 * 1024)).toFixed(1)} MB / ${(totalBytes / (1024 * 1024)).toFixed(1)} MB - ${pct}%)...`
            });
          });

          if (isLast) {
            finalResult = res;
          }
        }

        onProgress?.({
          loadedBytes: totalBytes,
          totalBytes,
          percentage: 100,
          currentChunk: totalChunks,
          totalChunks,
          stage: 'complete',
          statusText: 'Selesai! Mod berhasil dipasang di server.'
        });

        return finalResult;
      } catch (err: any) {
        // Fallback to single XHR if chunk endpoint returns 404 (prior server container)
        if (err.message && err.message.includes('404')) {
          return uploadSingleFileXHR(file, base, token, onProgress);
        }
        throw err;
      }
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
