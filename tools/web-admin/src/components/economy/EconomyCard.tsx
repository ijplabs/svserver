import React, { useState, useEffect, useCallback } from 'react';
import { api } from '../../lib/api';
import { EconomyOverview } from '../../types/economy';
import { 
  Coins, 
  Landmark, 
  Wallet, 
  Users, 
  RefreshCw, 
  TrendingUp, 
  AlertCircle 
} from 'lucide-react';

export const EconomyCard: React.FC = () => {
  const [economy, setEconomy] = useState<EconomyOverview | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchEconomy = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.economy.getOverview();
      setEconomy(data);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Gagal memuat data kas ekonomi';
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchEconomy();
  }, [fetchEconomy]);

  const formatGold = (amount: number) => {
    return `${amount.toLocaleString()} g`;
  };

  const totalBankDeposits = economy?.accounts?.reduce((acc, a) => acc + (a.bankBalance || 0), 0) || 0;
  const isSeparate = economy?.useSeparateWallets ?? false;

  return (
    <div className="space-y-6">
      {/* Metric Cards Row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        {/* Treasury / Shared Money */}
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl p-5 shadow-xs flex items-center space-x-4">
          <div className="w-12 h-12 rounded-xl bg-amber-50 dark:bg-amber-950/60 border border-amber-200 dark:border-amber-800/50 flex items-center justify-center text-amber-600 dark:text-amber-400 shrink-0">
            <Coins className="w-6 h-6" />
          </div>
          <div>
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              {isSeparate ? 'Sistem Dompet Terpisah' : 'Kas Bersama Kebun'}
            </span>
            <div className="text-xl font-bold text-slate-900 dark:text-white mt-0.5 font-mono">
              {isSeparate ? 'Dompet Individu Aktif' : formatGold(economy?.sharedFarmMoney || 0)}
            </div>
            <div className="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5">
              {isSeparate ? 'Setiap petani memiliki uang pribadi' : 'Uang dibagi rata untuk semua anggota kebun'}
            </div>
          </div>
        </div>

        {/* Total Bank Deposits */}
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl p-5 shadow-xs flex items-center space-x-4">
          <div className="w-12 h-12 rounded-xl bg-indigo-50 dark:bg-indigo-950/60 border border-indigo-200 dark:border-indigo-800/50 flex items-center justify-center text-indigo-600 dark:text-indigo-400 shrink-0">
            <Landmark className="w-6 h-6" />
          </div>
          <div>
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              Total Tabungan Bank Desa
            </span>
            <div className="text-xl font-bold text-indigo-900 dark:text-indigo-300 mt-0.5 font-mono">
              {formatGold(totalBankDeposits)}
            </div>
            <div className="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5">
              Tersimpan aman di engine Redis (/data)
            </div>
          </div>
        </div>

        {/* In-Game Bank Commands Info */}
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl p-5 shadow-xs flex items-center space-x-4">
          <div className="w-12 h-12 rounded-xl bg-emerald-50 dark:bg-emerald-950/60 border border-emerald-200 dark:border-emerald-800/50 flex items-center justify-center text-emerald-600 dark:text-emerald-400 shrink-0">
            <Wallet className="w-6 h-6" />
          </div>
          <div>
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              Perintah Obrolan In-Game
            </span>
            <div className="text-xs font-mono text-slate-800 dark:text-slate-200 font-semibold mt-0.5">
              !bank, !deposit, !pay
            </div>
            <div className="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5">
              Ketik !bank di chat untuk melihat panduan transaksi
            </div>
          </div>
        </div>
      </div>

      {/* Accounts Leaderboard Card */}
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl shadow-xs overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-200 dark:border-[#1e2a42] flex items-center justify-between bg-slate-50/50 dark:bg-[#0a0f1d]">
          <div className="flex items-center space-x-3">
            <div className="p-2 bg-slate-900 dark:bg-[#162035] text-white rounded-lg border border-transparent dark:border-[#1e2a42]">
              <Users className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-semibold text-slate-900 dark:text-white tracking-tight">
                Rekening Petani & Peringkat Kekayaan
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                Data saldo rekening perbankan pribadi dan dompet setiap petani terdaftar
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={fetchEconomy}
            disabled={isLoading}
            className="inline-flex items-center space-x-1.5 px-3 py-1.5 bg-white dark:bg-[#141e33] hover:bg-slate-50 dark:hover:bg-[#1a2845] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-xs font-medium text-slate-700 dark:text-slate-300 transition-colors disabled:opacity-50 cursor-pointer shadow-2xs"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
            <span>Segarkan</span>
          </button>
        </div>

        {error && (
          <div className="px-6 py-3 border-b bg-rose-50 dark:bg-rose-950/60 border-rose-200 dark:border-rose-800/50 text-rose-800 dark:text-rose-300 text-xs flex items-center space-x-2">
            <AlertCircle className="w-4 h-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        <div className="overflow-x-auto">
          {!economy || !economy.accounts || economy.accounts.length === 0 ? (
            <div className="py-12 text-center text-slate-500 dark:text-slate-400">
              <Users className="w-10 h-10 mx-auto text-slate-300 dark:text-slate-600 mb-2" />
              <p className="text-sm font-medium text-slate-700 dark:text-slate-300">Belum Ada Petani Terdaftar</p>
              <p className="text-xs text-slate-400 dark:text-slate-500 mt-1">
                Data rekening akan muncul secara otomatis saat pemain bergabung ke dalam kebun.
              </p>
            </div>
          ) : (
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50/75 dark:bg-[#0a0f1d] border-b border-slate-200 dark:border-[#1e2a42] text-slate-500 dark:text-slate-400 uppercase tracking-wider font-semibold">
                <tr>
                  <th className="px-6 py-3">Nama Petani</th>
                  <th className="px-6 py-3">Status</th>
                  <th className="px-6 py-3">Saldo Dompet</th>
                  <th className="px-6 py-3">Tabungan Bank</th>
                  <th className="px-6 py-3 text-right">Total Kekayaan</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 dark:divide-[#1e2a42] text-slate-700 dark:text-slate-300">
                {economy.accounts
                  .slice()
                  .sort((a, b) => b.totalWealth - a.totalWealth)
                  .map((acc, idx) => (
                    <tr key={acc.farmerId} className="hover:bg-slate-50/75 dark:hover:bg-[#141e33] transition-colors">
                      <td className="px-6 py-3.5 font-semibold text-slate-900 dark:text-white flex items-center space-x-2">
                        <span className="w-5 text-slate-400 dark:text-slate-500 font-mono text-[11px]">#{idx + 1}</span>
                        <span>{acc.farmerName}</span>
                      </td>
                      <td className="px-6 py-3.5">
                        <span
                          className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold border ${
                            acc.isOnline
                              ? 'bg-emerald-50 dark:bg-emerald-950/80 text-emerald-700 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800/50'
                              : 'bg-slate-100 dark:bg-[#0a0f1d] text-slate-500 dark:text-slate-400 border-slate-200 dark:border-[#1e2a42]'
                          }`}
                        >
                          {acc.isOnline ? 'Online' : 'Offline'}
                        </span>
                      </td>
                      <td className="px-6 py-3.5 font-mono text-slate-700 dark:text-slate-300">
                        {formatGold(acc.walletBalance)}
                      </td>
                      <td className="px-6 py-3.5 font-mono font-medium text-indigo-700 dark:text-indigo-400">
                        {formatGold(acc.bankBalance)}
                      </td>
                      <td className="px-6 py-3.5 font-mono font-bold text-slate-900 dark:text-white text-right flex items-center justify-end space-x-1">
                        <TrendingUp className="w-3.5 h-3.5 text-amber-500 mr-1" />
                        <span>{formatGold(acc.totalWealth)}</span>
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </div>
  );
};

export default EconomyCard;
