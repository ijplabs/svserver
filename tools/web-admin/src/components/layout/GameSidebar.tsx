import React, { useState } from 'react';
import { useAuthStore } from '../../store/authStore';
import { useFarmStore } from '../../store/farmStore';
import { NavTab } from './Navbar';
import { 
  LayoutDashboard, 
  User, 
  Sprout, 
  Milk, 
  Compass, 
  MessageSquare, 
  Coins, 
  Terminal, 
  Archive, 
  Users, 
  Blocks,
  LogOut,
  ShieldCheck,
  ChevronLeft,
  ChevronRight
} from 'lucide-react';

interface GameSidebarProps {
  activeTab: NavTab;
  onSelectTab: (tab: NavTab) => void;
  isMobileOpen?: boolean;
  onCloseMobile?: () => void;
}

export const GameSidebar: React.FC<GameSidebarProps> = ({
  activeTab,
  onSelectTab,
  isMobileOpen,
  onCloseMobile
}) => {
  const { user, logout } = useAuthStore();
  const { cropsSnapshot, players } = useFarmStore();
  const [isCollapsed, setIsCollapsed] = useState(false);

  const isSuperadmin = user?.role === 'SUPERADMIN';
  const isAdmin = user?.role === 'ADMIN' || isSuperadmin;

  const readyCrops = cropsSnapshot?.readyCount ?? 0;
  const onlineFarmers = players.length;

  interface NavItem {
    id: NavTab;
    label: string;
    icon: React.ElementType;
    category: 'World' | 'Social' | 'Engine';
    badge?: number | string | null;
    badgeColor?: string;
  }

  const navItems: NavItem[] = isAdmin
    ? [
        // World & Farm
        { id: 'overview', label: 'Ringkasan Host', icon: LayoutDashboard, category: 'World', badge: onlineFarmers > 0 ? `${onlineFarmers}` : null, badgeColor: 'bg-emerald-950 text-emerald-300 border-emerald-500/40' },
        { id: 'character', label: 'Karakter Saya', icon: User, category: 'World' },
        { id: 'crops', label: 'Tanaman & Panen', icon: Sprout, category: 'World', badge: readyCrops > 0 ? `${readyCrops}` : null, badgeColor: 'bg-amber-950 text-amber-300 border-amber-500/40' },
        { id: 'animals', label: 'Peternakan', icon: Milk, category: 'World' },
        { id: 'map', label: 'Peta Lembah', icon: Compass, category: 'World' },
        // Social & Economy
        { id: 'chat', label: 'Obrolan Langsung', icon: MessageSquare, category: 'Social' },
        { id: 'economy', label: 'Bank & Ekonomi', icon: Coins, category: 'Social' },
        // Engine Room
        ...(isSuperadmin ? [{ id: 'console' as NavTab, label: 'Konsol SMAPI', icon: Terminal, category: 'Engine' as const }] : []),
        { id: 'backups', label: 'Cadangan Save', icon: Archive, category: 'Engine' },
        { id: 'mods', label: 'Pusat Mod Terpusat', icon: Blocks, category: 'Engine' },
        { id: 'users', label: 'Akses & Whitelist', icon: Users, category: 'Engine' },
      ]
    : [
        { id: 'character', label: 'Karakter Petani', icon: User, category: 'World' },
        { id: 'crops', label: 'Tanaman (Shared)', icon: Sprout, category: 'World', badge: readyCrops > 0 ? `${readyCrops}` : null, badgeColor: 'bg-amber-950 text-amber-300 border-amber-500/40' },
        { id: 'animals', label: 'Peternakan', icon: Milk, category: 'World' },
        { id: 'map', label: 'Peta Lembah', icon: Compass, category: 'World' },
        { id: 'chat', label: 'Obrolan Langsung', icon: MessageSquare, category: 'Social' },
        { id: 'economy', label: 'Bank Kas Desa', icon: Coins, category: 'Social' },
      ];

  const categories = [
    { key: 'World', title: 'Dunia & Ladang' },
    { key: 'Social', title: 'Komunitas' },
    { key: 'Engine', title: 'Pusat Mesin' }
  ];

  const handleSelect = (tab: NavTab) => {
    onSelectTab(tab);
    if (onCloseMobile) onCloseMobile();
  };

  return (
    <>
      {/* Mobile Drawer Overlay */}
      {isMobileOpen && (
        <div 
          className="fixed inset-0 bg-black/70 backdrop-blur-xs z-50 lg:hidden"
          onClick={onCloseMobile}
        />
      )}

      {/* Main Sidebar Element */}
      <aside className={`
        fixed lg:sticky top-0 lg:top-16 z-50 lg:z-30 h-screen lg:h-[calc(100vh-4rem)]
        bg-[#0c1220] border-r border-[#1e2a42] text-slate-300 flex flex-col justify-between
        transition-all duration-200 select-none
        ${isCollapsed ? 'w-18' : 'w-64'}
        ${isMobileOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'}
      `}>
        {/* TOP: Header (Mobile only) or Collapse Toggle */}
        <div className="p-3 border-b border-[#1e2a42] flex items-center justify-between">
          {!isCollapsed && (
            <span className="text-[11px] font-bold uppercase tracking-wider text-amber-400/90 px-2">
              Menu Kendali Game
            </span>
          )}
          <button
            type="button"
            onClick={() => setIsCollapsed(!isCollapsed)}
            className="hidden lg:flex p-1.5 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 ml-auto cursor-pointer"
            title={isCollapsed ? 'Perlebar Sidebar' : 'Persempit Sidebar (Icon Only)'}
          >
            {isCollapsed ? <ChevronRight className="w-4 h-4" /> : <ChevronLeft className="w-4 h-4" />}
          </button>
        </div>

        {/* MIDDLE: Scrollable Nav Groups */}
        <div className="flex-1 overflow-y-auto p-2.5 space-y-4">
          {categories.map((cat) => {
            const items = navItems.filter((i) => i.category === cat.key);
            if (items.length === 0) return null;

            return (
              <div key={cat.key} className="space-y-1">
                {!isCollapsed && (
                  <div className="text-[10px] font-bold uppercase tracking-wider text-slate-500 px-2 py-1">
                    {cat.title}
                  </div>
                )}
                {items.map((item) => {
                  const Icon = item.icon;
                  const isActive = activeTab === item.id;

                  return (
                    <button
                      key={item.id}
                      type="button"
                      onClick={() => handleSelect(item.id)}
                      className={`
                        w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-xs font-semibold
                        transition-all cursor-pointer group relative
                        ${isActive
                          ? 'bg-gradient-to-r from-amber-500/20 via-amber-500/10 to-transparent text-amber-200 border-l-4 border-amber-400 shadow-sm'
                          : 'text-slate-400 hover:text-slate-100 hover:bg-[#141e33]'
                        }
                        ${isCollapsed ? 'justify-center px-0' : ''}
                      `}
                      title={isCollapsed ? item.label : undefined}
                    >
                      <Icon className={`w-4 h-4 shrink-0 transition-transform ${
                        isActive ? 'text-amber-400 scale-110' : 'text-slate-400 group-hover:text-slate-200'
                      }`} />

                      {!isCollapsed && (
                        <span className="truncate flex-1 text-left">
                          {item.label}
                        </span>
                      )}

                      {!isCollapsed && item.badge && (
                        <span className={`px-1.5 py-0.5 rounded text-[10px] font-mono font-bold border ${item.badgeColor || 'bg-slate-800 text-slate-300 border-slate-700'}`}>
                          {item.badge}
                        </span>
                      )}
                    </button>
                  );
                })}
              </div>
            );
          })}
        </div>

        {/* BOTTOM: Farmer Profile & Logout */}
        {user && (
          <div className="p-3 border-t border-[#1e2a42] bg-[#090d17]">
            <div className={`flex items-center gap-2.5 ${isCollapsed ? 'justify-center' : ''}`}>
              <div className="w-8 h-8 rounded-lg bg-[#141e33] border border-amber-500/30 flex items-center justify-center text-amber-400 shrink-0 font-bold text-xs">
                {user.role === 'SUPERADMIN' ? (
                  <ShieldCheck className="w-4 h-4 text-indigo-400" />
                ) : (
                  <User className="w-4 h-4 text-emerald-400" />
                )}
              </div>

              {!isCollapsed && (
                <div className="flex-1 truncate">
                  <div className="font-bold text-xs text-white truncate">
                    {user.username}
                  </div>
                  <div className="text-[10px] text-amber-400/80 uppercase font-mono tracking-wider">
                    {user.role === 'SUPERADMIN' ? 'Superadmin' : `Petani #${user.farmerId}`}
                  </div>
                </div>
              )}

              <button
                type="button"
                onClick={logout}
                className="p-1.5 text-slate-500 hover:text-rose-400 hover:bg-rose-950/40 rounded-lg transition cursor-pointer"
                title="Keluar (Sign Out)"
              >
                <LogOut className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </aside>
    </>
  );
};

export default GameSidebar;
