import React, { useState, useEffect, useCallback } from 'react';
import { api } from '../../lib/api';
import { AutomationStatus } from '../../types/automation';
import { 
  ShieldAlert, 
  CheckCircle2, 
  UserX,
  BedDouble
} from 'lucide-react';

export const AutomationStatusCard: React.FC = () => {
  const [data, setData] = useState<AutomationStatus | null>(null);

  const fetchStatus = useCallback(async () => {
    try {
      const res = await api.automation.getStatus();
      setData(res);
    } catch {
      // Ignored if server is still starting
    }
  }, []);

  useEffect(() => {
    fetchStatus();
    const interval = setInterval(fetchStatus, 5000);
    return () => clearInterval(interval);
  }, [fetchStatus]);

  const sleepVote = data?.sleepVote;
  const afk = data?.afkWatchdog;

  const votePercent =
    sleepVote && sleepVote.eligiblePlayers > 0
      ? Math.round((sleepVote.activeVotes / Math.max(1, sleepVote.requiredVotes)) * 100)
      : 0;

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
      {/* Sleep Voting Card */}
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl p-5 shadow-xs">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-[#1e2a42]">
          <div className="flex items-center space-x-2.5">
            <div className="p-2 bg-indigo-50 dark:bg-indigo-950/60 border border-indigo-200 dark:border-indigo-800/50 text-indigo-700 dark:text-indigo-300 rounded-lg">
              <BedDouble className="w-4 h-4" />
            </div>
            <div>
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-900 dark:text-white">
                Pemungutan Suara Tidur (!votesleep)
              </h3>
              <span className="text-[11px] text-slate-500 dark:text-slate-400">
                Ambang Batas: {sleepVote?.thresholdPercent ?? 50}% Pemain Aktif
              </span>
            </div>
          </div>

          <span
            className={`px-2 py-0.5 rounded-full text-[11px] font-semibold border ${
              sleepVote?.enabled
                ? 'bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800/50'
                : 'bg-slate-100 dark:bg-[#0a0f1d] text-slate-500 dark:text-slate-400 border-slate-200 dark:border-[#1e2a42]'
            }`}
          >
            {sleepVote?.enabled ? 'Aktif' : 'Nonaktif'}
          </span>
        </div>

        <div className="mt-4 space-y-3">
          <div className="flex items-center justify-between text-xs">
            <span className="text-slate-600 dark:text-slate-400">Suara Terkumpul:</span>
            <span className="font-semibold text-slate-900 dark:text-slate-200 font-mono">
              {sleepVote?.activeVotes ?? 0} dari {sleepVote?.requiredVotes ?? 0} suara (
              {sleepVote?.eligiblePlayers ?? 0} pemain berhak)
            </span>
          </div>

          {/* Progress bar */}
          <div className="w-full bg-slate-100 dark:bg-[#0a0f1d] rounded-full h-2 overflow-hidden border border-slate-200 dark:border-[#1e2a42]">
            <div
              className={`h-full transition-all duration-500 ${
                sleepVote?.votePassed ? 'bg-emerald-500' : 'bg-indigo-500'
              }`}
              style={{ width: `${Math.min(100, votePercent)}%` }}
            />
          </div>

          <div className="flex items-center justify-between text-[11px] text-slate-500 dark:text-slate-400 pt-1">
            <span>Status Waktu: {sleepVote?.isNight ? 'Malam Hari (>= 18:00)' : 'Siang Hari'}</span>
            {sleepVote?.votePassed && (
              <span className="font-semibold text-emerald-600 dark:text-emerald-400 flex items-center space-x-1">
                <CheckCircle2 className="w-3.5 h-3.5" />
                <span>Threshold Tercapai</span>
              </span>
            )}
          </div>
        </div>
      </div>

      {/* AFK Watchdog Card */}
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl p-5 shadow-xs">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-[#1e2a42]">
          <div className="flex items-center space-x-2.5">
            <div className="p-2 bg-rose-50 dark:bg-rose-950/60 border border-rose-200 dark:border-rose-800/50 text-rose-700 dark:text-rose-300 rounded-lg">
              <ShieldAlert className="w-4 h-4" />
            </div>
            <div>
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-900 dark:text-white">
                Sistem Deteksi AFK (Watchdog)
              </h3>
              <span className="text-[11px] text-slate-500 dark:text-slate-400">
                Peringatan: {afk?.warningMinutes ?? 8}m • Kick Otomatis: {afk?.timeoutMinutes ?? 10}m
              </span>
            </div>
          </div>

          <span
            className={`px-2 py-0.5 rounded-full text-[11px] font-semibold border ${
              afk?.enabled
                ? 'bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800/50'
                : 'bg-slate-100 dark:bg-[#0a0f1d] text-slate-500 dark:text-slate-400 border-slate-200 dark:border-[#1e2a42]'
            }`}
          >
            {afk?.enabled ? 'Aktif' : 'Nonaktif'}
          </span>
        </div>

        <div className="mt-4">
          {!afk?.trackedPlayers || afk.trackedPlayers.length === 0 ? (
            <div className="py-2.5 text-center text-slate-500 dark:text-slate-400 text-xs flex items-center justify-center space-x-2">
              <CheckCircle2 className="w-4 h-4 text-emerald-500" />
              <span>Semua pemain online terdeteksi aktif bergerak.</span>
            </div>
          ) : (
            <div className="space-y-2">
              {afk.trackedPlayers.map((p) => (
                <div
                  key={p.farmerId}
                  className="flex items-center justify-between p-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-xs"
                >
                  <div className="flex items-center space-x-2">
                    <UserX className="w-3.5 h-3.5 text-slate-400" />
                    <span className="font-semibold text-slate-800 dark:text-slate-200">{p.farmerName}</span>
                    <span className="text-[11px] text-slate-500 dark:text-slate-400 font-mono">({p.location})</span>
                  </div>
                  <div className="flex items-center space-x-2">
                    <span className="text-slate-600 dark:text-slate-400 font-mono">Idle: {p.idleMinutes}m</span>
                    {p.warningSent && (
                      <span className="px-1.5 py-0.5 rounded text-[10px] font-semibold bg-rose-100 dark:bg-rose-950/60 text-rose-700 dark:text-rose-300 border border-rose-200 dark:border-rose-800/50">
                        Peringatan Terkirim
                      </span>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default AutomationStatusCard;
