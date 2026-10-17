import React, { useState } from 'react';
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
  Play,
  Radio
} from 'lucide-react';
import { api } from '../../lib/api';

export const ServerOverviewCard: React.FC = () => {
  const { status, players, cropsSnapshot, fetchAll } = useFarmStore();
  const [copied, setCopied] = useState(false);
  const [isActionLoading, setIsActionLoading] = useState(false);

  const handleCopyInvite = () => {
    if (status?.inviteCode) {
      navigator.clipboard.writeText(status.inviteCode);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const handleResume = async () => {
    setIsActionLoading(true);
    try {
      await api.server.resumeGame();
      await fetchAll();
    } catch (err) {
      console.error('Failed to resume:', err);
    } finally {
      setIsActionLoading(false);
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
        <div className="p-4 bg-amber-50/90 border border-amber-300 rounded-xl flex flex-col sm:flex-row sm:items-center justify-between gap-3 text-amber-900 shadow-xs">
          <div className="flex items-center space-x-3">
            <div className="w-10 h-10 rounded-lg bg-amber-200/80 flex items-center justify-center text-amber-800 shrink-0">
              <Pause className="w-5 h-5" />
            </div>
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-xs font-bold uppercase tracking-wider bg-amber-200 px-2 py-0.5 rounded text-amber-900">
                  Dunia Sedang Dijeda (Game Paused)
                </span>
                <span className="text-xs text-amber-800 font-medium">
                  oleh {status.pausedBy || 'Admin'}
                </span>
              </div>
              <p className="text-xs font-medium text-amber-800 mt-1">
                Alasan: "{status.pauseReason || 'AFK / Istirahat'}" • Jam game berhenti • Sisa batas otomatis: {Math.floor((status.pauseRemainingSeconds || 0) / 60)}m {(status.pauseRemainingSeconds || 0) % 60}s
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={handleResume}
            disabled={isActionLoading}
            className="btn-tactile btn-tactile-primary inline-flex items-center justify-center space-x-2 px-4 py-2 rounded-lg text-xs font-semibold cursor-pointer shrink-0 disabled:opacity-50 min-h-[40px]"
          >
            <Play className="w-4 h-4" />
            <span>Lanjutkan Game (Resume)</span>
          </button>
        </div>
      )}

      {/* Main Metric Cards Grid (4 Columns, Responsive to Fluid Widescreen) */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* 1. Invite Code Card */}
        <div className="card-cozy rounded-xl p-4 sm:p-5 flex flex-col justify-between">
          <div className="flex items-center justify-between">
            <span className="text-[11px] font-bold text-slate-500 uppercase tracking-wider">
              Steam & Galaxy Invite
            </span>
            <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center text-slate-700">
              <Key className="w-4 h-4" />
            </div>
          </div>

          <div className="my-3">
            {status?.inviteCode ? (
              <div className="flex items-center justify-between bg-slate-50 border border-slate-200/90 px-3 py-2 rounded-lg font-mono text-base font-bold text-slate-900">
                <span className="tracking-wide select-all">{status.inviteCode}</span>
                <button
                  type="button"
                  onClick={handleCopyInvite}
                  className="text-slate-400 hover:text-slate-800 transition-colors cursor-pointer p-1.5 rounded hover:bg-slate-200/50"
                  title="Salin kode invite"
                  aria-label="Salin kode invite"
                >
                  {copied ? (
                    <Check className="w-4 h-4 text-emerald-600" />
                  ) : (
                    <Copy className="w-4 h-4" />
                  )}
                </button>
              </div>
            ) : (
              <div className="text-xs font-medium text-slate-400 py-2">
                Menunggu inisialisasi lobby...
              </div>
            )}
          </div>

          <div className="text-[11px] text-slate-500 flex items-center justify-between pt-2 border-t border-slate-100">
            <span className="flex items-center space-x-1.5">
              <Radio className="w-3 h-3 text-slate-400" />
              <span>Relay: {status?.steamRelayReady ? 'Aktif' : 'Menunggu'}</span>
            </span>
            <span className="font-mono text-[10px] text-slate-400 uppercase">
              {status?.connectionStatusCode ?? 'Unknown'}
            </span>
          </div>
        </div>

        {/* 2. Connected Farmers Card */}
        <div className="card-cozy rounded-xl p-4 sm:p-5 flex flex-col justify-between">
          <div className="flex items-center justify-between">
            <span className="text-[11px] font-bold text-slate-500 uppercase tracking-wider">
              Petani Terhubung
            </span>
            <div className="w-8 h-8 rounded-lg bg-indigo-50 flex items-center justify-center text-indigo-600">
              <Users className="w-4 h-4" />
            </div>
          </div>

          <div className="my-3 flex items-baseline space-x-2">
            <div className="text-3xl font-bold text-slate-900 tracking-tight font-mono tabular-nums">
              {players.length}
            </div>
            <span className="text-xs text-slate-500 font-medium">petani aktif</span>
          </div>

          <div className="text-[11px] text-slate-500 truncate pt-2 border-t border-slate-100">
            {players.length === 0 ? (
              <span className="text-slate-400 italic">Tidak ada petani in-game</span>
            ) : (
              <span className="font-medium text-slate-700">
                {players.map((p) => p.name).join(', ')}
              </span>
            )}
          </div>
        </div>

        {/* 3. Ready to Harvest Card */}
        <div className="card-cozy rounded-xl p-4 sm:p-5 flex flex-col justify-between">
          <div className="flex items-center justify-between">
            <span className="text-[11px] font-bold text-slate-500 uppercase tracking-wider">
              Siap Panen (Harvest)
            </span>
            <div className="w-8 h-8 rounded-lg bg-emerald-50 flex items-center justify-center text-emerald-600">
              <Sparkles className="w-4 h-4" />
            </div>
          </div>

          <div className="my-3 flex items-baseline space-x-2">
            <div className="text-3xl font-bold text-emerald-600 tracking-tight font-mono tabular-nums">
              {readyCrops}
            </div>
            <span className="text-xs text-slate-500 font-medium">
              dari {totalCrops} tanaman
            </span>
          </div>

          <div className="text-[11px] text-slate-500 flex items-center justify-between pt-2 border-t border-slate-100">
            <span>Tanaman lahan terbuka</span>
            {readyCrops > 0 ? (
              <span className="text-emerald-700 font-bold bg-emerald-50 px-1.5 py-0.5 rounded text-[10px]">
                Siap Dipetik
              </span>
            ) : (
              <span className="text-slate-400">Sedang Tumbuh</span>
            )}
          </div>
        </div>

        {/* 4. Irrigation & Field Health Card */}
        <div className="card-cozy rounded-xl p-4 sm:p-5 flex flex-col justify-between">
          <div className="flex items-center justify-between">
            <span className="text-[11px] font-bold text-slate-500 uppercase tracking-wider">
              Penyiraman Lahan
            </span>
            <div className="w-8 h-8 rounded-lg bg-sky-50 flex items-center justify-center text-sky-600">
              <Droplets className="w-4 h-4" />
            </div>
          </div>

          <div className="my-3">
            <div className="flex items-center justify-between text-xs font-semibold mb-1.5">
              <span className="text-sky-700 font-mono tabular-nums">{wateredCrops} tersiram</span>
              <span className="text-slate-600 font-mono tabular-nums">{waterPercent}%</span>
            </div>
            <div className="w-full bg-slate-100 rounded-full h-2 overflow-hidden">
              <div
                className="bg-sky-500 h-2 rounded-full transition-all duration-500"
                style={{ width: `${waterPercent}%` }}
              />
            </div>
          </div>

          <div className="text-[11px] text-slate-500 flex items-center justify-between pt-2 border-t border-slate-100">
            <span className={unwateredCrops > 0 ? 'text-amber-600 font-medium' : 'text-slate-400'}>
              {unwateredCrops} kering
            </span>
            {deadCrops > 0 && (
              <span className="text-rose-600 font-medium flex items-center space-x-1">
                <AlertTriangle className="w-3 h-3" />
                <span>{deadCrops} mati</span>
              </span>
            )}
            <span className="flex items-center space-x-1 font-mono text-[10px] text-slate-400">
              <Activity className="w-3 h-3" />
              <span>{status?.tps?.toFixed(1) ?? '60.0'} TPS</span>
            </span>
          </div>
        </div>
      </div>
    </div>
  );
};

export default ServerOverviewCard;
