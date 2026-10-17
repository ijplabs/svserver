import React, { useState, useEffect, useCallback } from 'react';
import { api } from '../../lib/api';
import { EconomyOverview } from '../../types/economy';
import { 
  Landmark, 
  Wallet, 
  Coins, 
  Users, 
  RefreshCw, 
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
      const msg = err instanceof Error ? err.message : 'Gagal memuat data ekonomi';
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchEconomy();
  }, [fetchEconomy]);

  const formatGold = (amount: number) => {
    return `${amount.toLocaleString()}g`;
  };

  const totalBankDeposits = economy?.accounts?.reduce((acc, a) => acc + (a.bankBalance || 0), 0) || 0;
  const isSeparate = economy?.useSeparateWallets ?? false;

  return (
    <div className="space-y-6">
      {/* Metric Cards Row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        {/* Treasury / Shared Money */}
        <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs flex items-center space-x-4">
          <div className="w-12 h-12 rounded-xl bg-amber-50 border border-amber-200 flex items-center justify-center text-amber-600 shrink-0">
            <Coins className="w-6 h-6" />
          </div>
          <div>
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">
              {isSeparate ? 'Sistem Dompet Terpisah' : 'Kas Bersama Kebun'}
            </span>
            <div className="text-xl font-bold text-slate-900 mt-0.5">
              {isSeparate ? 'Dompet Individu Aktif' : formatGold(economy?.sharedFarmMoney || 0)}
            </div>
            <div className="text-[11px] text-slate-500 mt-0.5">
              {isSeparate ? 'Setiap petani memiliki uang pribadi' : 'Uang dibagi rata untuk semua anggota kebun'}
            </div>
          </div>
        </div>

        {/* Total Bank Deposits */}
        <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs flex items-center space-x-4">
          <div className="w-12 h-12 rounded-xl bg-indigo-50 border border-indigo-200 flex items-center justify-center text-indigo-600 shrink-0">
            <Landmark className="w-6 h-6" />
          </div>
          <div>
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">
              Total Tabungan Bank Desa
            </span>
            <div className="text-xl font-bold text-indigo-900 mt-0.5">
              {formatGold(totalBankDeposits)}
            </div>
            <div className="text-[11px] text-slate-500 mt-0.5">
              Tersimpan aman di engine Redis (/data)
            </div>
          </div>
        </div>

        {/* In-Game Bank Commands Info */}
        <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs flex items-center space-x-4">
          <div className="w-12 h-12 rounded-xl bg-emerald-50 border border-emerald-200 flex items-center justify-center text-emerald-600 shrink-0">
            <Wallet className="w-6 h-6" />
          </div>
          <div>
            <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">
              Perintah Obrolan In-Game
            </span>
            <div className="text-xs font-mono text-slate-800 font-semibold mt-0.5">
              !bank, !deposit, !pay
            </div>
            <div className="text-[11px] text-slate-500 mt-0.5">
              Ketik !bank di chat untuk melihat panduan transaksi
            </div>
          </div>
        </div>
      </div>

      {/* Accounts Leaderboard Card */}
      <div className="bg-white border border-slate-200 rounded-xl shadow-xs overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between bg-slate-50/50">
          <div className="flex items-center space-x-3">
            <div className="p-2 bg-slate-900 text-white rounded-lg">
              <Users className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-semibold text-slate-900 tracking-tight">
                Rekening Petani & Peringkat Kekayaan
              </h2>
              <p className="text-xs text-slate-500">
                Data saldo rekening perbankan pribadi dan dompet setiap petani terdaftar
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={fetchEconomy}
            disabled={isLoading}
            className="inline-flex items-center space-x-1.5 px-3 py-1.5 bg-white hover:bg-slate-50 border border-slate-200 rounded-lg text-xs font-medium text-slate-700 transition-colors disabled:opacity-50 cursor-pointer"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
            <span>Segarkan</span>
          </button>
        </div>

        {error && (
          <div className="px-6 py-3 border-b bg-rose-50 border-rose-200 text-rose-800 text-xs flex items-center space-x-2">
            <AlertCircle className="w-4 h-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        <div className="overflow-x-auto">
          {!economy || !economy.accounts || economy.accounts.length === 0 ? (
            <div className="py-12 text-center text-slate-500">
              <Users className="w-10 h-10 mx-auto text-slate-300 mb-2" />
              <p className="text-sm font-medium text-slate-700">Belum Ada Petani Terdaftar</p>
              <p className="text-xs text-slate-400 mt-1">
                Data rekening akan muncul secara otomatis saat pemain bergabung ke dalam kebun.
              </p>
            </div>
          ) : (
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50/75 border-b border-slate-200 text-slate-500 uppercase tracking-wider font-semibold">
                <tr>
                  <th className="px-6 py-3">Nama Petani</th>
                  <th className="px-6 py-3">Status</th>
                  <th className="px-6 py-3">Saldo Dompet</th>
                  <th className="px-6 py-3">Tabungan Bank</th>
                  <th className="px-6 py-3 text-right">Total Kekayaan</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-slate-700">
                {economy.accounts
                  .slice()
                  .sort((a, b) => b.totalWealth - a.totalWealth)
                  .map((acc) => (
                    <tr key={acc.farmerId} className="hover:bg-slate-50/75 transition-colors">
                      <td className="px-6 py-3.5 font-medium text-slate-900">
                        <div className="flex flex-col">
                          <span className="font-semibold text-slate-900">{acc.farmerName}</span>
                          <span className="font-mono text-[11px] text-slate-400">
                            ID: {acc.farmerId}
                          </span>
                        </div>
                      </td>
                      <td className="px-6 py-3.5">
                        {acc.isOnline ? (
                          <span className="inline-flex items-center space-x-1.5 px-2 py-0.5 rounded-full text-[11px] font-medium bg-emerald-50 text-emerald-700 border border-emerald-200">
                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                            <span>Online</span>
                          </span>
                        ) : (
                          <span className="inline-flex items-center space-x-1.5 px-2 py-0.5 rounded-full text-[11px] font-medium bg-slate-100 text-slate-500 border border-slate-200">
                            <span className="w-1.5 h-1.5 rounded-full bg-slate-400" />
                            <span>Offline</span>
                          </span>
                        )}
                      </td>
                      <td className="px-6 py-3.5 font-medium text-slate-800">
                        {formatGold(acc.walletBalance)}
                      </td>
                      <td className="px-6 py-3.5 font-semibold text-indigo-700">
                        {formatGold(acc.bankBalance)}
                      </td>
                      <td className="px-6 py-3.5 text-right font-bold text-slate-900">
                        {formatGold(acc.totalWealth)}
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
