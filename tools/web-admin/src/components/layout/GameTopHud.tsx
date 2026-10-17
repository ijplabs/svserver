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
  theme: 'dark' | 'light';
  onToggleTheme: () => void;
}

export const GameTopHud: React.FC<GameTopHudProps> = ({
  isRightTickerOpen,
  onToggleRightTicker,
  onOpenMobileMenu,
  theme,
  onToggleTheme
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
          badge: 'bg-emerald-50 dark:bg-emerald-950/80 text-emerald-800 dark:text-emerald-300 border-emerald-300 dark:border-emerald-500/40',
          dot: 'bg-emerald-500',
        };
      case 'summer':
        return {
          name: 'Summer',
          badge: 'bg-amber-50 dark:bg-amber-950/80 text-amber-800 dark:text-amber-300 border-amber-300 dark:border-amber-500/40',
          dot: 'bg-amber-500',
        };
      case 'fall':
        return {
          name: 'Fall',
          badge: 'bg-orange-50 dark:bg-orange-950/80 text-orange-800 dark:text-orange-300 border-orange-300 dark:border-orange-500/40',
          dot: 'bg-orange-500',
        };
      case 'winter':
        return {
          name: 'Winter',
          badge: 'bg-sky-50 dark:bg-sky-950/80 text-sky-800 dark:text-sky-300 border-sky-300 dark:border-sky-500/40',
          dot: 'bg-sky-500',
        };
      default:
        return {
          name: 'Valley',
          badge: 'bg-slate-100 dark:bg-slate-900 text-slate-700 dark:text-slate-300 border-slate-300 dark:border-slate-700',
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
    <header className="h-16 bg-white/95 dark:bg-[#0c1220]/95 backdrop-blur-md border-b border-slate-200 dark:border-[#1e2a42] px-3 sm:px-5 flex items-center justify-between sticky top-0 z-40 text-slate-800 dark:text-slate-200 transition-colors">
      {/* LEFT: Game Farm Crest & Status */}
      <div className="flex items-center space-x-3">
        <button
          type="button"
          onClick={onOpenMobileMenu}
          className="lg:hidden p-2 text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800 transition"
          aria-label="Buka Menu"
        >
          <Menu className="w-5 h-5" />
        </button>

        <div className="flex items-center space-x-2.5">
          <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-amber-500 to-amber-700 dark:from-amber-600 dark:to-amber-900 border border-amber-400/50 flex items-center justify-center text-amber-100 shadow-md shrink-0">
            <Server className="w-4 h-4 text-amber-200" />
          </div>
          <div>
            <div className="flex items-center space-x-2">
              <span className="font-bold text-slate-900 dark:text-white text-sm sm:text-base tracking-tight truncate max-w-[120px] sm:max-w-[200px]">
                {status?.farmName ? `${status.farmName} Farm` : 'Stardew Valley'}
              </span>
              <span className="px-1.5 py-0.5 rounded text-[10px] font-mono font-bold bg-amber-50 dark:bg-[#141d33] text-amber-800 dark:text-amber-300 border border-amber-200 dark:border-amber-500/30">
                {status?.gameVersion ? `v${status.gameVersion}` : '1.6.15'}
              </span>
            </div>
            <div className="flex items-center space-x-2 text-[11px] text-slate-500 dark:text-slate-400">
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
              <span className="truncate max-w-[100px] sm:max-w-none text-slate-700 dark:text-slate-300 font-medium">
                {isServerReady ? 'World Online' : (status?.connectionStatusCode || 'Booting')}
              </span>
              {status?.isManuallyPaused && (
                <span className="text-amber-600 dark:text-amber-400 font-bold hidden sm:inline">• Clock Paused</span>
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
            <div className={`flex items-center space-x-2 px-3 py-1.5 rounded-lg border text-xs font-semibold shadow-2xs ${seasonStyle.badge}`}>
              <Calendar className="w-3.5 h-3.5 opacity-80" />
              <span>{seasonStyle.name} {status.day}</span>
              <span className="opacity-30">•</span>
              <span className="opacity-80">Th {status.year}</span>
            </div>

            {/* In-Game Clock */}
            <div className="flex items-center space-x-2 px-3 py-1.5 rounded-lg bg-slate-50 dark:bg-[#11192b] border border-slate-200 dark:border-[#223354] text-xs font-mono tabular-nums text-slate-800 dark:text-slate-200 shadow-2xs">
              <TimeIcon className="w-3.5 h-3.5 text-amber-500 dark:text-amber-400" />
              <span className="font-bold">{formatGameTime(status.timeOfDay)}</span>
            </div>

            {/* Game Loop TPS */}
            <div className="hidden xl:flex items-center space-x-1.5 px-2.5 py-1.5 rounded-lg bg-slate-50 dark:bg-[#11192b] border border-slate-200 dark:border-[#223354] text-xs font-mono tabular-nums text-slate-600 dark:text-slate-400">
              <Activity className="w-3 h-3 text-emerald-500 dark:text-emerald-400" />
              <span>{status.tps?.toFixed(1) ?? '60.0'} TPS</span>
            </div>

            {/* Invite Code Quick Pill */}
            {status.inviteCode && (
              <button
                type="button"
                onClick={handleCopyInvite}
                className="hidden lg:flex items-center space-x-1.5 px-2.5 py-1.5 rounded-lg bg-slate-50 hover:bg-amber-50 dark:bg-[#141e33] dark:hover:bg-[#1a2845] border border-slate-200 hover:border-amber-300 dark:border-amber-500/30 dark:hover:border-amber-400/60 text-xs font-mono text-slate-800 dark:text-amber-200 transition cursor-pointer"
                title="Salin Invite Code"
              >
                <Radio className="w-3 h-3 text-amber-500 dark:text-amber-400" />
                <span className="font-bold">{status.inviteCode}</span>
                {copied ? <Check className="w-3 h-3 text-emerald-600 dark:text-emerald-400" /> : <Copy className="w-3 h-3 text-slate-400" />}
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
        {/* Theme Toggle Button (Dark vs Light) */}
        <button
          type="button"
          onClick={onToggleTheme}
          className="p-2 text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-amber-300 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition cursor-pointer"
          title={theme === 'dark' ? 'Ganti ke Mode Terang (Light Mode)' : 'Ganti ke Mode Gelap (Dark Mode)'}
        >
          {theme === 'dark' ? (
            <Sun className="w-4 h-4 text-amber-400" />
          ) : (
            <Moon className="w-4 h-4 text-indigo-600" />
          )}
        </button>

        <button
          type="button"
          onClick={() => fetchAll()}
          disabled={isLoading}
          className="p-2 text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition cursor-pointer disabled:opacity-50"
          title="Sinkronisasi Telemetri"
        >
          <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
        </button>

        {/* Fullscreen Toggle */}
        <button
          type="button"
          onClick={toggleFullscreen}
          className="p-2 text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition cursor-pointer"
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
              ? 'text-amber-600 dark:text-amber-400 bg-amber-50 dark:bg-amber-950/40 border border-amber-300 dark:border-amber-500/40' 
              : 'text-slate-500 hover:text-slate-900 dark:text-slate-400 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800 border border-transparent'
          }`}
          title={isRightTickerOpen ? 'Sembunyikan Panel Telemetri' : 'Tampilkan Panel Telemetri Lembah'}
        >
          {isRightTickerOpen ? <SidebarClose className="w-4 h-4" /> : <SidebarOpen className="w-4 h-4" />}
        </button>

        {/* User Pill */}
        {user && (
          <div className="hidden sm:flex items-center space-x-2 bg-slate-50 dark:bg-[#141e33] border border-slate-200 dark:border-[#223354] px-2.5 py-1 rounded-lg text-xs">
            {user.role === 'SUPERADMIN' ? (
              <ShieldCheck className="w-3.5 h-3.5 text-indigo-600 dark:text-indigo-400" />
            ) : (
              <User className="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400" />
            )}
            <span className="font-semibold text-slate-800 dark:text-slate-200">{user.username}</span>
          </div>
        )}
      </div>
    </header>
  );
};

export default GameTopHud;
