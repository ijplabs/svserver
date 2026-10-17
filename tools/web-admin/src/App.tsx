import React, { useEffect } from 'react';
import { useAuthStore } from './store/authStore';
import { useFarmStore } from './store/farmStore';
import { Navbar } from './components/layout/Navbar';
import { LoginForm } from './components/auth/LoginForm';
import { ServerOverviewCard } from './components/dashboard/ServerOverviewCard';
import { AdminControlsCard } from './components/dashboard/AdminControlsCard';
import { CropsDashboard } from './components/crops/CropsDashboard';
import { PlayersCard } from './components/players/PlayersCard';
import { Loader2 } from 'lucide-react';

export const App: React.FC = () => {
  const { user, isLoading: authLoading, checkAuth } = useAuthStore();
  const { startPolling, stopPolling, lastUpdated } = useFarmStore();

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
      <Navbar />

      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-6 space-y-6">
        {/* Metric Overview Row */}
        <ServerOverviewCard />

        {/* Superadmin Quick Action Controls */}
        {isSuperadmin && <AdminControlsCard />}

        {/* Crops Monitoring & Harvest Readiness */}
        <CropsDashboard />

        {/* Connected Players & Farmhand Cabin Slots */}
        <PlayersCard />
      </main>

      <footer className="border-t border-slate-200 bg-white py-4 mt-8">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 flex flex-col sm:flex-row items-center justify-between text-xs text-slate-500 gap-2">
          <div className="flex items-center space-x-2">
            <span className="font-semibold text-slate-700">JunimoServer Enterprise</span>
            <span>•</span>
            <span>Multi-User Telemetry Dashboard</span>
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
