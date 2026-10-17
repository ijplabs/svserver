import React from 'react';
import { useAuthStore } from '../../store/authStore';
import { useFarmStore } from '../../store/farmStore';
import { 
  Server, 
  LogOut, 
  User, 
  ShieldCheck, 
  Clock, 
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
  Users
} from 'lucide-react';

export type NavTab = 'overview' | 'character' | 'crops' | 'animals' | 'map' | 'chat' | 'console' | 'backups' | 'economy' | 'users';

interface NavbarProps {
  activeTab: NavTab;
  onSelectTab: (tab: NavTab) => void;
}

export const Navbar: React.FC<NavbarProps> = ({ activeTab, onSelectTab }) => {
  const { user, logout } = useAuthStore();
  const { status, isLoading, fetchAll } = useFarmStore();
  const [copied, setCopied] = React.useState(false);

  const handleCopyInvite = () => {
    if (status?.inviteCode) {
      navigator.clipboard.writeText(status.inviteCode);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
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

  const formatSeason = (season?: string) => {
    if (!season) return '';
    return season.charAt(0).toUpperCase() + season.slice(1);
  };

  const isServerReady = status?.isOnline && status?.isReady;
  const isSuperadmin = user?.role === 'SUPERADMIN';
  const isAdmin = user?.role === 'ADMIN' || isSuperadmin;

  const navItems: { id: NavTab; label: string; icon: React.ElementType }[] = isAdmin
    ? [
        { id: 'overview', label: 'Ringkasan & Kontrol', icon: LayoutDashboard },
        { id: 'character', label: 'Karakter Saya', icon: User },
        { id: 'crops', label: 'Tanaman & Panen', icon: Sprout },
        { id: 'animals', label: 'Peternakan', icon: Milk },
        { id: 'map', label: 'Peta Lembah', icon: Compass },
        { id: 'chat', label: 'Obrolan Langsung', icon: MessageSquare },
        ...(isSuperadmin ? [{ id: 'console' as NavTab, label: 'Konsol SMAPI', icon: Terminal }] : []),
        { id: 'backups', label: 'Cadangan (Backups)', icon: Archive },
        { id: 'economy', label: 'Bank & Ekonomi', icon: Coins },
        { id: 'users', label: 'Akses & Whitelist', icon: Users },
      ]
    : [
        { id: 'character', label: 'Karakter Saya', icon: User },
        { id: 'crops', label: 'Tanaman Kebun (Shared)', icon: Sprout },
        { id: 'animals', label: 'Peternakan (Shared)', icon: Milk },
        { id: 'map', label: 'Peta Lembah (Shared)', icon: Compass },
        { id: 'chat', label: 'Obrolan Langsung', icon: MessageSquare },
        { id: 'economy', label: 'Bank & Tabungan', icon: Coins },
      ];

  return (
    <header className="bg-white border-b border-slate-200 sticky top-0 z-30 shadow-2xs">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
        {/* Left: Branding & Status Dot */}
        <div className="flex items-center space-x-4">
          <div className="flex items-center space-x-3">
            <div className="w-10 h-10 rounded-lg bg-slate-900 flex items-center justify-center text-white shadow-2xs">
              <Server className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center space-x-2">
                <span className="font-bold text-slate-900 tracking-tight text-base">
                  {status?.farmName ? `${status.farmName} Farm` : 'JunimoServer'}
                </span>
                <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-slate-100 text-slate-700 border border-slate-200">
                  {status?.gameVersion ? `v${status.gameVersion}` : 'SDV 1.6'}
                </span>
              </div>
              <div className="flex items-center space-x-2 text-xs text-slate-500">
                <span className="flex h-2 w-2 relative">
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
                <span>
                  {isServerReady
                    ? 'Server Online & Ready'
                    : status?.connectionStatusCode || 'Initializing'}
                </span>
                {status?.isPaused && (
                  <span className="text-amber-600 font-medium">• Clock Paused</span>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* Middle: In-Game Date & Time & Invite Code */}
        <div className="hidden md:flex items-center space-x-3">
          {status && (
            <>
              <div className="flex items-center space-x-2 bg-slate-50 border border-slate-200 px-3 py-1.5 rounded-lg text-xs font-medium text-slate-700">
                <Calendar className="w-3.5 h-3.5 text-slate-400" />
                <span>
                  {formatSeason(status.season)} {status.day}, Year {status.year}
                </span>
                <span className="text-slate-300">|</span>
                <Clock className="w-3.5 h-3.5 text-slate-400" />
                <span>{formatGameTime(status.timeOfDay)}</span>
              </div>

              {status.inviteCode && (
                <button
                  type="button"
                  onClick={handleCopyInvite}
                  className="flex items-center space-x-2 bg-slate-50 hover:bg-slate-100 border border-slate-200 px-3 py-1.5 rounded-lg text-xs font-mono text-slate-800 transition-colors cursor-pointer"
                  title="Click to copy Steam/Galaxy invite code"
                >
                  <span className="text-slate-500 font-sans">Code:</span>
                  <span className="font-semibold">{status.inviteCode}</span>
                  {copied ? (
                    <Check className="w-3.5 h-3.5 text-emerald-600" />
                  ) : (
                    <Copy className="w-3.5 h-3.5 text-slate-400" />
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
            title="Refresh telemetry"
          >
            <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
          </button>
        </div>

        {/* Right: User Role Pill & Logout */}
        <div className="flex items-center space-x-3">
          {user ? (
            <div className="flex items-center space-x-3">
              <div className="flex items-center space-x-2 bg-slate-100 border border-slate-200 px-3 py-1.5 rounded-lg text-xs">
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
                    {user.role === 'SUPERADMIN' ? 'Superadmin' : `Farmer #${user.farmerId}`}
                  </span>
                </div>
              </div>

              <button
                type="button"
                onClick={logout}
                className="p-2 text-slate-500 hover:text-rose-600 hover:bg-rose-50 border border-slate-200 rounded-lg transition-colors cursor-pointer"
                title="Sign out"
              >
                <LogOut className="w-4 h-4" />
              </button>
            </div>
          ) : (
            <span className="text-xs text-slate-500">Not authenticated</span>
          )}
        </div>
      </div>

      {/* Navigation Sub-bar */}
      <div className="bg-slate-50/90 border-t border-slate-200">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex items-center space-x-1.5 overflow-x-auto py-2">
          {navItems.map((item) => {
            const Icon = item.icon;
            const isActive = activeTab === item.id;
            return (
              <button
                key={item.id}
                type="button"
                onClick={() => onSelectTab(item.id)}
                className={`flex items-center space-x-2 px-3 py-1.5 rounded-lg text-xs font-medium transition-colors cursor-pointer whitespace-nowrap ${
                  isActive
                    ? 'bg-white text-slate-900 border border-slate-200 shadow-2xs font-semibold'
                    : 'text-slate-600 hover:text-slate-900 hover:bg-slate-100'
                }`}
              >
                <Icon className={`w-3.5 h-3.5 ${isActive ? 'text-indigo-600' : 'text-slate-400'}`} />
                <span>{item.label}</span>
              </button>
            );
          })}
        </div>
      </div>
    </header>
  );
};
