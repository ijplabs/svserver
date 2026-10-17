import React, { useState, useEffect } from 'react';
import { useAuthStore } from '../../store/authStore';
import { useFarmStore } from '../../store/farmStore';
import { api } from '../../lib/api';
import { 
  Server, 
  LogOut, 
  User, 
  ShieldCheck, 
  Calendar, 
  RefreshCw,
  Copy,
  Check,
  LayoutDashboard,
  Sprout,
  Milk,
  MessageSquare,
  Terminal,
  Archive,
  Coins,
  Compass,
  Users,
  Blocks,
  Sun,
  Moon,
  Sunset,
  Sunrise,
  Maximize2,
  Minimize2,
  Columns,
  StretchHorizontal,
  Pause,
  Play,
  Menu,
  X,
  ChevronRight
} from 'lucide-react';

export type NavTab = 
  | 'overview' 
  | 'character' 
  | 'crops' 
  | 'animals' 
  | 'map' 
  | 'chat' 
  | 'console' 
  | 'backups' 
  | 'economy' 
  | 'users' 
  | 'mods';

interface NavbarProps {
  activeTab: NavTab;
  onSelectTab: (tab: NavTab) => void;
  isWidescreen: boolean;
  onToggleWidescreen: () => void;
}

export const Navbar: React.FC<NavbarProps> = ({ 
  activeTab, 
  onSelectTab, 
  isWidescreen, 
  onToggleWidescreen 
}) => {
  const { user, logout } = useAuthStore();
  const { status, isLoading, fetchAll } = useFarmStore();
  const [copied, setCopied] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [isPausing, setIsPausing] = useState(false);

  useEffect(() => {
    const handleFullscreenChange = () => {
      setIsFullscreen(!!document.fullscreenElement);
    };
    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () => document.removeEventListener('fullscreenchange', handleFullscreenChange);
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
        await api.server.pauseGame('Jeda cepat via Web Admin');
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

  const getSeasonTheme = (season?: string) => {
    switch (season?.toLowerCase()) {
      case 'spring':
        return {
          label: 'Musim Semi',
          enLabel: 'Spring',
          badgeClass: 'bg-emerald-50 text-emerald-900 border-emerald-200/80',
          dotClass: 'bg-emerald-500',
        };
      case 'summer':
        return {
          label: 'Musim Panas',
          enLabel: 'Summer',
          badgeClass: 'bg-amber-50 text-amber-900 border-amber-200/80',
          dotClass: 'bg-amber-500',
        };
      case 'fall':
        return {
          label: 'Musim Gugur',
          enLabel: 'Fall',
          badgeClass: 'bg-orange-50 text-orange-900 border-orange-200/80',
          dotClass: 'bg-orange-500',
        };
      case 'winter':
        return {
          label: 'Musim Dingin',
          enLabel: 'Winter',
          badgeClass: 'bg-sky-50 text-sky-900 border-sky-200/80',
          dotClass: 'bg-sky-500',
        };
      default:
        return {
          label: 'Stardew Valley',
          enLabel: 'Valley',
          badgeClass: 'bg-slate-50 text-slate-800 border-slate-200',
          dotClass: 'bg-slate-500',
        };
    }
  };

  const isServerReady = status?.isOnline && status?.isReady;
  const isSuperadmin = user?.role === 'SUPERADMIN';
  const isAdmin = user?.role === 'ADMIN' || isSuperadmin;
  const seasonInfo = getSeasonTheme(status?.season);
  const TimeIcon = getTimeIcon(status?.timeOfDay);

  const navItems: { id: NavTab; label: string; icon: React.ElementType; category: string }[] = isAdmin
    ? [
        { id: 'overview', label: 'Ringkasan & Host', icon: LayoutDashboard, category: 'Operasional' },
        { id: 'character', label: 'Karakter Saya', icon: User, category: 'Operasional' },
        { id: 'crops', label: 'Tanaman & Panen', icon: Sprout, category: 'Kebun' },
        { id: 'animals', label: 'Peternakan', icon: Milk, category: 'Kebun' },
        { id: 'map', label: 'Peta Lembah', icon: Compass, category: 'Kebun' },
        { id: 'chat', label: 'Obrolan Langsung', icon: MessageSquare, category: 'Komunitas' },
        { id: 'economy', label: 'Bank & Ekonomi', icon: Coins, category: 'Komunitas' },
        ...(isSuperadmin ? [{ id: 'console' as NavTab, label: 'Konsol SMAPI', icon: Terminal, category: 'Sistem' }] : []),
        { id: 'backups', label: 'Cadangan Save', icon: Archive, category: 'Sistem' },
        { id: 'users', label: 'Akses & Whitelist', icon: Users, category: 'Sistem' },
        { id: 'mods', label: 'Pusat Mod Terpusat', icon: Blocks, category: 'Sistem' },
      ]
    : [
        { id: 'character', label: 'Karakter Saya', icon: User, category: 'Pribadi' },
        { id: 'crops', label: 'Tanaman Kebun (Shared)', icon: Sprout, category: 'Kebun' },
        { id: 'animals', label: 'Peternakan (Shared)', icon: Milk, category: 'Kebun' },
        { id: 'map', label: 'Peta Lembah (Shared)', icon: Compass, category: 'Kebun' },
        { id: 'chat', label: 'Obrolan Langsung', icon: MessageSquare, category: 'Komunitas' },
        { id: 'economy', label: 'Bank & Tabungan', icon: Coins, category: 'Komunitas' },
      ];

  const handleSelectMobileTab = (id: NavTab) => {
    onSelectTab(id);
    setIsMobileMenuOpen(false);
  };

  return (
    <>
      <header className="bg-white border-b border-slate-200 sticky top-0 z-30 shadow-2xs">
        <div className={`mx-auto px-3 sm:px-6 lg:px-8 h-16 flex items-center justify-between transition-all duration-200 ${
          isWidescreen ? 'w-full max-w-[1920px]' : 'max-w-7xl'
        }`}>
          {/* Left: Branding & Status */}
          <div className="flex items-center space-x-3 sm:space-x-4">
            <div className="flex items-center space-x-2.5">
              <div className="w-9 h-9 sm:w-10 sm:h-10 rounded-lg bg-slate-900 flex items-center justify-center text-amber-300 shadow-2xs shrink-0">
                <Server className="w-4 h-4 sm:w-5 sm:h-5 text-emerald-400" />
              </div>
              <div>
                <div className="flex items-center space-x-1.5 sm:space-x-2">
                  <span className="font-bold text-slate-900 tracking-tight text-sm sm:text-base truncate max-w-[130px] sm:max-w-[200px]">
                    {status?.farmName ? `${status.farmName} Farm` : 'JunimoServer'}
                  </span>
                  <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] sm:text-xs font-semibold bg-slate-100 text-slate-700 border border-slate-200">
                    {status?.gameVersion ? `v${status.gameVersion}` : 'SDV 1.6'}
                  </span>
                </div>
                <div className="flex items-center space-x-1.5 text-[11px] text-slate-500">
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
                  <span className="truncate max-w-[90px] sm:max-w-none">
                    {isServerReady ? 'Online & Ready' : (status?.connectionStatusCode || 'Initializing')}
                  </span>
                  {status?.isManuallyPaused && (
                    <span className="text-amber-700 font-semibold hidden sm:inline">• Paused</span>
                  )}
                </div>
              </div>
            </div>
          </div>

          {/* Middle: Diurnal Clock & Quick Status (Desktop) */}
          <div className="hidden lg:flex items-center space-x-2 xl:space-x-3">
            {status && (
              <>
                {/* Diurnal Time & Season Badge */}
                <div className={`flex items-center space-x-2 border px-3 py-1.5 rounded-lg text-xs font-medium shadow-2xs ${seasonInfo.badgeClass}`}>
                  <Calendar className="w-3.5 h-3.5 opacity-70" />
                  <span className="font-semibold">
                    {seasonInfo.label} {status.day}, Th {status.year}
                  </span>
                  <span className="opacity-30">|</span>
                  <TimeIcon className="w-3.5 h-3.5 opacity-80" />
                  <span className="font-mono tabular-nums font-semibold">
                    {formatGameTime(status.timeOfDay)}
                  </span>
                </div>

                {/* Invite Code Quick Box */}
                {status.inviteCode && (
                  <button
                    type="button"
                    onClick={handleCopyInvite}
                    className="flex items-center space-x-2 bg-slate-50 hover:bg-amber-50/50 border border-slate-200 hover:border-amber-300 px-3 py-1.5 rounded-lg text-xs font-mono text-slate-800 transition-colors cursor-pointer"
                    title="Salin kode invite Steam/Galaxy"
                  >
                    <span className="text-slate-500 font-sans">Invite:</span>
                    <span className="font-bold text-slate-900">{status.inviteCode}</span>
                    {copied ? (
                      <Check className="w-3.5 h-3.5 text-emerald-600" />
                    ) : (
                      <Copy className="w-3.5 h-3.5 text-slate-400" />
                    )}
                  </button>
                )}

                {/* Quick In-Game Pause Toggle (Admin Only) */}
                {isAdmin && (
                  <button
                    type="button"
                    onClick={handleTogglePause}
                    disabled={isPausing}
                    className={`btn-tactile flex items-center space-x-1.5 px-2.5 py-1.5 rounded-lg text-xs font-semibold cursor-pointer disabled:opacity-50 ${
                      status.isManuallyPaused || status.isPaused
                        ? 'btn-tactile-primary'
                        : 'btn-tactile-amber'
                    }`}
                    title={status.isManuallyPaused ? 'Lanjutkan waktu game' : 'Jeda waktu game'}
                  >
                    {status.isManuallyPaused || status.isPaused ? (
                      <>
                        <Play className="w-3.5 h-3.5" />
                        <span>Resume</span>
                      </>
                    ) : (
                      <>
                        <Pause className="w-3.5 h-3.5" />
                        <span>Pause</span>
                      </>
                    )}
                  </button>
                )}
              </>
            )}

            <button
              type="button"
              onClick={() => fetchAll()}
              disabled={isLoading}
              className="p-1.5 text-slate-500 hover:text-slate-800 hover:bg-slate-100 rounded-lg transition-colors cursor-pointer disabled:opacity-50"
              title="Perbarui telemetri"
            >
              <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
            </button>
          </div>

          {/* Right: Layout Modes, Fullscreen, Profile & Mobile Hamburger */}
          <div className="flex items-center space-x-1.5 sm:space-x-2">
            {/* Widescreen Toggle (Desktop) */}
            <button
              type="button"
              onClick={onToggleWidescreen}
              className="hidden md:flex p-2 text-slate-500 hover:text-slate-900 hover:bg-slate-100 border border-slate-200 rounded-lg transition-colors cursor-pointer"
              title={isWidescreen ? 'Mode Terpusat (Standard 1280px)' : 'Mode Lebar Penuh (Widescreen 100%)'}
            >
              {isWidescreen ? (
                <Columns className="w-4 h-4" />
              ) : (
                <StretchHorizontal className="w-4 h-4" />
              )}
            </button>

            {/* Native Fullscreen Mode Toggle */}
            <button
              type="button"
              onClick={toggleFullscreen}
              className="p-2 text-slate-500 hover:text-slate-900 hover:bg-slate-100 border border-slate-200 rounded-lg transition-colors cursor-pointer"
              title={isFullscreen ? 'Keluar Layar Penuh' : 'Masuk Layar Penuh (Full Screen)'}
            >
              {isFullscreen ? (
                <Minimize2 className="w-4 h-4" />
              ) : (
                <Maximize2 className="w-4 h-4" />
              )}
            </button>

            {/* User Profile Pill (Desktop) */}
            {user && (
              <div className="hidden sm:flex items-center space-x-2 bg-slate-50 border border-slate-200 px-2.5 py-1.5 rounded-lg text-xs">
                {user.role === 'SUPERADMIN' ? (
                  <ShieldCheck className="w-3.5 h-3.5 text-indigo-600" />
                ) : (
                  <User className="w-3.5 h-3.5 text-emerald-600" />
                )}
                <div className="flex flex-col text-left">
                  <span className="font-semibold text-slate-900 leading-tight">
                    {user.username}
                  </span>
                  <span className="text-[10px] text-slate-500 uppercase tracking-wider leading-tight">
                    {user.role === 'SUPERADMIN' ? 'Superadmin' : `Petani #${user.farmerId}`}
                  </span>
                </div>
              </div>
            )}

            {/* Logout button (Desktop) */}
            {user && (
              <button
                type="button"
                onClick={logout}
                className="hidden sm:flex p-2 text-slate-500 hover:text-rose-600 hover:bg-rose-50 border border-slate-200 rounded-lg transition-colors cursor-pointer"
                title="Keluar (Sign out)"
              >
                <LogOut className="w-4 h-4" />
              </button>
            )}

            {/* Mobile Hamburger Menu Toggle */}
            <button
              type="button"
              onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
              className="lg:hidden p-2 text-slate-700 hover:text-slate-900 hover:bg-slate-100 border border-slate-200 rounded-lg transition-colors cursor-pointer"
              aria-label="Toggle Navigation Drawer"
            >
              {isMobileMenuOpen ? (
                <X className="w-5 h-5 text-slate-900" />
              ) : (
                <Menu className="w-5 h-5 text-slate-900" />
              )}
            </button>
          </div>
        </div>

        {/* Desktop Navigation Sub-bar */}
        <div className="hidden lg:block bg-slate-50/90 border-t border-slate-200">
          <div className={`mx-auto px-4 sm:px-6 lg:px-8 flex items-center space-x-1.5 overflow-x-auto py-2 transition-all duration-200 ${
            isWidescreen ? 'w-full max-w-[1920px]' : 'max-w-7xl'
          }`}>
            {navItems.map((item) => {
              const Icon = item.icon;
              const isActive = activeTab === item.id;
              return (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => onSelectTab(item.id)}
                  className={`btn-tactile flex items-center space-x-2 px-3 py-1.5 rounded-lg text-xs font-medium cursor-pointer whitespace-nowrap ${
                    isActive
                      ? 'bg-white text-slate-900 border border-slate-200 border-b-2 border-b-emerald-600 shadow-2xs font-semibold'
                      : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100 border border-transparent'
                  }`}
                >
                  <Icon className={`w-3.5 h-3.5 ${isActive ? 'text-emerald-600' : 'text-slate-400'}`} />
                  <span>{item.label}</span>
                </button>
              );
            })}
          </div>
        </div>
      </header>

      {/* Mobile Drawer (Slide-Over Off-Canvas Menu) */}
      {isMobileMenuOpen && (
        <div className="fixed inset-0 z-50 lg:hidden">
          {/* Backdrop */}
          <div 
            className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs transition-opacity"
            onClick={() => setIsMobileMenuOpen(false)}
          />

          {/* Drawer Content */}
          <div className="fixed inset-y-0 right-0 max-w-xs w-full bg-white shadow-2xl flex flex-col z-10 border-l border-slate-200">
            {/* Drawer Header */}
            <div className="p-4 border-b border-slate-200 flex items-center justify-between bg-slate-50">
              <div className="flex items-center space-x-2">
                <div className="w-8 h-8 rounded-lg bg-slate-900 flex items-center justify-center text-amber-300">
                  <Server className="w-4 h-4 text-emerald-400" />
                </div>
                <div>
                  <div className="font-bold text-slate-900 text-sm">
                    {status?.farmName ? `${status.farmName} Farm` : 'JunimoServer'}
                  </div>
                  <div className="text-[10px] text-slate-500">
                    {isServerReady ? 'Online & Ready' : (status?.connectionStatusCode || 'Initializing')}
                  </div>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setIsMobileMenuOpen(false)}
                className="p-1.5 text-slate-400 hover:text-slate-700 rounded-lg hover:bg-slate-200/60"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Drawer Body (Scrollable) */}
            <div className="flex-1 overflow-y-auto p-4 space-y-4">
              {/* Mobile Game Clock & Season Box */}
              {status && (
                <div className={`p-3 rounded-xl border space-y-2 ${seasonInfo.badgeClass}`}>
                  <div className="flex items-center justify-between text-xs font-semibold">
                    <span className="flex items-center space-x-1.5">
                      <Calendar className="w-3.5 h-3.5" />
                      <span>{seasonInfo.label} {status.day} (Th {status.year})</span>
                    </span>
                    <span className="flex items-center space-x-1 font-mono">
                      <TimeIcon className="w-3.5 h-3.5" />
                      <span>{formatGameTime(status.timeOfDay)}</span>
                    </span>
                  </div>

                  {status.inviteCode && (
                    <div className="pt-2 border-t border-slate-200/60 flex items-center justify-between">
                      <span className="text-[11px] font-mono font-bold text-slate-800">
                        Code: {status.inviteCode}
                      </span>
                      <button
                        type="button"
                        onClick={handleCopyInvite}
                        className="px-2 py-1 bg-white/80 hover:bg-white text-slate-700 rounded text-[10px] font-semibold flex items-center space-x-1 border border-slate-200"
                      >
                        {copied ? <Check className="w-3 h-3 text-emerald-600" /> : <Copy className="w-3 h-3" />}
                        <span>{copied ? 'Tersalin' : 'Salin'}</span>
                      </button>
                    </div>
                  )}

                  {isAdmin && (
                    <div className="pt-2 border-t border-slate-200/60">
                      <button
                        type="button"
                        onClick={handleTogglePause}
                        disabled={isPausing}
                        className={`w-full btn-tactile py-2 px-3 rounded-lg text-xs font-semibold flex items-center justify-center space-x-2 ${
                          status.isManuallyPaused || status.isPaused
                            ? 'btn-tactile-primary'
                            : 'btn-tactile-amber'
                        }`}
                      >
                        {status.isManuallyPaused || status.isPaused ? (
                          <>
                            <Play className="w-3.5 h-3.5" />
                            <span>Lanjutkan Waktu Game (Resume)</span>
                          </>
                        ) : (
                          <>
                            <Pause className="w-3.5 h-3.5" />
                            <span>Jeda Waktu Game (Pause)</span>
                          </>
                        )}
                      </button>
                    </div>
                  )}
                </div>
              )}

              {/* Categorized Navigation Links */}
              <div className="space-y-1">
                <span className="text-[11px] font-bold uppercase tracking-wider text-slate-400 px-2 block mb-1">
                  Menu Navigasi
                </span>
                {navItems.map((item) => {
                  const Icon = item.icon;
                  const isActive = activeTab === item.id;
                  return (
                    <button
                      key={item.id}
                      type="button"
                      onClick={() => handleSelectMobileTab(item.id)}
                      className={`w-full flex items-center justify-between p-3 rounded-xl text-xs font-semibold transition-colors cursor-pointer ${
                        isActive
                          ? 'bg-slate-900 text-white shadow-sm'
                          : 'text-slate-700 hover:bg-slate-100'
                      }`}
                    >
                      <div className="flex items-center space-x-3">
                        <Icon className={`w-4 h-4 ${isActive ? 'text-emerald-400' : 'text-slate-400'}`} />
                        <span>{item.label}</span>
                      </div>
                      <ChevronRight className={`w-3.5 h-3.5 ${isActive ? 'text-white' : 'text-slate-300'}`} />
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Drawer Footer (User Info & Logout) */}
            {user && (
              <div className="p-4 border-t border-slate-200 bg-slate-50 space-y-3 pb-safe">
                <div className="flex items-center space-x-3">
                  <div className="w-9 h-9 rounded-lg bg-white border border-slate-200 flex items-center justify-center text-slate-700 font-bold">
                    {user.role === 'SUPERADMIN' ? (
                      <ShieldCheck className="w-5 h-5 text-indigo-600" />
                    ) : (
                      <User className="w-5 h-5 text-emerald-600" />
                    )}
                  </div>
                  <div className="flex-1 truncate">
                    <div className="font-semibold text-slate-900 text-xs truncate">
                      {user.username}
                    </div>
                    <div className="text-[10px] text-slate-500 uppercase tracking-wider">
                      {user.role === 'SUPERADMIN' ? 'Superadmin' : `Petani #${user.farmerId}`}
                    </div>
                  </div>
                </div>

                <button
                  type="button"
                  onClick={logout}
                  className="w-full btn-tactile btn-tactile-subtle py-2 px-3 rounded-lg text-xs font-semibold text-rose-600 hover:bg-rose-50 hover:border-rose-200 flex items-center justify-center space-x-2"
                >
                  <LogOut className="w-4 h-4" />
                  <span>Keluar Akun</span>
                </button>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Sticky Mobile Bottom Navigation Bar (Phone Ergonomics) */}
      <nav className="fixed bottom-0 inset-x-0 bg-white/95 backdrop-blur-md border-t border-slate-200 z-30 lg:hidden shadow-lg pb-safe">
        <div className="grid grid-cols-5 h-14">
          {/* Button 1: Overview (Admin) / Karakter (Player) */}
          <button
            type="button"
            onClick={() => onSelectTab(isAdmin ? 'overview' : 'character')}
            className={`flex flex-col items-center justify-center py-1 transition-colors ${
              (isAdmin && activeTab === 'overview') || (!isAdmin && activeTab === 'character')
                ? 'text-emerald-600 font-semibold'
                : 'text-slate-500 hover:text-slate-900'
            }`}
          >
            {isAdmin ? (
              <LayoutDashboard className="w-5 h-5" />
            ) : (
              <User className="w-5 h-5" />
            )}
            <span className="text-[10px] mt-0.5">{isAdmin ? 'Host' : 'Karakter'}</span>
          </button>

          {/* Button 2: Map */}
          <button
            type="button"
            onClick={() => onSelectTab('map')}
            className={`flex flex-col items-center justify-center py-1 transition-colors ${
              activeTab === 'map' ? 'text-emerald-600 font-semibold' : 'text-slate-500 hover:text-slate-900'
            }`}
          >
            <Compass className="w-5 h-5" />
            <span className="text-[10px] mt-0.5">Peta</span>
          </button>

          {/* Button 3: Crops */}
          <button
            type="button"
            onClick={() => onSelectTab('crops')}
            className={`flex flex-col items-center justify-center py-1 transition-colors ${
              activeTab === 'crops' ? 'text-emerald-600 font-semibold' : 'text-slate-500 hover:text-slate-900'
            }`}
          >
            <Sprout className="w-5 h-5" />
            <span className="text-[10px] mt-0.5">Tanaman</span>
          </button>

          {/* Button 4: Chat */}
          <button
            type="button"
            onClick={() => onSelectTab('chat')}
            className={`flex flex-col items-center justify-center py-1 transition-colors ${
              activeTab === 'chat' ? 'text-emerald-600 font-semibold' : 'text-slate-500 hover:text-slate-900'
            }`}
          >
            <MessageSquare className="w-5 h-5" />
            <span className="text-[10px] mt-0.5">Chat</span>
          </button>

          {/* Button 5: Full Menu (Drawer trigger) */}
          <button
            type="button"
            onClick={() => setIsMobileMenuOpen(true)}
            className="flex flex-col items-center justify-center py-1 text-slate-500 hover:text-slate-900 transition-colors"
          >
            <Menu className="w-5 h-5" />
            <span className="text-[10px] mt-0.5">Lainnya</span>
          </button>
        </div>
      </nav>
    </>
  );
};

export default Navbar;
