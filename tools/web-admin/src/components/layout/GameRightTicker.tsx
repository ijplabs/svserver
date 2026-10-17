import React, { useState } from 'react';
import { useFarmStore } from '../../store/farmStore';
import { 
  Users, 
  MapPin, 
  Sprout, 
  Key, 
  Copy, 
  Check, 
  Activity, 
  Radio,
  Clock,
  Sparkles,
  Droplets
} from 'lucide-react';

export const GameRightTicker: React.FC = () => {
  const { status, players, cropsSnapshot } = useFarmStore();
  const [copied, setCopied] = useState(false);

  const handleCopyInvite = () => {
    if (status?.inviteCode) {
      navigator.clipboard.writeText(status.inviteCode);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const totalCrops = cropsSnapshot?.totalCrops ?? 0;
  const readyCrops = cropsSnapshot?.readyCount ?? 0;
  const wateredCrops = cropsSnapshot?.wateredCount ?? 0;
  const waterPercent = totalCrops > 0 ? Math.round((wateredCrops / totalCrops) * 100) : 0;

  return (
    <aside className="w-72 bg-[#0c1220] border-l border-[#1e2a42] p-4 text-slate-300 hidden 2xl:flex flex-col gap-5 overflow-y-auto select-none shrink-0 h-[calc(100vh-4rem)] sticky top-16">
      {/* 1. STEAM / GALAXY INVITE TACTICAL BOX */}
      <div className="bg-[#11192b] border border-amber-500/30 rounded-xl p-3.5 space-y-2">
        <div className="flex items-center justify-between text-xs text-amber-300 font-bold uppercase tracking-wider">
          <span className="flex items-center gap-1.5">
            <Key className="w-3.5 h-3.5 text-amber-400" />
            <span>Invite Code</span>
          </span>
          <span className="text-[10px] text-slate-400 font-mono">
            {status?.steamRelayReady ? 'Relay OK' : 'Pending'}
          </span>
        </div>

        {status?.inviteCode ? (
          <div className="flex items-center justify-between bg-[#0a0f1d] border border-[#223354] px-2.5 py-1.5 rounded-lg font-mono text-sm font-bold text-amber-200">
            <span className="tracking-wider">{status.inviteCode}</span>
            <button
              type="button"
              onClick={handleCopyInvite}
              className="p-1 hover:text-white transition text-slate-400 cursor-pointer"
              title="Salin kode"
            >
              {copied ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
            </button>
          </div>
        ) : (
          <div className="text-xs text-slate-500 italic py-1">Lobby initializing...</div>
        )}
      </div>

      {/* 2. ONLINE FARMERS LIVE ROSTER */}
      <div className="space-y-2.5">
        <div className="flex items-center justify-between text-xs font-bold text-slate-400 uppercase tracking-wider">
          <span className="flex items-center gap-1.5">
            <Users className="w-3.5 h-3.5 text-indigo-400" />
            <span>Petani Aktif ({players.length})</span>
          </span>
          <span className="flex h-2 w-2 relative">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75" />
            <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-500" />
          </span>
        </div>

        {players.length === 0 ? (
          <div className="p-4 bg-[#11192b]/60 border border-[#1e2a42] rounded-xl text-center text-xs text-slate-500">
            <Clock className="w-5 h-5 mx-auto mb-1 text-slate-600" />
            <span>Semua petani sedang offline</span>
          </div>
        ) : (
          <div className="space-y-2">
            {players.map((p) => (
              <div
                key={p.id}
                className="flex items-center justify-between p-2.5 bg-[#11192b] border border-[#1e2a42] hover:border-slate-700 rounded-xl transition text-xs"
              >
                <div className="flex items-center gap-2.5 truncate">
                  <div className="w-7 h-7 rounded-lg bg-indigo-950 border border-indigo-700/50 text-indigo-300 font-bold flex items-center justify-center text-xs shrink-0">
                    {p.name.charAt(0).toUpperCase()}
                  </div>
                  <div className="truncate">
                    <div className="font-semibold text-white truncate">{p.name}</div>
                    <div className="text-[10px] text-slate-400 flex items-center gap-1 truncate">
                      <MapPin className="w-2.5 h-2.5 text-slate-500" />
                      <span className="truncate">{p.location || 'Kebun'}</span>
                    </div>
                  </div>
                </div>
                <div className="w-2 h-2 rounded-full bg-emerald-400 shrink-0" />
              </div>
            ))}
          </div>
        )}
      </div>

      {/* 3. FIELD & CROPS SUMMARY */}
      <div className="space-y-2.5">
        <div className="flex items-center justify-between text-xs font-bold text-slate-400 uppercase tracking-wider">
          <span className="flex items-center gap-1.5">
            <Sprout className="w-3.5 h-3.5 text-emerald-400" />
            <span>Kondisi Lahan</span>
          </span>
        </div>

        <div className="bg-[#11192b] border border-[#1e2a42] rounded-xl p-3 space-y-3 text-xs">
          <div className="flex items-center justify-between">
            <span className="text-slate-400 flex items-center gap-1.5">
              <Sparkles className="w-3.5 h-3.5 text-amber-400" />
              <span>Siap Panen</span>
            </span>
            <span className="font-mono font-bold text-amber-300 text-sm">
              {readyCrops} petak
            </span>
          </div>

          <div className="space-y-1">
            <div className="flex items-center justify-between text-[11px]">
              <span className="text-slate-400 flex items-center gap-1.5">
                <Droplets className="w-3.5 h-3.5 text-sky-400" />
                <span>Penyiraman</span>
              </span>
              <span className="font-mono text-slate-300">{waterPercent}%</span>
            </div>
            <div className="w-full bg-[#0a0f1d] rounded-full h-1.5 overflow-hidden">
              <div 
                className="bg-sky-500 h-full rounded-full transition-all duration-500"
                style={{ width: `${waterPercent}%` }}
              />
            </div>
          </div>

          <div className="text-[10px] text-slate-500 text-right pt-1 border-t border-[#1a253b]">
            Total: {totalCrops} tanaman terdaftar
          </div>
        </div>
      </div>

      {/* 4. SERVER HEALTH TICKER */}
      <div className="mt-auto pt-3 border-t border-[#1e2a42] text-[11px] text-slate-400 flex items-center justify-between font-mono">
        <span className="flex items-center gap-1.5">
          <Radio className="w-3 h-3 text-emerald-400" />
          <span>SDR Relay</span>
        </span>
        <span className="flex items-center gap-1.5">
          <Activity className="w-3 h-3 text-indigo-400" />
          <span>{status?.tps?.toFixed(1) ?? '60.0'} TPS</span>
        </span>
      </div>
    </aside>
  );
};

export default GameRightTicker;
