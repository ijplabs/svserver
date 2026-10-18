import React, { useState } from 'react';
import { api } from '../../lib/api';
import { useFarmStore } from '../../store/farmStore';
import { CabinStrategyType } from '../../types/farm';
import { 
  Sparkles, 
  X, 
  AlertTriangle, 
  CheckCircle2, 
  Loader2, 
  MapPin, 
  Users, 
  Layers, 
  Wallet, 
  ShieldAlert,
  Info
} from 'lucide-react';

interface CreateNewGameModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

export const CreateNewGameModal: React.FC<CreateNewGameModalProps> = ({
  isOpen,
  onClose,
  onSuccess,
}) => {
  const { players, fetchAll } = useFarmStore();

  const [farmName, setFarmName] = useState('Lembah Baru');
  const [selectedFarmType, setSelectedFarmType] = useState<string>('7'); // Default: Meadowlands (1.6)
  const [customFarmId, setCustomFarmId] = useState('');
  const [startingCabins, setStartingCabins] = useState<number>(3);
  const [cabinStrategy, setCabinStrategy] = useState<CabinStrategyType>('CabinStack');
  const [maxPlayers, setMaxPlayers] = useState<number>(8);
  const [profitMargin, setProfitMargin] = useState<number>(1.0);
  const [separateWallets, setSeparateWallets] = useState<boolean>(true);
  const [allowCabinRelocation, setAllowCabinRelocation] = useState<boolean>(true);
  const [allowIpConnections, setAllowIpConnections] = useState<boolean>(true);
  const [hasConfirmed, setHasConfirmed] = useState<boolean>(false);

  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  if (!isOpen) return null;

  const onlinePlayersCount = players.filter((p) => p.isOnline).length;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!farmName.trim()) {
      setErrorMessage('Nama kebun tidak boleh kosong.');
      return;
    }

    if (maxPlayers < 1 || maxPlayers > 100) {
      setErrorMessage('Batas pemain harus antara 1 sampai 100.');
      return;
    }

    if (!hasConfirmed) {
      setErrorMessage('Harap centang konfirmasi sebelum membuat game baru.');
      return;
    }

    // Determine farmType payload
    let farmTypePayload: number | string;
    if (selectedFarmType === 'custom') {
      const trimmedCustom = customFarmId.trim();
      farmTypePayload = trimmedCustom.length > 0 ? trimmedCustom : 'modded';
    } else {
      farmTypePayload = parseInt(selectedFarmType, 10);
    }

    setIsSubmitting(true);
    try {
      const response = await api.server.newGame({
        farmName: farmName.trim(),
        farmType: farmTypePayload,
        startingCabins,
        cabinStrategy,
        maxPlayers,
        profitMargin,
        separateWallets,
        allowCabinRelocation,
        allowIpConnections,
      });

      if (response && response.success) {
        onSuccess(response.message || 'Game baru berhasil dibuat! World sedang dimuat.');
        await fetchAll();
        onClose();
      } else {
        setErrorMessage(response.error || 'Gagal membuat game baru.');
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Terjadi kesalahan sistem saat membuat game baru.';
      setErrorMessage(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm overflow-y-auto">
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl max-w-xl w-full p-6 shadow-2xl space-y-5 my-8">
        {/* Modal Header */}
        <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-[#1e2a42]">
          <div className="flex items-center space-x-2.5">
            <div className="w-8 h-8 rounded-lg bg-emerald-50 dark:bg-emerald-950/60 border border-emerald-200 dark:border-emerald-800/50 flex items-center justify-center text-emerald-600 dark:text-emerald-400">
              <Sparkles className="w-4 h-4" />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900 dark:text-white tracking-tight">
                Buat Save Game Baru
              </h3>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                Konfigurasi parameter world dan generate save baru langsung di server
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={isSubmitting}
            className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 hover:bg-slate-100 dark:hover:bg-[#162035] transition-colors cursor-pointer disabled:opacity-50"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Online Players Warning Banner */}
        {onlinePlayersCount > 0 && (
          <div className="p-3.5 bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800/50 rounded-xl flex items-start space-x-3 text-xs text-amber-800 dark:text-amber-200">
            <ShieldAlert className="w-4 h-4 shrink-0 text-amber-600 dark:text-amber-400 mt-0.5" />
            <div className="space-y-1">
              <span className="font-semibold block">
                Perhatian: Terdapat {onlinePlayersCount} pemain aktif di server
              </span>
              <p className="text-amber-700 dark:text-amber-300 text-[11px] leading-relaxed">
                Membuat game baru akan menutup sesi permainan saat ini dan kembali ke Spring 1 Tahun 1. 
                Pastikan semua pemain sudah keluar atau beri tahu pemain sebelum melanjutkan.
              </p>
            </div>
          </div>
        )}

        {/* Error Notification */}
        {errorMessage && (
          <div className="p-3 bg-rose-50 dark:bg-rose-950/60 border border-rose-200 dark:border-rose-800/50 rounded-xl flex items-center space-x-2 text-xs text-rose-700 dark:text-rose-300">
            <AlertTriangle className="w-4 h-4 shrink-0" />
            <span>{errorMessage}</span>
          </div>
        )}

        {/* Configuration Form */}
        <form onSubmit={handleSubmit} className="space-y-4 text-xs">
          {/* Farm Name & Max Players */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
            <div>
              <label className="block font-semibold text-slate-700 dark:text-slate-300 mb-1.5 flex items-center space-x-1.5">
                <MapPin className="w-3.5 h-3.5 text-slate-400" />
                <span>Nama Kebun (Farm Name)</span>
              </label>
              <input
                type="text"
                value={farmName}
                onChange={(e) => setFarmName(e.target.value)}
                placeholder="Contoh: Lembah Baru"
                required
                disabled={isSubmitting}
                className="w-full px-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-xl text-slate-900 dark:text-white placeholder-slate-400 focus:outline-hidden focus:ring-1 focus:ring-emerald-500 text-xs"
              />
            </div>

            <div>
              <label className="block font-semibold text-slate-700 dark:text-slate-300 mb-1.5 flex items-center space-x-1.5">
                <Users className="w-3.5 h-3.5 text-slate-400" />
                <span>Batas Pemain (Max Players)</span>
              </label>
              <input
                type="number"
                min={1}
                max={100}
                value={maxPlayers}
                onChange={(e) => setMaxPlayers(parseInt(e.target.value, 10) || 1)}
                required
                disabled={isSubmitting}
                className="w-full px-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-xl text-slate-900 dark:text-white focus:outline-hidden focus:ring-1 focus:ring-emerald-500 text-xs"
              />
            </div>
          </div>

          {/* Farm Type / Map Selector */}
          <div>
            <label className="block font-semibold text-slate-700 dark:text-slate-300 mb-1.5 flex items-center space-x-1.5">
              <Layers className="w-3.5 h-3.5 text-slate-400" />
              <span>Tipe Peta Kebun (Farm Type)</span>
            </label>
            <select
              value={selectedFarmType}
              onChange={(e) => setSelectedFarmType(e.target.value)}
              disabled={isSubmitting}
              className="w-full px-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-xl text-slate-900 dark:text-white focus:outline-hidden focus:ring-1 focus:ring-emerald-500 text-xs"
            >
              <option value="7">Meadowlands Farm (Rekomendasi 1.6 - Padang Rumput & Kandang Ayam)</option>
              <option value="5">Four Corners Farm (Rekomendasi Multiplayer - 4 Kuadran)</option>
              <option value="0">Standard Farm (Klasik - Lahan Pertanian Luas)</option>
              <option value="2">Forest Farm (Hutan & Hijauan Kayu Keras)</option>
              <option value="6">Beach Farm (Pantai - Peti Terdampar, Tanpa Sprinkler)</option>
              <option value="1">Riverland Farm (Kepulauan Sungai - Fokus Memancing)</option>
              <option value="3">Hilltop Farm (Perbukitan - Deposit Batu & Bijih Tambang)</option>
              <option value="4">Wilderness Farm (Belantara - Monster Muncul Malam Hari)</option>
              <option value="custom">Modded / Custom Farm (Peta Kustom Mod seperti IF2R)</option>
            </select>
          </div>

          {/* Custom Mod Farm ID input if custom is chosen */}
          {selectedFarmType === 'custom' && (
            <div className="p-3 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-xl space-y-2">
              <label className="block font-semibold text-slate-700 dark:text-slate-300 text-xs">
                Mod Farm ID / Identifier
              </label>
              <input
                type="text"
                value={customFarmId}
                onChange={(e) => setCustomFarmId(e.target.value)}
                placeholder="Contoh: modded atau Immersive Farm 2 Remastered"
                disabled={isSubmitting}
                className="w-full px-3 py-2 bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-slate-900 dark:text-white text-xs font-mono focus:outline-hidden focus:ring-1 focus:ring-emerald-500"
              />
              <p className="text-[11px] text-slate-500 dark:text-slate-400">
                Gunakan nilai &quot;modded&quot; untuk memilih peta mod kustom pertama yang terpasang secara otomatis, atau ketik ID mod peta secara spesifik.
              </p>
            </div>
          )}

          {/* Starting Cabins & Cabin Strategy */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
            <div>
              <label className="block font-semibold text-slate-700 dark:text-slate-300 mb-1.5 flex items-center space-x-1.5">
                <Users className="w-3.5 h-3.5 text-slate-400" />
                <span>Jumlah Kabin Awal (Starting Cabins)</span>
              </label>
              <select
                value={startingCabins}
                onChange={(e) => setStartingCabins(parseInt(e.target.value, 10))}
                disabled={isSubmitting}
                className="w-full px-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-xl text-slate-900 dark:text-white focus:outline-hidden focus:ring-1 focus:ring-emerald-500 text-xs"
              >
                <option value={0}>0 Kabin (Tanpa kabin awal)</option>
                <option value={1}>1 Kabin</option>
                <option value={2}>2 Kabin</option>
                <option value={3}>3 Kabin (Standar)</option>
                <option value={4}>4 Kabin</option>
                <option value={7}>7 Kabin</option>
                <option value={8}>8 Kabin (Maksimal awal)</option>
              </select>
            </div>

            <div>
              <label className="block font-semibold text-slate-700 dark:text-slate-300 mb-1.5 flex items-center space-x-1.5">
                <Layers className="w-3.5 h-3.5 text-slate-400" />
                <span>Strategi Penataan Kabin</span>
              </label>
              <select
                value={cabinStrategy}
                onChange={(e) => setCabinStrategy(e.target.value as CabinStrategyType)}
                disabled={isSubmitting}
                className="w-full px-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-xl text-slate-900 dark:text-white focus:outline-hidden focus:ring-1 focus:ring-emerald-500 text-xs"
              >
                <option value="CabinStack">CabinStack (Tumpuk Otomatis - Hemat Lahan Tani)</option>
                <option value="FarmhouseStack">FarmhouseStack (Di Dekat Rumah Host)</option>
                <option value="None">None (Tata Letak Asli Peta)</option>
              </select>
            </div>
          </div>

          {/* Economy: Profit Margin */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
            <div>
              <label className="block font-semibold text-slate-700 dark:text-slate-300 mb-1.5 flex items-center space-x-1.5">
                <Wallet className="w-3.5 h-3.5 text-slate-400" />
                <span>Margin Keuntungan (Profit Margin)</span>
              </label>
              <select
                value={profitMargin}
                onChange={(e) => setProfitMargin(parseFloat(e.target.value))}
                disabled={isSubmitting}
                className="w-full px-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-xl text-slate-900 dark:text-white focus:outline-hidden focus:ring-1 focus:ring-emerald-500 text-xs"
              >
                <option value={1.0}>1.0x (100% - Pendapatan Normal)</option>
                <option value={0.75}>0.75x (75% - Tantangan Sedang)</option>
                <option value={0.5}>0.5x (50% - Tantangan Sulit)</option>
                <option value={0.25}>0.25x (25% - Mode Ekstrem)</option>
              </select>
            </div>

            <div>
              <label className="block font-semibold text-slate-700 dark:text-slate-300 mb-1.5 flex items-center space-x-1.5">
                <Info className="w-3.5 h-3.5 text-slate-400" />
                <span>Aturan Keuangan & Akses</span>
              </label>
              <div className="space-y-2 pt-1">
                <label className="flex items-center space-x-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={separateWallets}
                    onChange={(e) => setSeparateWallets(e.target.checked)}
                    disabled={isSubmitting}
                    className="w-4 h-4 rounded text-emerald-600 focus:ring-emerald-500 border-slate-300 dark:border-slate-700"
                  />
                  <span className="text-xs text-slate-700 dark:text-slate-300">
                    Dompet Terpisah per Pemain (Separate Wallets)
                  </span>
                </label>
                <label className="flex items-center space-x-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={allowCabinRelocation}
                    onChange={(e) => setAllowCabinRelocation(e.target.checked)}
                    disabled={isSubmitting}
                    className="w-4 h-4 rounded text-emerald-600 focus:ring-emerald-500 border-slate-300 dark:border-slate-700"
                  />
                  <span className="text-xs text-slate-700 dark:text-slate-300">
                    Izinkan Perintah Relokasi Kabin (!cabin)
                  </span>
                </label>
                <label className="flex items-center space-x-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={allowIpConnections}
                    onChange={(e) => setAllowIpConnections(e.target.checked)}
                    disabled={isSubmitting}
                    className="w-4 h-4 rounded text-emerald-600 focus:ring-emerald-500 border-slate-300 dark:border-slate-700"
                  />
                  <span className="text-xs text-slate-700 dark:text-slate-300">
                    Izinkan Koneksi Direct IP
                  </span>
                </label>
              </div>
            </div>
          </div>

          {/* Confirmation Checkbox */}
          <div className="pt-2 border-t border-slate-100 dark:border-[#1e2a42]">
            <label className="flex items-start space-x-2 cursor-pointer">
              <input
                type="checkbox"
                checked={hasConfirmed}
                onChange={(e) => setHasConfirmed(e.target.checked)}
                disabled={isSubmitting}
                className="w-4 h-4 mt-0.5 rounded text-emerald-600 focus:ring-emerald-500 border-slate-300 dark:border-slate-700 shrink-0"
              />
              <span className="text-xs text-slate-600 dark:text-slate-400 leading-tight">
                Saya mengerti bahwa save game baru akan dibuat, world saat ini akan dialihkan, dan server akan memulai hari ke-1 Spring Tahun 1 secara otomatis.
              </span>
            </label>
          </div>

          {/* Modal Actions */}
          <div className="flex items-center justify-end space-x-3 pt-3">
            <button
              type="button"
              onClick={onClose}
              disabled={isSubmitting}
              className="px-4 py-2 bg-slate-100 dark:bg-[#162035] hover:bg-slate-200 dark:hover:bg-[#1e2a42] text-slate-700 dark:text-slate-300 rounded-xl text-xs font-semibold transition-colors cursor-pointer disabled:opacity-50"
            >
              Batal
            </button>
            <button
              type="submit"
              disabled={isSubmitting || !hasConfirmed}
              className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-semibold shadow-xs transition-colors cursor-pointer disabled:opacity-50 flex items-center space-x-1.5"
            >
              {isSubmitting ? (
                <>
                  <Loader2 className="w-3.5 h-3.5 animate-spin" />
                  <span>Membuat Game Baru...</span>
                </>
              ) : (
                <>
                  <CheckCircle2 className="w-3.5 h-3.5" />
                  <span>Konfirmasi & Buat Game</span>
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
