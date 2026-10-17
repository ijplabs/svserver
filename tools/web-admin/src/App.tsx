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
import { Loader2 } from 'lucide-react';

export const App: React.FC = () => {
  const { user, isLoading: authLoading, checkAuth } = useAuthStore();
  const { startPolling, stopPolling, lastUpdated } = useFarmStore();
  const [activeTab, setActiveTab] = useState<NavTab>('overview');

  useEffect(() => {
    checkAuth();
  }, [checkAuth]);

  useEffect(() => {
    if (user) {
      startPolling(4000);
      return () => stopPolling();
    }
  }, [user, startPolling, stopPolling]);

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

  const isSuperadmin = user.role === 'SUPERADMIN';

  return (
    <div className="min-h-screen bg-slate-50 text-slate-900 flex flex-col">
      <Navbar activeTab={activeTab} onSelectTab={setActiveTab} />

      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
        {/* Tab 1: Server Overview & Automation Controls */}
        {activeTab === 'overview' && (
          <div className="space-y-6">
            <ServerOverviewCard />
            <AutomationStatusCard />
            {isSuperadmin && <AdminControlsCard />}
            <PlayersCard />
          </div>
        )}

        {/* Tab 2: Agricultural Telemetry */}
        {activeTab === 'crops' && (
          <div className="space-y-6">
            <CropsDashboard />
          </div>
        )}

        {/* Tab 3: Livestock & Farm Animals */}
        {activeTab === 'animals' && (
          <div className="space-y-6">
            <LivestockDashboard />
          </div>
        )}

        {/* Tab 4: Live 2-Way Chat Relay */}
        {activeTab === 'chat' && (
          <div className="space-y-6">
            <ChatRelayCard />
          </div>
        )}

        {/* Tab 5: Interactive SMAPI Console (Superadmin) */}
        {activeTab === 'console' && isSuperadmin && (
          <div className="space-y-6">
            <ConsoleTerminalCard />
          </div>
        )}

        {/* Tab 6: Persistent Backups Management */}
        {activeTab === 'backups' && (
          <div className="space-y-6">
            <BackupManagerCard />
          </div>
        )}

        {/* Tab 7: Village Banking & Economy */}
        {activeTab === 'economy' && (
          <div className="space-y-6">
            <EconomyCard />
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
