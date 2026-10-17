import React, { useState, useEffect } from 'react';
import { useAuthStore } from './store/authStore';
import { useFarmStore } from './store/farmStore';
import { NavTab } from './components/layout/Navbar';
import { GameTopHud } from './components/layout/GameTopHud';
import { GameSidebar } from './components/layout/GameSidebar';
import { GameRightTicker } from './components/layout/GameRightTicker';
import { MobileGameDock } from './components/layout/MobileGameDock';
import { LoginForm } from './components/auth/LoginForm';
import { ServerOverviewCard } from './components/dashboard/ServerOverviewCard';
import { AdminControlsCard } from './components/dashboard/AdminControlsCard';
import { CropsDashboard } from './components/crops/CropsDashboard';
import { PlayersCard } from './components/players/PlayersCard';
import { BackupManagerCard } from './components/backups/BackupManagerCard';
import { EconomyCard } from './components/economy/EconomyCard';
import { AutomationStatusCard } from './components/automation/AutomationStatusCard';
import { ChatRelayCard } from './components/chat/ChatRelayCard';
import { ConsoleTerminalCard } from './components/console/ConsoleTerminalCard';
import { LivestockDashboard } from './components/livestock/LivestockDashboard';
import { FarmerProfileCard } from './components/farmer/FarmerProfileCard';
import { ValleyMapCard } from './components/map/ValleyMapCard';
import { UserManagementCard } from './components/admin/UserManagementCard';
import { ModManagementCard } from './components/mods/ModManagementCard';
import { Loader2 } from 'lucide-react';

