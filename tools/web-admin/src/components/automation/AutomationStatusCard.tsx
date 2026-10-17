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
      <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100">
          <div className="flex items-center space-x-2.5">
            <div className="p-2 bg-indigo-50 border border-indigo-200 text-indigo-700 rounded-lg">
              <BedDouble className="w-4 h-4" />
            </div>
            <div>
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-900">
                Pemungutan Suara Tidur (!votesleep)
              </h3>
              <span className="text-[11px] text-slate-500">
                Ambang Batas: {sleepVote?.thresholdPercent ?? 50}% Pemain Aktif
              </span>
            </div>
          </div>

          <span
            className={`px-2 py-0.5 rounded-full text-[11px] font-semibold border ${
              sleepVote?.enabled
                ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                : 'bg-slate-100 text-slate-500 border-slate-200'
            }`}
          >
            {sleepVote?.enabled ? 'Aktif' : 'Nonaktif'}
          </span>
        </div>

        <div className="mt-4 space-y-3">
          <div className="flex items-center justify-between text-xs">
            <span className="text-slate-600">Suara Terkumpul:</span>
            <span className="font-semibold text-slate-900">
              {sleepVote?.activeVotes ?? 0} dari {sleepVote?.requiredVotes ?? 0} suara (
              {sleepVote?.eligiblePlayers ?? 0} pemain berhak)
            </span>
          </div>

          {/* Progress bar */}
          <div className="w-full bg-slate-100 rounded-full h-2 overflow-hidden border border-slate-200">
            <div
              className={`h-full transition-all duration-500 ${
                sleepVote?.votePassed ? 'bg-emerald-500' : 'bg-indigo-500'
              }`}
              style={{ width: `${Math.min(100, votePercent)}%` }}
            />
          </div>

          <div className="flex items-center justify-between text-[11px] text-slate-500 pt-1">
            <span>Status Waktu: {sleepVote?.isNight ? 'Malam Hari (>= 18:00)' : 'Siang Hari'}</span>
            {sleepVote?.votePassed && (
              <span className="font-semibold text-emerald-600 flex items-center space-x-1">
                <CheckCircle2 className="w-3.5 h-3.5" />
                <span>Threshold Tercapai</span>
              </span>
            )}
          </div>
        </div>
      </div>

      {/* AFK Watchdog Card */}
      <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100">
          <div className="flex items-center space-x-2.5">
            <div className="p-2 bg-rose-50 border border-rose-200 text-rose-700 rounded-lg">
              <ShieldAlert className="w-4 h-4" />
            </div>
            <div>
              <h3 className="text-xs font-bold uppercase tracking-wider text-slate-900">
                Sistem Deteksi AFK (Watchdog)
              </h3>
              <span className="text-[11px] text-slate-500">
                Peringatan: {afk?.warningMinutes ?? 8}m • Kick Otomatis: {afk?.timeoutMinutes ?? 10}m
              </span>
            </div>
          </div>

          <span
            className={`px-2 py-0.5 rounded-full text-[11px] font-semibold border ${
              afk?.enabled
                ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                : 'bg-slate-100 text-slate-500 border-slate-200'
            }`}
          >
            {afk?.enabled ? 'Aktif' : 'Nonaktif'}
          </span>
        </div>

        <div className="mt-4">
          {!afk?.trackedPlayers || afk.trackedPlayers.length === 0 ? (
            <div className="py-2.5 text-center text-slate-500 text-xs flex items-center justify-center space-x-2">
              <CheckCircle2 className="w-4 h-4 text-emerald-500" />
              <span>Semua pemain online terdeteksi aktif bergerak.</span>
            </div>
          ) : (
            <div className="space-y-2">
              {afk.trackedPlayers.map((p) => (
                <div
                  key={p.farmerId}
                  className="flex items-center justify-between p-2 bg-slate-50 border border-slate-200 rounded-lg text-xs"
                >
                  <div className="flex items-center space-x-2">
                    <UserX className="w-3.5 h-3.5 text-slate-400" />
                    <span className="font-semibold text-slate-800">{p.farmerName}</span>
                    <span className="text-[11px] text-slate-500 font-mono">({p.location})</span>
                  </div>
                  <div className="flex items-center space-x-2">
                    <span className="text-slate-600">Idle: {p.idleMinutes}m</span>
                    {p.warningSent && (
                      <span className="px-1.5 py-0.5 rounded text-[10px] font-semibold bg-rose-100 text-rose-700">
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
