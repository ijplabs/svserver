import React, { useState, useEffect, useCallback } from 'react';
import { api } from '../../lib/api';
import { FarmAnimalInfo } from '../../types/animals';
import { 
  Heart, 
  Sparkles, 
  CheckCircle2, 
  AlertCircle, 
  RefreshCw, 
  Home, 
  Milk,
  User
} from 'lucide-react';

export const LivestockDashboard: React.FC = () => {
  const [animals, setAnimals] = useState<FarmAnimalInfo[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchAnimals = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.animals.getAnimals();
      setAnimals(data || []);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Gagal memuat status hewan ternak';
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchAnimals();
  }, [fetchAnimals]);

  const totalAnimals = animals.length;
  const pettedCount = animals.filter((a) => a.wasPet).length;
  const fedCount = animals.filter((a) => a.wasFed).length;
  const produceReadyCount = animals.filter((a) => !!a.currentProduce).length;

  return (
    <div className="space-y-6">
      {/* Metric Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {/* Total Animals */}
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl p-4 shadow-xs flex items-center space-x-3.5">
          <div className="w-10 h-10 rounded-lg bg-indigo-50 dark:bg-indigo-950/60 border border-indigo-200 dark:border-indigo-800/50 flex items-center justify-center text-indigo-600 dark:text-indigo-400 shrink-0">
            <Home className="w-5 h-5" />
          </div>
          <div>
            <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              Total Hewan Ternak
            </span>
            <div className="text-xl font-bold text-slate-900 dark:text-white font-mono">{totalAnimals} Ekor</div>
          </div>
        </div>

        {/* Petted Today */}
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl p-4 shadow-xs flex items-center space-x-3.5">
          <div className="w-10 h-10 rounded-lg bg-rose-50 dark:bg-rose-950/60 border border-rose-200 dark:border-rose-800/50 flex items-center justify-center text-rose-600 dark:text-rose-400 shrink-0">
            <Heart className="w-5 h-5" />
          </div>
          <div>
            <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              Sudah Dielus Hari Ini
            </span>
            <div className="text-xl font-bold text-slate-900 dark:text-white font-mono">
              {pettedCount} / {totalAnimals}
            </div>
          </div>
        </div>

        {/* Fed Today */}
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl p-4 shadow-xs flex items-center space-x-3.5">
          <div className="w-10 h-10 rounded-lg bg-emerald-50 dark:bg-emerald-950/60 border border-emerald-200 dark:border-emerald-800/50 flex items-center justify-center text-emerald-600 dark:text-emerald-400 shrink-0">
            <CheckCircle2 className="w-5 h-5" />
          </div>
          <div>
            <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              Sudah Makan (Kenyang)
            </span>
            <div className="text-xl font-bold text-slate-900 dark:text-white font-mono">
              {fedCount} / {totalAnimals}
            </div>
          </div>
        </div>

        {/* Produce Ready */}
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl p-4 shadow-xs flex items-center space-x-3.5">
          <div className="w-10 h-10 rounded-lg bg-amber-50 dark:bg-amber-950/60 border border-amber-200 dark:border-amber-800/50 flex items-center justify-center text-amber-600 dark:text-amber-400 shrink-0">
            <Milk className="w-5 h-5" />
          </div>
          <div>
            <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
              Hasil Produksi Siap Ambil
            </span>
            <div className="text-xl font-bold text-amber-600 dark:text-amber-300 font-mono">{produceReadyCount} Hasil</div>
          </div>
        </div>
      </div>

      {/* Main Table / Grid */}
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl shadow-xs overflow-hidden">
        <div className="px-6 py-4 border-b border-slate-200 dark:border-[#1e2a42] flex items-center justify-between bg-slate-50/50 dark:bg-[#0a0f1d]">
          <div>
            <h2 className="text-base font-semibold text-slate-900 dark:text-white tracking-tight">
              Daftar Hewan Ternak (Kandang &amp; Padang Rumput)
            </h2>
            <p className="text-xs text-slate-500 dark:text-slate-400">
              Pantau status nutrisi, kasih sayang, dan produksi telur/susu di seluruh kebun
            </p>
          </div>

          <button
            type="button"
            onClick={fetchAnimals}
            disabled={isLoading}
            className="inline-flex items-center space-x-1.5 px-3 py-1.5 bg-white dark:bg-[#141e33] hover:bg-slate-50 dark:hover:bg-[#1a2845] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-xs font-medium text-slate-700 dark:text-slate-300 transition-colors disabled:opacity-50 cursor-pointer shadow-2xs"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
            <span>Segarkan</span>
          </button>
        </div>

        {error && (
          <div className="px-6 py-2.5 bg-rose-50 dark:bg-rose-950/60 border-b border-rose-200 dark:border-rose-800/50 text-rose-800 dark:text-rose-300 text-xs flex items-center space-x-2">
            <AlertCircle className="w-4 h-4 shrink-0" />
            <span>{error}</span>
          </div>
        )}

        <div className="overflow-x-auto">
          {animals.length === 0 ? (
            <div className="py-12 text-center text-slate-500 dark:text-slate-400">
              <Home className="w-10 h-10 mx-auto text-slate-300 dark:text-slate-600 mb-2" />
              <p className="text-sm font-medium text-slate-700 dark:text-slate-300">Belum Ada Hewan Ternak di Kebun</p>
              <p className="text-xs text-slate-400 dark:text-slate-500 mt-1">
                Bangun Coop atau Barn dan beli ternak dari Marnie untuk melihat data di sini.
              </p>
            </div>
          ) : (
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50/75 dark:bg-[#0a0f1d] border-b border-slate-200 dark:border-[#1e2a42] text-slate-500 dark:text-slate-400 uppercase tracking-wider font-semibold">
                <tr>
                  <th className="px-6 py-3">Nama Hewan</th>
                  <th className="px-6 py-3">Jenis &amp; Umur</th>
                  <th className="px-6 py-3">Lokasi / Kandang</th>
                  <th className="px-6 py-3">Pemilik</th>
                  <th className="px-6 py-3">Tingkat Kasih Sayang</th>
                  <th className="px-6 py-3">Status Hari Ini</th>
                  <th className="px-6 py-3 text-right">Hasil Panen</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 dark:divide-[#1e2a42] text-slate-700 dark:text-slate-300">
                {animals.map((a, idx) => (
                  <tr key={`${a.name}-${idx}`} className="hover:bg-slate-50/75 dark:hover:bg-[#141e33] transition-colors">
                    <td className="px-6 py-3.5 font-semibold text-slate-900 dark:text-white">
                      {a.name}
                    </td>
                    <td className="px-6 py-3.5">
                      <div className="flex flex-col">
                        <span className="font-medium text-slate-800 dark:text-slate-200">{a.type}</span>
                        <span className="text-[11px] text-slate-400 dark:text-slate-500">{a.age} hari</span>
                      </div>
                    </td>
                    <td className="px-6 py-3.5 text-slate-600 dark:text-slate-400">
                      <div className="flex items-center space-x-1.5">
                        <Home className="w-3.5 h-3.5 text-slate-400" />
                        <span>{a.homeBuilding}</span>
                      </div>
                    </td>
                    <td className="px-6 py-3.5">
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-medium bg-slate-100 dark:bg-[#162035] text-slate-700 dark:text-slate-300 border border-slate-200 dark:border-[#1e2a42]">
                        <User className="w-3 h-3 text-slate-400" />
                        <span>{a.ownerName || 'Bersama'}</span>
                      </span>
                    </td>
                    <td className="px-6 py-3.5">
                      <div className="flex items-center space-x-1">
                        <Heart className="w-3.5 h-3.5 text-rose-500 fill-rose-500" />
                        <span className="font-semibold text-slate-900 dark:text-slate-200">{a.hearts} / 5 Hati</span>
                        <span className="text-[11px] text-slate-400 dark:text-slate-500">({a.friendship} pts)</span>
                      </div>
                    </td>
                    <td className="px-6 py-3.5 space-x-2">
                      <span
                        className={`inline-flex items-center space-x-1 px-2 py-0.5 rounded-full text-[11px] font-semibold border ${
                          a.wasPet
                            ? 'bg-rose-50 dark:bg-rose-950/80 text-rose-700 dark:text-rose-300 border-rose-200 dark:border-rose-800/50'
                            : 'bg-slate-100 dark:bg-[#0a0f1d] text-slate-500 dark:text-slate-400 border-slate-200 dark:border-[#1e2a42]'
                        }`}
                      >
                        <Heart className="w-3 h-3" />
                        <span>{a.wasPet ? 'Sudah Dielus' : 'Belum Dielus'}</span>
                      </span>

                      <span
                        className={`inline-flex items-center space-x-1 px-2 py-0.5 rounded-full text-[11px] font-semibold border ${
                          a.wasFed
                            ? 'bg-emerald-50 dark:bg-emerald-950/80 text-emerald-700 dark:text-emerald-300 border-emerald-200 dark:border-emerald-800/50'
                            : 'bg-amber-50 dark:bg-amber-950/80 text-amber-700 dark:text-amber-300 border-amber-200 dark:border-amber-800/50'
                        }`}
                      >
                        <CheckCircle2 className="w-3 h-3" />
                        <span>{a.wasFed ? 'Kenyang' : 'Lapar'}</span>
                      </span>
                    </td>
                    <td className="px-6 py-3.5 text-right font-medium">
                      {a.currentProduce ? (
                        <span className="inline-flex items-center space-x-1 px-2 py-0.5 rounded-full text-[11px] font-bold bg-amber-50 dark:bg-amber-950/80 text-amber-700 dark:text-amber-300 border border-amber-200 dark:border-amber-800/50">
                          <Sparkles className="w-3 h-3" />
                          <span>Siap Diambil</span>
                        </span>
                      ) : (
                        <span className="text-slate-400 dark:text-slate-500 text-[11px]">Belum siap</span>
                      )}
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

export default LivestockDashboard;