export const App: React.FC = () => {
  const { user, isLoading: authLoading, checkAuth } = useAuthStore();
  const { startPolling, stopPolling, lastUpdated } = useFarmStore();
  const [activeTab, setActiveTab] = useState<NavTab>('overview');
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [isRightTickerOpen, setIsRightTickerOpen] = useState(true);

  // Theme state: 'dark' (default) or 'light'
  const [theme, setTheme] = useState<'dark' | 'light'>(() => {
    const saved = localStorage.getItem('sv_theme');
    return saved === 'light' ? 'light' : 'dark';
  });

  useEffect(() => {
    if (theme === 'dark') {
      document.documentElement.classList.add('dark');
    } else {
      document.documentElement.classList.remove('dark');
    }
    localStorage.setItem('sv_theme', theme);
  }, [theme]);

  const handleToggleTheme = () => {
    setTheme((prev) => (prev === 'dark' ? 'light' : 'dark'));
  };

  const isSuperadmin = user?.role === 'SUPERADMIN';
  const isAdmin = user?.role === 'ADMIN' || isSuperadmin;
  const isPlayer = user?.role === 'PLAYER';

  useEffect(() => {
    checkAuth();
  }, [checkAuth]);

  useEffect(() => {
    if (user) {
      if (isPlayer && activeTab === 'overview') {
        setActiveTab('character');
      }
      startPolling(4000);
      return () => stopPolling();
    }
  }, [user, startPolling, stopPolling, activeTab, isPlayer]);

  if (authLoading && !user) {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center bg-slate-50 dark:bg-[#090d16] text-slate-500 dark:text-slate-400">
        <Loader2 className="w-8 h-8 animate-spin text-amber-500 mb-3" />
        <span className="text-xs font-mono font-bold tracking-widest uppercase text-slate-700 dark:text-amber-300">
          Memuat Stardew Cockpit...
        </span>
      </div>
    );
  }

  if (!user) {
    return (
      <div className="min-h-screen bg-slate-50 dark:bg-[#090d16] text-slate-900 dark:text-slate-100 flex items-center justify-center p-4">
        <LoginForm />
      </div>
    );
  }

  return (
    <div className="h-screen h-dvh flex flex-col overflow-hidden bg-slate-100/70 dark:bg-[#080d19] text-slate-900 dark:text-slate-100 selection:bg-amber-500 selection:text-white transition-colors">
      {/* 1. TOP SIMULATION HUD */}
      <GameTopHud 
        isRightTickerOpen={isRightTickerOpen}
        onToggleRightTicker={() => setIsRightTickerOpen(!isRightTickerOpen)}
        onOpenMobileMenu={() => setIsMobileMenuOpen(true)}
        theme={theme}
        onToggleTheme={handleToggleTheme}
      />

      {/* 2. MAIN COCKPIT BODY (Left Sidebar + Center Canvas + Right Telemetry) */}
      <div className="flex-1 flex overflow-hidden p-3 sm:p-4 lg:p-5 gap-4 lg:gap-5 min-h-0">
        {/* LEFT GAMING DOCK / SIDEBAR */}
        <GameSidebar 
          activeTab={activeTab}
          onSelectTab={setActiveTab}
          isMobileOpen={isMobileMenuOpen}
          onCloseMobile={() => setIsMobileMenuOpen(false)}
        />

        {/* CENTER VIEWPORT / ACTIVE GAME STAGE */}
        <main className="flex-1 overflow-y-auto space-y-6 pb-20 lg:pb-6 min-w-0 pr-0.5">
          {/* Tab: Server Overview & Automation Controls (Admin Only) */}
          {activeTab === 'overview' && isAdmin && (
            <div className="space-y-6 max-w-7xl mx-auto">
              <ServerOverviewCard />
              <AutomationStatusCard />
              {isSuperadmin && <AdminControlsCard />}
              <PlayersCard />
            </div>
          )}

          {/* Tab: Personalized Character Sheet */}
          {activeTab === 'character' && (
            <div className="space-y-6 max-w-7xl mx-auto">
              <FarmerProfileCard />
            </div>
          )}

          {/* Tab: Agricultural Telemetry (Shared) */}
          {activeTab === 'crops' && (
            <div className="space-y-6 max-w-7xl mx-auto">
              <CropsDashboard />
            </div>
          )}

          {/* Tab: Livestock & Farm Animals (Shared) */}
          {activeTab === 'animals' && (
            <div className="space-y-6 max-w-7xl mx-auto">
              <LivestockDashboard />
            </div>
          )}

          {/* Tab: Interactive Valley Map & Farmer Tracker (Shared) */}
          {activeTab === 'map' && (
            <div className="space-y-6 max-w-7xl mx-auto">
              <ValleyMapCard />
            </div>
          )}

          {/* Tab: Live 2-Way Chat Relay (Shared) */}
          {activeTab === 'chat' && (
            <div className="space-y-6 max-w-7xl mx-auto">
              <ChatRelayCard />
            </div>
          )}

          {/* Tab: Interactive SMAPI Console (Superadmin Only) */}
          {activeTab === 'console' && isSuperadmin && (
            <div className="space-y-6 max-w-7xl mx-auto">
              <ConsoleTerminalCard />
            </div>
          )}

          {/* Tab: Persistent Backups Management (Admin Only) */}
          {activeTab === 'backups' && isAdmin && (
            <div className="space-y-6 max-w-7xl mx-auto">
              <BackupManagerCard />
            </div>
          )}

          {/* Tab: Village Banking & Economy (Shared) */}
          {activeTab === 'economy' && (
            <div className="space-y-6 max-w-7xl mx-auto">
              <EconomyCard />
            </div>
          )}

          {/* Tab: User Management & Whitelist (Admin Only) */}
          {activeTab === 'users' && isAdmin && (
            <div className="space-y-6 max-w-7xl mx-auto">
              <UserManagementCard />
            </div>
          )}

          {/* Tab: Centralized Mod Management & Policy (Admin Only) */}
          {activeTab === 'mods' && isAdmin && (
            <div className="space-y-6 max-w-7xl mx-auto">
              <ModManagementCard />
            </div>
          )}

          {/* Footer Bar */}
          <footer className="pt-6 border-t border-slate-200 dark:border-[#1e2a42] text-xs text-slate-500 dark:text-slate-400 flex flex-col sm:flex-row items-center justify-between gap-2 max-w-7xl mx-auto">
            <div className="flex items-center space-x-2">
              <span className="font-semibold text-slate-700 dark:text-slate-300">JunimoServer Cockpit</span>
              <span>•</span>
              <span>Stardew Valley 1.6 Dedicated Engine</span>
            </div>
            {lastUpdated && (
              <span className="font-mono tabular-nums text-slate-500 dark:text-slate-400">
                Telemetri tersinkronisasi: {lastUpdated.toLocaleTimeString()}
              </span>
            )}
          </footer>
        </main>

        {/* RIGHT TELEMETRY TICKER (Widescreen 2xl) */}
        {isRightTickerOpen && <GameRightTicker />}
      </div>

      {/* 3. MOBILE GAME DOCK (Sticky Bottom Navigation for Phones) */}
      <MobileGameDock 
        activeTab={activeTab}
        onSelectTab={setActiveTab}
        onOpenMenu={() => setIsMobileMenuOpen(true)}
      />
    </div>
  );
};

export default App;
