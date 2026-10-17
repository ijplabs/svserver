import React from 'react';
import { useFarmStore } from '../../store/farmStore';
import { 
  Users, 
  Droplets, 
  Sparkles, 
  Activity, 
  Key, 
  Copy, 
  Check, 
  AlertTriangle,
  Pause,
  Play
} from 'lucide-react';
import { api } from '../../lib/api';

export const ServerOverviewCard: React.FC = () => {
  const { status, players, cropsSnapshot, fetchAll } = useFarmStore();
  const [copied, setCopied] = React.useState(false);

  const handleCopyInvite = () => {
    if (status?.inviteCode) {
      navigator.clipboard.writeText(status.inviteCode);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const handleResume = async () => {
    try {
      await api.server.resumeGame();
      await fetchAll();
    } catch (err) {
      console.error('Failed to resume:', err);
    }
  };

  const totalCrops = cropsSnapshot?.totalCrops ?? 0;
  const readyCrops = cropsSnapshot?.readyCount ?? 0;
  const wateredCrops = cropsSnapshot?.wateredCount ?? 0;
  const unwateredCrops = cropsSnapshot?.unwateredCount ?? 0;
  const deadCrops = cropsSnapshot?.deadCount ?? 0;

  const waterPercent = totalCrops > 0 ? Math.round((wateredCrops / totalCrops) * 100) : 0;

  return (
    <div className="space-y-4">
      {/* Active Manual Pause Alert Banner */}
      {status?.isManuallyPaused && (
        <div className="p-4 bg-amber-50 border border-amber-200 rounded-xl flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-amber-900 shadow-xs">
          <div className="flex items-center space-x-3">
            <div className="w-9 h-9 rounded-lg bg-amber-100 flex items-center justify-center text-amber-700 shrink-0">
              <Pause className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center space-x-2">
                <span className="text-xs font-bold uppercase tracking-wider bg-amber-200/80 px-2 py-0.5 rounded text-amber-900">
                  Dunia Sedang Dijeda (Paused)
                </span>
                <span className="text-xs text-amber-700">
                  oleh {status.pausedBy || 'Admin'}
                </span>
              </div>
              <p className="text-xs font-medium text-amber-800 mt-0.5">
                Alasan: "{status.pauseReason || 'AFK / Istirahat'}" • Jam game berhenti • Sisa batas aman: {Math.floor((status.pauseRemainingSeconds || 0) / 60)}m {(status.pauseRemainingSeconds || 0) % 60}s
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={handleResume}
            className="inline-flex items-center justify-center space-x-1.5 px-3 py-1.5 bg-amber-600 hover:bg-amber-700 text-white rounded-lg text-xs font-semibold transition-colors cursor-pointer shrink-0"
          >
            <Play className="w-3.5 h-3.5" />
            <span>Lanjutkan Game (Resume)</span>
          </button>
        </div>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
      {/* 1. Invite Code Card */}
      <div className="bg-white border border-slate-200 rounded-xl p-4 shadow-xs flex flex-col justify-between">
        <div className="flex items-center justify-between">
          <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider">
            Invite Code
          </span>
          <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center text-slate-700">
            <Key className="w-4 h-4" />
          </div>
        </div>
        <div className="my-2">
          {status?.inviteCode ? (
            <div className="flex items-center justify-between bg-slate-50 border border-slate-200 px-3 py-2 rounded-lg font-mono text-base font-bold text-slate-900">
              <span>{status.inviteCode}</span>
              <button
                type="button"
                onClick={handleCopyInvite}
                className="text-slate-400 hover:text-slate-700 transition-colors cursor-pointer p-1"
                title="Copy code"
              >
                {copied ? (
                  <Check className="w-4 h-4 text-emerald-600" />
                ) : (
                  <Copy className="w-4 h-4" />
                )}
              </button>
            </div>
          ) : (
            <div className="text-sm font-medium text-slate-400 py-2">
              Lobby initializing...
            </div>
          )}
        </div>
        <div className="text-[11px] text-slate-500 flex items-center justify-between">
          <span>Relay: {status?.steamRelayReady ? 'Active' : 'Pending'}</span>
          <span>Status: {status?.connectionStatusCode ?? 'Unknown'}</span>
        </div>
      </div>

      {/* 2. Connected Farmers */}
      <div className="bg-white border border-slate-200 rounded-xl p-4 shadow-xs flex flex-col justify-between">
        <div className="flex items-center justify-between">
          <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider">
            Connected Farmers
          </span>
          <div className="w-8 h-8 rounded-lg bg-indigo-50 flex items-center justify-center text-indigo-600">
            <Users className="w-4 h-4" />
          </div>
        </div>
        <div className="my-2">
          <div className="text-2xl font-bold text-slate-900 tracking-tight">
            {players.length}
          </div>
        </div>
        <div className="text-[11px] text-slate-500 flex items-center justify-between">
          <span>
            {players.length === 0
              ? 'No players online'
              : players.map((p) => p.name).join(', ')}
          </span>
        </div>
      </div>

      {/* 3. Ready to Harvest */}
      <div className="bg-white border border-slate-200 rounded-xl p-4 shadow-xs flex flex-col justify-between">
        <div className="flex items-center justify-between">
          <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider">
            Harvest Ready
          </span>
          <div className="w-8 h-8 rounded-lg bg-emerald-50 flex items-center justify-center text-emerald-600">
            <Sparkles className="w-4 h-4" />
          </div>
        </div>
        <div className="my-2 flex items-baseline space-x-2">
          <div className="text-2xl font-bold text-emerald-600 tracking-tight">
            {readyCrops}
          </div>
          <span className="text-xs text-slate-500 font-medium">
            of {totalCrops} crops
          </span>
        </div>
        <div className="text-[11px] text-slate-500 flex items-center justify-between">
          <span>Planted field crops</span>
          {readyCrops > 0 && (
            <span className="text-emerald-700 font-semibold">Ready today</span>
          )}
        </div>
      </div>

      {/* 4. Irrigation & Field Health */}
      <div className="bg-white border border-slate-200 rounded-xl p-4 shadow-xs flex flex-col justify-between">
        <div className="flex items-center justify-between">
          <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider">
            Field Watering
          </span>
          <div className="w-8 h-8 rounded-lg bg-sky-50 flex items-center justify-center text-sky-600">
            <Droplets className="w-4 h-4" />
          </div>
        </div>
        <div className="my-2">
          <div className="flex items-center justify-between text-xs font-semibold mb-1">
            <span className="text-sky-700">{wateredCrops} watered</span>
            <span className="text-slate-500">{waterPercent}%</span>
          </div>
          <div className="w-full bg-slate-100 rounded-full h-2 overflow-hidden">
            <div
              className="bg-sky-500 h-2 rounded-full transition-all duration-500"
              style={{ width: `${waterPercent}%` }}
            />
          </div>
        </div>
        <div className="text-[11px] text-slate-500 flex items-center justify-between">
          <span className={unwateredCrops > 0 ? 'text-amber-600 font-medium' : ''}>
            {unwateredCrops} dry
          </span>
          {deadCrops > 0 && (
            <span className="text-rose-600 font-medium flex items-center space-x-1">
              <AlertTriangle className="w-3 h-3" />
              <span>{deadCrops} dead</span>
            </span>
          )}
          <span className="flex items-center space-x-1">
            <Activity className="w-3 h-3 text-slate-400" />
            <span>{status?.tps?.toFixed(1) ?? '60.0'} TPS</span>
          </span>
        </div>
      </div>
    </div>
    </div>
  );
};
