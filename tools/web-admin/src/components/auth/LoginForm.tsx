import React, { useState } from 'react';
import { useAuthStore } from '../../store/authStore';
import { 
  Lock, 
  User, 
  KeyRound, 
  ShieldCheck, 
  Sprout, 
  AlertCircle,
  HelpCircle,
  ArrowRight
} from 'lucide-react';

export const LoginForm: React.FC = () => {
  const { login, register, isLoading, error, clearError } = useAuthStore();
  
  // Tab: 'farmer-login' | 'farmer-register' | 'admin-login'
  const [activeTab, setActiveTab] = useState<'farmer-login' | 'farmer-register' | 'admin-login'>('farmer-login');

  // Farmer login fields
  const [farmerName, setFarmerName] = useState('');
  const [farmerPass, setFarmerPass] = useState('');

  // Farmer register fields
  const [regCode, setRegCode] = useState('');
  const [regPass, setRegPass] = useState('');
  const [regPassConfirm, setRegPassConfirm] = useState('');
  const [localError, setLocalError] = useState<string | null>(null);

  // Admin login fields
  const [adminUser, setAdminUser] = useState('');
  const [adminPass, setAdminPass] = useState('');

  const handleTabChange = (tab: 'farmer-login' | 'farmer-register' | 'admin-login') => {
    setActiveTab(tab);
    clearError();
    setLocalError(null);
  };

  const handleFarmerLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLocalError(null);
    if (!farmerName.trim() || !farmerPass) {
      setLocalError('Character name and password are required');
      return;
    }
    await login({ username: farmerName.trim(), password: farmerPass });
  };

  const handleFarmerRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setLocalError(null);
    if (!regCode.trim() || regCode.trim().length !== 6) {
      setLocalError('Please enter a valid 6-digit registration code');
      return;
    }
    if (!regPass || regPass.length < 4) {
      setLocalError('Password must be at least 4 characters long');
      return;
    }
    if (regPass !== regPassConfirm) {
      setLocalError('Passwords do not match');
      return;
    }
    await register({ code: regCode.trim(), password: regPass });
  };

  const handleAdminLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLocalError(null);
    if (!adminUser.trim() || !adminPass) {
      setLocalError('Username and password are required');
      return;
    }
    await login({ username: adminUser.trim(), password: adminPass });
  };

  const displayError = localError || error;

  return (
    <div className="min-h-[85vh] flex items-center justify-center p-4">
      <div className="w-full max-w-md bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl shadow-xl p-6 sm:p-8 transition-colors">
        {/* Header */}
        <div className="text-center mb-6">
          <div className="w-12 h-12 bg-slate-900 dark:bg-[#162035] text-white rounded-xl flex items-center justify-center mx-auto mb-3 border border-slate-800 dark:border-[#1e2a42] shadow-2xs">
            <Sprout className="w-6 h-6 text-emerald-400" />
          </div>
          <h1 className="text-xl font-bold text-slate-900 dark:text-white tracking-tight">
            Junimo Farm Portal
          </h1>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
            Access real-time crop telemetry, schedule, and server controls
          </p>
        </div>

        {/* Tab Selector */}
        <div className="grid grid-cols-3 gap-1 bg-slate-100 dark:bg-[#0a0f1d] border border-slate-200/80 dark:border-[#1e2a42] p-1 rounded-xl mb-6 text-xs font-semibold">
          <button
            type="button"
            onClick={() => handleTabChange('farmer-login')}
            className={`py-2 rounded-lg transition-all cursor-pointer ${
              activeTab === 'farmer-login'
                ? 'bg-white dark:bg-[#162035] text-slate-900 dark:text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            }`}
          >
            Farmer
          </button>
          <button
            type="button"
            onClick={() => handleTabChange('farmer-register')}
            className={`py-2 rounded-lg transition-all cursor-pointer ${
              activeTab === 'farmer-register'
                ? 'bg-white dark:bg-[#162035] text-slate-900 dark:text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            }`}
          >
            Link Code
          </button>
          <button
            type="button"
            onClick={() => handleTabChange('admin-login')}
            className={`py-2 rounded-lg transition-all cursor-pointer ${
              activeTab === 'admin-login'
                ? 'bg-white dark:bg-[#162035] text-slate-900 dark:text-white shadow-xs'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            }`}
          >
            Superadmin
          </button>
        </div>

        {/* Error message */}
        {displayError && (
          <div className="mb-4 p-3 bg-rose-50 dark:bg-rose-950/40 border border-rose-200 dark:border-rose-800 text-rose-700 dark:text-rose-300 rounded-xl text-xs flex items-start space-x-2">
            <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
            <span>{displayError}</span>
          </div>
        )}

        {/* Farmer Login Tab */}
        {activeTab === 'farmer-login' && (
          <form onSubmit={handleFarmerLogin} className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                Farmer Character Name
              </label>
              <div className="relative">
                <User className="w-4 h-4 text-slate-400 dark:text-slate-500 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="text"
                  value={farmerName}
                  onChange={(e) => setFarmerName(e.target.value)}
                  placeholder="e.g. Alex"
                  className="w-full pl-9 pr-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-sm text-slate-900 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500 focus:outline-hidden focus:border-amber-500 focus:bg-white dark:focus:bg-[#141e33] transition-colors"
                  required
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                Password
              </label>
              <div className="relative">
                <Lock className="w-4 h-4 text-slate-400 dark:text-slate-500 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="password"
                  value={farmerPass}
                  onChange={(e) => setFarmerPass(e.target.value)}
                  placeholder="••••••••"
                  className="w-full pl-9 pr-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-sm text-slate-900 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500 focus:outline-hidden focus:border-amber-500 focus:bg-white dark:focus:bg-[#141e33] transition-colors"
                  required
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="w-full py-2.5 px-4 bg-amber-500 hover:bg-amber-600 active:bg-amber-700 text-slate-950 font-bold rounded-lg text-xs transition-colors disabled:opacity-50 cursor-pointer flex items-center justify-center space-x-2 shadow-xs"
            >
              <span>{isLoading ? 'Signing in...' : 'Sign in as Farmer'}</span>
              <ArrowRight className="w-3.5 h-3.5" />
            </button>

            <div className="pt-2 text-center">
              <button
                type="button"
                onClick={() => handleTabChange('farmer-register')}
                className="text-xs text-slate-500 dark:text-slate-400 hover:text-slate-800 dark:hover:text-slate-200 cursor-pointer"
              >
                First time? Link your character with <span className="font-mono font-semibold text-slate-700 dark:text-amber-400">!webpass</span>
              </button>
            </div>
          </form>
        )}

        {/* Farmer Register Tab */}
        {activeTab === 'farmer-register' && (
          <form onSubmit={handleFarmerRegister} className="space-y-4">
            <div className="p-3 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-xl text-xs text-slate-600 dark:text-slate-400 space-y-1.5">
              <div className="flex items-center space-x-1.5 font-semibold text-slate-800 dark:text-slate-200">
                <HelpCircle className="w-3.5 h-3.5 text-slate-500 dark:text-slate-400" />
                <span>How to get your OTP code:</span>
              </div>
              <p>
                1. Join the server in Stardew Valley.
              </p>
              <p>
                2. Open in-game chat and type <code className="bg-slate-200 dark:bg-[#162035] px-1 py-0.5 rounded font-mono text-slate-900 dark:text-amber-300 font-semibold border border-transparent dark:border-[#1e2a42]">!webpass</code>.
              </p>
              <p>
                3. The server whispers your 6-digit registration code.
              </p>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                6-Digit Registration Code
              </label>
              <div className="relative">
                <KeyRound className="w-4 h-4 text-slate-400 dark:text-slate-500 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="text"
                  maxLength={6}
                  value={regCode}
                  onChange={(e) => setRegCode(e.target.value.replace(/\D/g, ''))}
                  placeholder="123456"
                  className="w-full pl-9 pr-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-sm font-mono tracking-widest text-slate-900 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500 focus:outline-hidden focus:border-amber-500 focus:bg-white dark:focus:bg-[#141e33] transition-colors"
                  required
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                Create Web Password
              </label>
              <div className="relative">
                <Lock className="w-4 h-4 text-slate-400 dark:text-slate-500 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="password"
                  value={regPass}
                  onChange={(e) => setRegPass(e.target.value)}
                  placeholder="••••••••"
                  className="w-full pl-9 pr-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-sm text-slate-900 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500 focus:outline-hidden focus:border-amber-500 focus:bg-white dark:focus:bg-[#141e33] transition-colors"
                  required
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                Confirm Password
              </label>
              <div className="relative">
                <Lock className="w-4 h-4 text-slate-400 dark:text-slate-500 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="password"
                  value={regPassConfirm}
                  onChange={(e) => setRegPassConfirm(e.target.value)}
                  placeholder="••••••••"
                  className="w-full pl-9 pr-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-sm text-slate-900 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500 focus:outline-hidden focus:border-amber-500 focus:bg-white dark:focus:bg-[#141e33] transition-colors"
                  required
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="w-full py-2.5 px-4 bg-emerald-600 hover:bg-emerald-700 text-white font-semibold rounded-lg text-xs transition-colors disabled:opacity-50 cursor-pointer flex items-center justify-center space-x-2 shadow-xs"
            >
              <span>{isLoading ? 'Linking account...' : 'Link Account & Sign In'}</span>
              <ArrowRight className="w-3.5 h-3.5" />
            </button>
          </form>
        )}

        {/* Superadmin Login Tab */}
        {activeTab === 'admin-login' && (
          <form onSubmit={handleAdminLogin} className="space-y-4">
            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                Superadmin Username
              </label>
              <div className="relative">
                <ShieldCheck className="w-4 h-4 text-slate-400 dark:text-slate-500 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="text"
                  value={adminUser}
                  onChange={(e) => setAdminUser(e.target.value)}
                  placeholder="admin"
                  className="w-full pl-9 pr-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-sm text-slate-900 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500 focus:outline-hidden focus:border-amber-500 focus:bg-white dark:focus:bg-[#141e33] transition-colors"
                  required
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 dark:text-slate-300 mb-1.5">
                Admin Password
              </label>
              <div className="relative">
                <Lock className="w-4 h-4 text-slate-400 dark:text-slate-500 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="password"
                  value={adminPass}
                  onChange={(e) => setAdminPass(e.target.value)}
                  placeholder="••••••••"
                  className="w-full pl-9 pr-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-sm text-slate-900 dark:text-slate-100 placeholder-slate-400 dark:placeholder-slate-500 focus:outline-hidden focus:border-amber-500 focus:bg-white dark:focus:bg-[#141e33] transition-colors"
                  required
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="w-full py-2.5 px-4 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-semibold transition-colors disabled:opacity-50 cursor-pointer flex items-center justify-center space-x-2 shadow-xs"
            >
              <span>{isLoading ? 'Verifying...' : 'Sign in as Superadmin'}</span>
              <ArrowRight className="w-3.5 h-3.5" />
            </button>
          </form>
        )}
      </div>
    </div>
  );
};
