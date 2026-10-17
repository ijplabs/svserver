import React, { useState, useEffect } from 'react';
import { useAuthStore } from './store/authStore';
import { useFarmStore } from './store/farmStore';
import { Navbar, NavTab } from './components/layout/Navbar';
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
import { Loader2 } from 'lucide-react';

export const App: React.FC = () => {
  const { user, isLoading: authLoading, checkAuth } = useAuthStore();
  const { startPolling, stopPolling, lastUpdated } = useFarmStore();
  const [activeTab, setActiveTab] = useState<NavTab>('overview');

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
      <div className="min-h-screen flex flex-col items-center justify-center bg-slate-50 text-slate-500">
        <Loader2 className="w-8 h-8 animate-spin text-slate-900 mb-3" />
        <span className="text-xs font-semibold tracking-wider uppercase text-slate-600">
          Loading Junimo Portal...
        </span>
      </div>
    );
  }

  if (!user) {
    return (
      <div className="min-h-screen bg-slate-50">
        <LoginForm />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 flex flex-col">
      <Navbar activeTab={activeTab} onSelectTab={setActiveTab} />

      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
        {/* Tab: Server Overview & Automation Controls (Admin Only) */}
        {activeTab === 'overview' && isAdmin && (
          <div className="space-y-6">
            <ServerOverviewCard />
            <AutomationStatusCard />
            {isSuperadmin && <AdminControlsCard />}
            <PlayersCard />
          </div>
        )}

        {/* Tab: Personalized Character Sheet */}
        {activeTab === 'character' && (
          <div className="space-y-6">
            <FarmerProfileCard />
          </div>
        )}

        {/* Tab: Agricultural Telemetry (Shared) */}
        {activeTab === 'crops' && (
          <div className="space-y-6">
            <CropsDashboard />
          </div>
        )}

        {/* Tab: Livestock & Farm Animals (Shared) */}
        {activeTab === 'animals' && (
          <div className="space-y-6">
            <LivestockDashboard />
          </div>
        )}

        {/* Tab: Interactive Valley Map & Farmer Tracker (Shared) */}
        {activeTab === 'map' && (
          <div className="space-y-6">
            <ValleyMapCard />
          </div>
        )}

        {/* Tab: Live 2-Way Chat Relay (Shared) */}
        {activeTab === 'chat' && (
          <div className="space-y-6">
            <ChatRelayCard />
          </div>
        )}

        {/* Tab: Interactive SMAPI Console (Superadmin Only) */}
        {activeTab === 'console' && isSuperadmin && (
          <div className="space-y-6">
            <ConsoleTerminalCard />
          </div>
        )}

        {/* Tab: Persistent Backups Management (Admin Only) */}
        {activeTab === 'backups' && isAdmin && (
          <div className="space-y-6">
            <BackupManagerCard />
          </div>
        )}

        {/* Tab: Village Banking & Economy (Shared) */}
        {activeTab === 'economy' && (
          <div className="space-y-6">
            <EconomyCard />
          </div>
        )}

        {/* Tab: User Management & Whitelist (Admin Only) */}
        {activeTab === 'users' && isAdmin && (
          <div className="space-y-6">
            <UserManagementCard />
          </div>
        )}
      </main>

      <footer className="border-t border-slate-200 bg-white py-4 mt-8">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex flex-col sm:flex-row items-center justify-between text-xs text-slate-500 gap-2">
          <div className="flex items-center space-x-2">
            <span className="font-semibold text-slate-700">JunimoServer Enterprise</span>
            <span>•</span>
            <span>Operations & Management Portal</span>
          </div>
          <div>
            {lastUpdated && (
              <span>Last telemetry sync: {lastUpdated.toLocaleTimeString()}</span>
            )}
          </div>
        </div>
      </footer>
    </div>
  );
};

export default App;
