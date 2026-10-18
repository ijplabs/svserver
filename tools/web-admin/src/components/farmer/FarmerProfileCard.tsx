import React, { useState, useEffect } from 'react';
import {
  User,
  Heart,
  Zap,
  Coins,
  Sparkles,
  MapPin,
  Package,
  Shield,
  RefreshCw,
  AlertCircle,
  Gem,
  Award,
} from 'lucide-react';
import { api } from '../../lib/api';
import { FarmerProfile } from '../../types/character';
import { WikiImage } from '../common/WikiImage';
import { getItemImageUrl } from '../../lib/wikiAssets';

export const FarmerProfileCard: React.FC = () => {
  const [profile, setProfile] = useState<FarmerProfile | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchProfile = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.farmer.getMyProfile();
      setProfile(data);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Gagal memuat profil karakter.';
      setError(message);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchProfile();
    const interval = setInterval(fetchProfile, 10000);
    return () => clearInterval(interval);
  }, []);

  if (isLoading && !profile) {
    return (
      <div className="flex flex-col items-center justify-center p-12 bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl shadow-xs text-slate-400">
        <RefreshCw className="w-8 h-8 animate-spin mb-3 text-amber-500" />
        <span className="text-sm font-medium">Memuat profil karakter petani...</span>
      </div>
    );
  }

  if (error && !profile) {
    return (
      <div className="p-6 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900 rounded-2xl text-center space-y-3">
        <AlertCircle className="w-8 h-8 text-red-500 mx-auto" />
        <p className="text-sm text-red-700 dark:text-red-400">{error}</p>
        <button
          onClick={fetchProfile}
          className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-xl text-xs font-medium transition-colors"
        >
          Coba Lagi
        </button>
      </div>
    );
  }

  if (!profile) return null;

  const staminaPercent = Math.min(100, Math.max(0, (profile.stamina / profile.maxStamina) * 100));
  const healthPercent = Math.min(100, Math.max(0, (profile.health / profile.maxHealth) * 100));

  // Build 36-slot backpack grid
  const slots = Array.from({ length: 36 }, (_, idx) => {
    return profile.inventory.find((item) => item.slotIndex === idx) || null;
  });

  return (
    <div className="space-y-6">
      {/* HEADER CARD: CHARACTER OVERVIEW */}
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 shadow-xs transition-colors">
        <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-6">
          {/* Avatar and Basic Details */}
          <div className="flex items-center gap-4">
            <div className="w-16 h-16 rounded-2xl bg-amber-500/10 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400 flex items-center justify-center font-bold text-xl border border-slate-200 dark:border-[#1e2a42]">
              <User className="w-8 h-8" />
            </div>
            <div className="space-y-1">
              <div className="flex items-center gap-2">
                <h1 className="text-xl font-bold text-slate-900 dark:text-white">
                  {profile.name}
                </h1>
                <span className="px-2.5 py-0.5 text-xs font-semibold rounded-full bg-slate-100 dark:bg-[#162035] text-slate-700 dark:text-slate-300 border border-slate-200 dark:border-[#1e2a42]">
                  Kebun {profile.farmName}
                </span>
              </div>
              <div className="flex flex-wrap items-center gap-3 text-xs text-slate-500 dark:text-slate-400">
                <div className="flex items-center gap-1">
                  <MapPin className="w-3.5 h-3.5 text-slate-400" />
                  <span>
                    {profile.currentLocation} ({profile.tileX}, {profile.tileY})
                  </span>
                </div>
                <div className="flex items-center gap-1">
                  <Sparkles className="w-3.5 h-3.5 text-amber-500" />
                  <span>Favorit: {profile.favoriteThing || 'Belum diatur'}</span>
                </div>
              </div>
            </div>
          </div>

          {/* Currencies & Balances */}
          <div className="flex flex-wrap items-center gap-3">
            <div className="px-4 py-2.5 bg-amber-50 dark:bg-[#151c2d] border border-amber-200/70 dark:border-amber-500/30 rounded-xl flex items-center gap-2.5 shadow-2xs">
              <Coins className="w-4 h-4 text-amber-600 dark:text-amber-400" />
              <div>
                <div className="text-[10px] text-amber-700/80 dark:text-amber-400/80 font-medium">Saldo Dompet</div>
                <div className="text-sm font-bold text-amber-900 dark:text-amber-200 font-mono">
                  {profile.money.toLocaleString('id-ID')} g
                </div>
              </div>
            </div>

            <div className="px-4 py-2.5 bg-purple-50 dark:bg-[#1c162e] border border-purple-200/70 dark:border-purple-500/30 rounded-xl flex items-center gap-2.5 shadow-2xs">
              <Gem className="w-4 h-4 text-purple-600 dark:text-purple-400" />
              <div>
                <div className="text-[10px] text-purple-700/80 dark:text-purple-400/80 font-medium">Qi Gems</div>
                <div className="text-sm font-bold text-purple-900 dark:text-purple-200 font-mono">
                  {profile.qiGems}
                </div>
              </div>
            </div>

            <div className="px-4 py-2.5 bg-indigo-50 dark:bg-[#161b2e] border border-indigo-200/70 dark:border-indigo-500/30 rounded-xl flex items-center gap-2.5 shadow-2xs">
              <Award className="w-4 h-4 text-indigo-600 dark:text-indigo-400" />
              <div>
                <div className="text-[10px] text-indigo-700/80 dark:text-indigo-400/80 font-medium">Club Coins</div>
                <div className="text-sm font-bold text-indigo-900 dark:text-indigo-200 font-mono">
                  {profile.clubCoins}
                </div>
              </div>
            </div>

            <button
              onClick={fetchProfile}
              disabled={isLoading}
              className="p-2.5 text-slate-400 hover:text-slate-700 dark:hover:text-amber-300 rounded-xl border border-slate-200 dark:border-[#1e2a42] hover:bg-slate-50 dark:hover:bg-[#162035] transition-colors cursor-pointer"
              title="Perbarui data karakter"
            >
              <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
            </button>
          </div>
        </div>

        {/* HEALTH & STAMINA METRICS */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-6 pt-6 border-t border-slate-100 dark:border-slate-800">
          {/* Stamina / Energy */}
          <div className="p-3.5 bg-slate-50 dark:bg-slate-800/40 border border-slate-200/70 dark:border-slate-700/50 rounded-xl space-y-2">
            <div className="flex items-center justify-between text-xs">
              <span className="font-semibold text-slate-700 dark:text-slate-300 flex items-center gap-1.5">
                <Zap className="w-3.5 h-3.5 text-emerald-500 fill-emerald-500" />
                Energi (Stamina)
              </span>
              <span className="font-mono text-slate-600 dark:text-slate-400">
                {Math.round(profile.stamina)} / {profile.maxStamina}
              </span>
            </div>
            <div className="w-full bg-slate-200 dark:bg-slate-700 h-2.5 rounded-full overflow-hidden">
              <div
                className="bg-emerald-500 h-full rounded-full transition-all duration-500"
                style={{ width: `${staminaPercent}%` }}
              />
            </div>
          </div>

          {/* Health */}
          <div className="p-3.5 bg-slate-50 dark:bg-slate-800/40 border border-slate-200/70 dark:border-slate-700/50 rounded-xl space-y-2">
            <div className="flex items-center justify-between text-xs">
              <span className="font-semibold text-slate-700 dark:text-slate-300 flex items-center gap-1.5">
                <Heart className="w-3.5 h-3.5 text-red-500 fill-red-500" />
                Kesehatan (Health)
              </span>
              <span className="font-mono text-slate-600 dark:text-slate-400">
                {profile.health} / {profile.maxHealth}
              </span>
            </div>
            <div className="w-full bg-slate-200 dark:bg-slate-700 h-2.5 rounded-full overflow-hidden">
              <div
                className="bg-red-500 h-full rounded-full transition-all duration-500"
                style={{ width: `${healthPercent}%` }}
              />
            </div>
          </div>
        </div>
      </div>

      {/* SKILLS & EQUIPMENT GRID */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* SKILLS CARD */}
        <div className="lg:col-span-2 bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 shadow-xs space-y-4 transition-colors">
          <div className="flex items-center gap-2 pb-3 border-b border-slate-100 dark:border-[#1e2a42]">
            <Award className="w-5 h-5 text-amber-500" />
            <h2 className="text-base font-semibold text-slate-900 dark:text-white">
              Tingkat Keahlian (Skills)
            </h2>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
            {Object.entries(profile.skills).map(([skillName, skillData]) => {
              const s = skillName.toLowerCase();
              const label =
                s === 'farming' ? 'Pertanian (Farming)' :
                s === 'fishing' ? 'Memancing (Fishing)' :
                s === 'foraging' ? 'Perambanan (Foraging)' :
                s === 'mining' ? 'Pertambangan (Mining)' :
                s === 'combat' ? 'Pertarungan (Combat)' : 'Keberuntungan (Luck)';

              return (
                <div
                  key={skillName}
                  className="p-3.5 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200/70 dark:border-[#1e2a42] hover:border-amber-500/40 rounded-xl space-y-2 shadow-2xs transition-colors"
                >
                  <div className="flex items-center justify-between text-xs">
                    <span className="font-semibold text-slate-800 dark:text-slate-200">
                      {label}
                    </span>
                    <span className="px-2 py-0.5 font-bold rounded bg-amber-100 text-amber-900 dark:bg-amber-950/80 dark:text-amber-300 border border-amber-200 dark:border-amber-800/40">
                      Lv. {skillData.level}
                    </span>
                  </div>
                  <div className="w-full bg-slate-200 dark:bg-[#162035] h-1.5 rounded-full overflow-hidden">
                    <div
                      className="bg-amber-500 h-full rounded-full"
                      style={{ width: `${Math.min(100, (skillData.level / 10) * 100)}%` }}
                    />
                  </div>
                  <div className="text-[11px] text-slate-400 dark:text-slate-500 font-mono text-right">
                    {skillData.experience.toLocaleString('id-ID')} XP
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* EQUIPMENT CARD */}
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 shadow-xs space-y-4 transition-colors">
          <div className="flex items-center gap-2 pb-3 border-b border-slate-100 dark:border-[#1e2a42]">
            <Shield className="w-5 h-5 text-amber-500" />
            <h2 className="text-base font-semibold text-slate-900 dark:text-white">
              Perlengkapan Aktif
            </h2>
          </div>

          <div className="space-y-2.5 text-xs">
            {profile.equipment.length === 0 ? (
              <p className="text-slate-400 dark:text-slate-500 py-6 text-center">
                Tidak ada perlengkapan yang terpasang saat ini.
              </p>
            ) : (
              profile.equipment.map((eq) => {
                const imgUrl = getItemImageUrl(eq.name);
                const s = eq.slot;
                const slotLabel =
                  s === 'Hat' ? 'Topi' :
                  s === 'Shirt' ? 'Baju' :
                  s === 'Pants' ? 'Celana' :
                  s === 'Boots' ? 'Sepatu Bot' :
                  s === 'LeftRing' ? 'Cincin Kiri' :
                  s === 'RightRing' ? 'Cincin Kanan' : eq.slot;

                return (
                  <div
                    key={eq.slot}
                    className="flex items-center justify-between p-2.5 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200/60 dark:border-[#1e2a42] hover:border-slate-300 dark:hover:border-slate-700 rounded-xl shadow-2xs transition-colors"
                  >
                    <div className="flex items-center gap-3 min-w-0">
                      <div className="w-9 h-9 rounded-lg bg-white dark:bg-[#141e33] border border-slate-200 dark:border-[#1e2a42] flex items-center justify-center p-1 shrink-0 overflow-hidden shadow-2xs">
                        <WikiImage
                          src={imgUrl}
                          alt={eq.name}
                          fallbackText={eq.name}
                          className="w-full h-full object-contain"
                        />
                      </div>
                      <div className="min-w-0">
                        <span className="text-[10px] text-slate-400 dark:text-slate-500 uppercase font-semibold tracking-wider block">
                          {slotLabel}
                        </span>
                        <span className="font-semibold text-slate-800 dark:text-slate-200 text-xs truncate block" title={eq.name}>
                          {eq.name}
                        </span>
                      </div>
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>
      </div>

      {/* BACKPACK INVENTORY (36 SLOTS) */}
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 shadow-xs space-y-4 transition-colors">
        <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-[#1e2a42]">
          <div className="flex items-center gap-2">
            <Package className="w-5 h-5 text-amber-500" />
            <h2 className="text-base font-semibold text-slate-900 dark:text-white">
              Isi Ransel Petani (Backpack Inventory)
            </h2>
          </div>
          <span className="text-xs text-slate-400 dark:text-slate-500">
            {profile.inventory.length} / 36 Slot Terisi
          </span>
        </div>

        {/* 3 Rows of 12 Slots Grid */}
        <div className="grid grid-cols-4 sm:grid-cols-6 md:grid-cols-12 gap-2">
          {slots.map((item, idx) => {
            if (!item) {
              return (
                <div
                  key={idx}
                  className="aspect-square rounded-xl border border-dashed border-slate-200 dark:border-[#1e2a42] bg-slate-50/50 dark:bg-[#0a0f1d]/50 flex items-center justify-center text-[10px] text-slate-300 dark:text-slate-600 font-mono select-none"
                >
                  {idx + 1}
                </div>
              );
            }

            const itemImg = getItemImageUrl(item.name) || getItemImageUrl(item.displayName);

            return (
              <div
                key={idx}
                className="aspect-square relative rounded-xl border border-slate-200 dark:border-[#1e2a42] bg-slate-50 dark:bg-[#0a0f1d] hover:border-amber-500/70 dark:hover:border-amber-500/70 p-1 flex flex-col items-center justify-between group cursor-default shadow-2xs transition-all hover:shadow-xs"
                title={`${item.displayName} (${item.categoryName || 'Item'})\nJumlah: ${item.stack}`}
              >
                {/* Header: Slot Index & Quality Star */}
                <div className="w-full flex items-center justify-between pointer-events-none z-10 leading-none px-0.5">
                  <span className="text-[9px] font-mono text-slate-400 dark:text-slate-500 font-medium">
                    {idx + 1}
                  </span>
                  {item.quality > 0 && (
                    <span
                      className={`w-2 h-2 rounded-full ring-1 ring-white dark:ring-slate-900 ${
                        item.quality === 1
                          ? 'bg-slate-400'
                          : item.quality === 2
                          ? 'bg-amber-400'
                          : 'bg-purple-500'
                      }`}
                      title={
                        item.quality === 1
                          ? 'Kualitas Perak'
                          : item.quality === 2
                          ? 'Kualitas Emas'
                          : 'Kualitas Iridium'
                      }
                    />
                  )}
                </div>

                {/* Center: Real Item Sprite */}
                <div className="flex-1 w-full flex items-center justify-center p-0.5 min-h-0">
                  <WikiImage
                    src={itemImg}
                    alt={item.displayName}
                    fallbackText={item.displayName}
                    className="w-7 h-7 sm:w-8 sm:h-8 object-contain drop-shadow-2xs group-hover:scale-110 transition-transform duration-150"
                  />
                </div>

                {/* Bottom Bar: Stack Count Badge & Item Name */}
                <div className="w-full flex items-end justify-between leading-none px-0.5 z-10">
                  <span className="text-[9px] text-slate-500 dark:text-slate-400 truncate max-w-[70%] font-medium hidden sm:inline-block">
                    {item.displayName}
                  </span>
                  {item.stack > 1 ? (
                    <span className="ml-auto px-1 py-0.5 bg-slate-900/80 dark:bg-slate-950/90 text-white rounded font-mono text-[9px] font-bold shadow-2xs leading-none">
                      {item.stack}
                    </span>
                  ) : (
                    <span className="sm:hidden" />
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
