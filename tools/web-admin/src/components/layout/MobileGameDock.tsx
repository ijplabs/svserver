import React from 'react';
import { NavTab } from './Navbar';
import { useAuthStore } from '../../store/authStore';
import { 
  LayoutDashboard, 
  User, 
  Compass, 
  Sprout, 
  MessageSquare, 
  Menu 
} from 'lucide-react';

interface MobileGameDockProps {
  activeTab: NavTab;
  onSelectTab: (tab: NavTab) => void;
  onOpenMenu: () => void;
}

export const MobileGameDock: React.FC<MobileGameDockProps> = ({
  activeTab,
  onSelectTab,
  onOpenMenu
}) => {
  const { user } = useAuthStore();
  const isAdmin = user?.role === 'ADMIN' || user?.role === 'SUPERADMIN';

  return (
    <nav className="fixed bottom-0 inset-x-0 bg-white/95 dark:bg-[#0c1220]/95 backdrop-blur-md border-t border-slate-200 dark:border-[#1e2a42] z-40 lg:hidden shadow-2xl pb-safe transition-colors">
      <div className="grid grid-cols-5 h-14">
        {/* Slot 1: Host Overview (Admin) or Farmer Character (Player) */}
        <button
          type="button"
          onClick={() => onSelectTab(isAdmin ? 'overview' : 'character')}
          className={`flex flex-col items-center justify-center py-1 transition-all ${
            (isAdmin && activeTab === 'overview') || (!isAdmin && activeTab === 'character')
              ? 'text-amber-600 dark:text-amber-300 font-bold scale-105'
              : 'text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100'
          }`}
        >
          {isAdmin ? <LayoutDashboard className="w-5 h-5" /> : <User className="w-5 h-5" />}
          <span className="text-[10px] mt-0.5 tracking-tight">{isAdmin ? 'Host' : 'Karakter'}</span>
        </button>

        {/* Slot 2: Regional Map */}
        <button
          type="button"
          onClick={() => onSelectTab('map')}
          className={`flex flex-col items-center justify-center py-1 transition-all ${
            activeTab === 'map' ? 'text-amber-600 dark:text-amber-300 font-bold scale-105' : 'text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100'
          }`}
        >
          <Compass className="w-5 h-5" />
          <span className="text-[10px] mt-0.5 tracking-tight">Peta</span>
        </button>

        {/* Slot 3: Crops & Farm */}
        <button
          type="button"
          onClick={() => onSelectTab('crops')}
          className={`flex flex-col items-center justify-center py-1 transition-all ${
            activeTab === 'crops' ? 'text-emerald-600 dark:text-emerald-400 font-bold scale-105' : 'text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100'
          }`}
        >
          <Sprout className="w-5 h-5" />
          <span className="text-[10px] mt-0.5 tracking-tight">Tanaman</span>
        </button>

        {/* Slot 4: Chat Relay */}
        <button
          type="button"
          onClick={() => onSelectTab('chat')}
          className={`flex flex-col items-center justify-center py-1 transition-all ${
            activeTab === 'chat' ? 'text-amber-600 dark:text-amber-300 font-bold scale-105' : 'text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100'
          }`}
        >
          <MessageSquare className="w-5 h-5" />
          <span className="text-[10px] mt-0.5 tracking-tight">Obrolan</span>
        </button>

        {/* Slot 5: Full Game Menu Trigger */}
        <button
          type="button"
          onClick={onOpenMenu}
          className="flex flex-col items-center justify-center py-1 text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-slate-100 transition-colors"
        >
          <Menu className="w-5 h-5" />
          <span className="text-[10px] mt-0.5 tracking-tight">Menu</span>
        </button>
      </div>
    </nav>
  );
};

export default MobileGameDock;
