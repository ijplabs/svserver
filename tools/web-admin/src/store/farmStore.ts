import { create } from 'zustand';
import { ServerStatus, PlayerInfo, FarmCropsSnapshot, CabinInfo } from '../types/farm';
import { api } from '../lib/api';

interface FarmState {
  status: ServerStatus | null;
  players: PlayerInfo[];
  cropsSnapshot: FarmCropsSnapshot | null;
  cabins: CabinInfo[];
  isLoading: boolean;
  lastUpdated: Date | null;
  error: string | null;
  pollTimer: number | null;

  fetchStatus: () => Promise<void>;
  fetchPlayers: () => Promise<void>;
  fetchCrops: () => Promise<void>;
  fetchCabins: () => Promise<void>;
  fetchAll: () => Promise<void>;
  startPolling: (intervalMs?: number) => void;
  stopPolling: () => void;
}

export const useFarmStore = create<FarmState>((set, get) => ({
  status: null,
  players: [],
  cropsSnapshot: null,
  cabins: [],
  isLoading: false,
  lastUpdated: null,
  error: null,
  pollTimer: null,

  fetchStatus: async () => {
    try {
      const status = await api.server.getStatus();
      set({ status, error: null });
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to fetch status';
      set({ error: msg });
    }
  },

  fetchPlayers: async () => {
    try {
      const res = await api.server.getPlayers();
      set({ players: res.players || [] });
    } catch {
      // Best-effort
    }
  },

  fetchCrops: async () => {
    try {
      const res = await api.server.getCrops();
      set({ cropsSnapshot: res });
    } catch {
      // Best-effort
    }
  },

  fetchCabins: async () => {
    try {
      const res = await api.server.getCabins();
      set({ cabins: res.cabins || [] });
    } catch {
      // Best-effort
    }
  },

  fetchAll: async () => {
    set({ isLoading: true });
    await Promise.allSettled([
      get().fetchStatus(),
      get().fetchPlayers(),
      get().fetchCrops(),
      get().fetchCabins(),
    ]);
    set({ isLoading: false, lastUpdated: new Date() });
  },

  startPolling: (intervalMs = 4000) => {
    get().stopPolling();
    get().fetchAll();
    const timer = window.setInterval(() => {
      get().fetchAll();
    }, intervalMs);
    set({ pollTimer: timer });
  },

  stopPolling: () => {
    const { pollTimer } = get();
    if (pollTimer) {
      clearInterval(pollTimer);
      set({ pollTimer: null });
    }
  },
}));
