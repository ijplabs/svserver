import React, { useState, useEffect } from 'react';
import { useFarmStore } from '../../store/farmStore';
import { useAuthStore } from '../../store/authStore';
import { api } from '../../lib/api';
import { 
  Server, 
  Calendar, 
  RefreshCw, 
  Copy, 
  Check, 
  Sun, 
  Sunrise, 
  Sunset, 
  Moon, 
  Maximize2, 
  Minimize2, 
  Pause, 
  Play, 
  Activity, 
  SidebarOpen, 
  SidebarClose, 
  Menu, 
  ShieldCheck,
  User,
  Radio
} from 'lucide-react';

interface GameTopHudProps {
  isRightTickerOpen: boolean;
  onToggleRightTicker: () => void;
  onOpenMobileMenu: () => void;
}

export const GameTopHud: React.FC<GameTopHudProps> = ({
  isRightTickerOpen,
  onToggleRightTicker,
  onOpenMobileMenu
}) => {
  const { status, isLoading, fetchAll } = useFarmStore();
  const { user } = useAuthStore();
  const [copied, setCopied] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isPausing, setIsPausing] = useState(false);

  useEffect(() => {
    const handleFullscreen = () => setIsFullscreen(!!document.fullscreenElement);
    document.addEventListener('fullscreenchange', handleFullscreen);
    return () => document.removeEventListener('fullscreenchange', handleFullscreen);
  }, []);

  const toggleFullscreen = () => {
    if (!document.fullscreenElement) {
      document.documentElement.requestFullscreen?.().catch(() => {});
    } else {
      document.exitFullscreen?.().catch(() => {});
    }
  };

  const handleCopyInvite = () => {
    if (status?.inviteCode) {
      navigator.clipboard.writeText(status.inviteCode);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const handleTogglePause = async () => {
    if (isPausing) return;
    setIsPausing(true);
    try {
      if (status?.isManuallyPaused || status?.isPaused) {
        await api.server.resumeGame();
      } else {
        await api.server.pauseGame('Jeda via Game HUD');
      }
      await fetchAll();
    } catch (err) {
      console.error('Failed to toggle game pause:', err);
    } finally {
      setIsPausing(false);
    }
  };

  const formatGameTime = (timeOfDay?: number) => {
    if (timeOfDay === undefined || timeOfDay === null) return '--:--';
    const hours = Math.floor(timeOfDay / 100);
    const mins = timeOfDay % 100;
    const period = hours >= 12 && hours < 24 ? 'PM' : 'AM';
    const displayHours = hours % 12 === 0 ? 12 : hours % 12;
    const formattedMins = mins < 10 ? `0${mins}` : mins;
    return `${displayHours}:${formattedMins} ${period}`;
  };

  const getTimeIcon = (timeOfDay?: number) => {
    if (!timeOfDay) return Sun;
    if (timeOfDay >= 600 && timeOfDay < 900) return Sunrise;
    if (timeOfDay >= 900 && timeOfDay < 1700) return Sun;
    if (timeOfDay >= 1700 && timeOfDay < 2000) return Sunset;
    return Moon;
  };

  const getSeasonStyle = (season?: string) => {
    switch (season?.toLowerCase()) {
      case 'spring':
        return {
          name: 'Spring',
          badge: 'bg-emerald-950/80 text-emerald-300 border-emerald-500/40',
          dot: 'bg-emerald-400',
        };
      case 'summer':
        return {
          name: 'Summer',
          badge: 'bg-amber-950/80 text-amber-300 border-amber-500/40',
          dot: 'bg-amber-400',
        };
      case 'fall':
        return {
          name: 'Fall',
          badge: 'bg-orange-950/80 text-orange-300 border-orange-500/40',
          dot: 'bg-orange-400',
        };
      case 'winter':
        return {
          name: 'Winter',
          badge: 'bg-sky-950/80 text-sky-300 border-sky-500/40',
          dot: 'bg-sky-400',
        };
      default:
        return {
          name: 'Valley',
          badge: 'bg-slate-900 text-slate-300 border-slate-700',
          dot: 'bg-slate-400',
        };
    }
  };

  const seasonStyle = getSeasonStyle(status?.season);
  const TimeIcon = getTimeIcon(status?.timeOfDay);
  const isServerReady = status?.isOnline && status?.isReady;
  const isSuperadmin = user?.role === 'SUPERADMIN';
  const isAdmin = user?.role === 'ADMIN' || isSuperadmin;

  return (
    <header className="h-16 bg-[#0c1220]/95 backdrop-blur-md border-b border-[#1e2a42] px-3 sm:px-5 flex items-center justify-between sticky top-0 z-40 text-slate-200">
      {/* LEFT: Game Farm Crest & Status */}
      <div className="flex items-center space-x-3">
        <button
          type="button"
          onClick={onOpenMobileMenu}
          className="lg:hidden p-2 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition"
          aria-label="Buka Menu"
        >
          <Menu className="w-5 h-5" />
        </button>

        <div className="flex items-center space-x-2.5">
          <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-amber-600 to-amber-900 border border-amber-400/40 flex items-center justify-center text-amber-200 shadow-md shrink-0">
            <Server className="w-4 h-4 text-amber-300" />
          </div>
          <div>
            <div className="flex items-center space-x-2">
              <span className="font-bold text-white text-sm sm:text-base tracking-tight truncate max-w-[120px] sm:max-w-[200px]">
                {status?.farmName ? `${status.farmName} Farm` : 'Stardew Valley'}
              </span>
              <span className="px-1.5 py-0.5 rounded text-[10px] font-mono font-bold bg-[#141d33] text-amber-300 border border-amber-500/30">
                {status?.gameVersion ? `v${status.gameVersion}` : '1.6.15'}
              </span>
            </div>
            <div className="flex items-center space-x-2 text-[11px] text-slate-400">
              <span className="flex h-2 w-2 relative shrink-0">
                <span
                  className={`animate-ping absolute inline-flex h-full w-full rounded-full opacity-75 ${
                    isServerReady ? 'bg-emerald-400' : 'bg-amber-400'
                  }`}
                />
                <span
                  className={`relative inline-flex rounded-full h-2 w-2 ${
                    isServerReady ? 'bg-emerald-500' : 'bg-amber-500'
                  }`}
                />
              </span>
              <span className="truncate max-w-[100px] sm:max-w-none text-slate-300 font-medium">
                {isServerReady ? 'World Online' : (status?.connectionStatusCode || 'Booting')}
              </span>
              {status?.isManuallyPaused && (
                <span className="text-amber-400 font-bold hidden sm:inline">• Clock Paused</span>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* CENTER: Stardew Valley Simulation Dial HUD */}
      <div className="hidden md:flex items-center space-x-2 lg:space-x-3">
        {status && (
          <>
            {/* Season, Day, Year Dial */}
            <div className={`flex items-center space-x-2 px-3 py-1.5 rounded-lg border text-xs font-semibold shadow-inner ${seasonStyle.badge}`}>
              <Calendar className="w-3.5 h-3.5 opacity-80" />
              <span>{seasonStyle.name} {status.day}</span>
              <span className="opacity-30">•</span>
              <span className="text-slate-400">Th {status.year}</span>
            </div>

            {/* In-Game Clock */}
            <div className="flex items-center space-x-2 px-3 py-1.5 rounded-lg bg-[#11192b] border border-[#223354] text-xs font-mono tabular-nums text-slate-200">
              <TimeIcon className="w-3.5 h-3.5 text-amber-400" />
              <span className="font-bold">{formatGameTime(status.timeOfDay)}</span>
            </div>

            {/* Game Loop TPS */}
            <div className="hidden xl:flex items-center space-x-1.5 px-2.5 py-1.5 rounded-lg bg-[#11192b] border border-[#223354] text-xs font-mono tabular-nums text-slate-400">
              <Activity className="w-3 h-3 text-emerald-400" />
              <span>{status.tps?.toFixed(1) ?? '60.0'} TPS</span>
            </div>

            {/* Invite Code Quick Pill */}
            {status.inviteCode && (
              <button
                type="button"
                onClick={handleCopyInvite}
                className="hidden lg:flex items-center space-x-1.5 px-2.5 py-1.5 rounded-lg bg-[#141e33] hover:bg-[#1a2845] border border-amber-500/30 hover:border-amber-400/60 text-xs font-mono text-amber-200 transition cursor-pointer"
                title="Salin Invite Code"
              >
                <Radio className="w-3 h-3 text-amber-400" />
                <span className="font-bold">{status.inviteCode}</span>
                {copied ? <Check className="w-3 h-3 text-emerald-400" /> : <Copy className="w-3 h-3 text-slate-400" />}
              </button>
            )}

            {/* Quick World Pause Toggle (Admin) */}
            {isAdmin && (
              <button
                type="button"
                onClick={handleTogglePause}
                disabled={isPausing}
                className={`btn-tactile flex items-center space-x-1 px-2.5 py-1.5 rounded-lg text-xs font-bold cursor-pointer disabled:opacity-50 ${
                  status.isManuallyPaused || status.isPaused
                    ? 'btn-tactile-primary'
                    : 'btn-tactile-amber'
                }`}
                title={status.isManuallyPaused ? 'Lanjutkan Waktu Game' : 'Jeda Waktu Game'}
              >
                {status.isManuallyPaused || status.isPaused ? (
                  <>
                    <Play className="w-3 h-3" />
                    <span>Resume</span>
                  </>
                ) : (
                  <>
                    <Pause className="w-3 h-3" />
                    <span>Pause</span>
                  </>
                )}
              </button>
            )}
          </>
        )}
      </div>

      {/* RIGHT: Cockpit Controls & Profile */}
      <div className="flex items-center space-x-2">
        <button
          type="button"
          onClick={() => fetchAll()}
          disabled={isLoading}
          className="p-2 text-slate-400 hover:text-white hover:bg-slate-800 rounded-lg transition cursor-pointer disabled:opacity-50"
          title="Sinkronisasi Telemetri"
        >
          <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
        </button>

        {/* Fullscreen Toggle */}
        <button
          type="button"
          onClick={toggleFullscreen}
          className="p-2 text-slate-400 hover:text-white hover:bg-slate-800 rounded-lg transition cursor-pointer"
          title={isFullscreen ? 'Keluar Fullscreen' : 'Masuk Fullscreen HUD'}
        >
          {isFullscreen ? <Minimize2 className="w-4 h-4" /> : <Maximize2 className="w-4 h-4" />}
        </button>

        {/* Toggle Right Telemetry Ticker (Desktop) */}
        <button
          type="button"
          onClick={onToggleRightTicker}
          className={`hidden 2xl:flex p-2 rounded-lg transition cursor-pointer ${
            isRightTickerOpen 
              ? 'text-amber-400 bg-amber-950/40 border border-amber-500/40' 
              : 'text-slate-400 hover:text-white hover:bg-slate-800 border border-transparent'
          }`}
          title={isRightTickerOpen ? 'Sembunyikan Panel Telemetri' : 'Tampilkan Panel Telemetri Lembah'}
        >
          {isRightTickerOpen ? <SidebarClose className="w-4 h-4" /> : <SidebarOpen className="w-4 h-4" />}
        </button>

        {/* User Pill */}
        {user && (
          <div className="hidden sm:flex items-center space-x-2 bg-[#141e33] border border-[#223354] px-2.5 py-1 rounded-lg text-xs">
            {user.role === 'SUPERADMIN' ? (
              <ShieldCheck className="w-3.5 h-3.5 text-indigo-400" />
            ) : (
              <User className="w-3.5 h-3.5 text-emerald-400" />
            )}
            <span className="font-semibold text-slate-200">{user.username}</span>
          </div>
        )}
      </div>
    </header>
  );
};

export default GameTopHud;
