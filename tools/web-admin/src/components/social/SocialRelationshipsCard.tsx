import React, { useState, useEffect, useMemo } from 'react';
import {
  Heart,
  Gift,
  MessageSquare,
  Search,
  Sparkles,
  Calendar,
  CheckCircle2,
  Clock,
  RefreshCw,
  AlertCircle,
  Users,
  Award,
  Filter,
} from 'lucide-react';
import { api } from '../../lib/api';
import { FarmerSocialResponse } from '../../types/social';
import { getNpcPortraitUrl } from '../../lib/wikiAssets';
import { WikiImage } from '../common/WikiImage';

export const SocialRelationshipsCard: React.FC = () => {
  const [socialData, setSocialData] = useState<FarmerSocialResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [filterType, setFilterType] = useState<'all' | 'datable' | 'notMax' | 'birthdaySeason' | 'needTalk'>('all');
  const [sortBy, setSortBy] = useState<'heartsDesc' | 'heartsAsc' | 'name' | 'giftsRemaining'>('heartsDesc');

  const fetchSocial = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.farmer.getSocial();
      setSocialData(data);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Gagal memuat data hubungan sosial.';
      setError(message);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchSocial();
    const interval = setInterval(fetchSocial, 15000);
    return () => clearInterval(interval);
  }, []);

  const birthdayTodayList = useMemo(() => {
    if (!socialData?.villagers) return [];
    return socialData.villagers.filter((v) => v.isBirthdayToday);
  }, [socialData]);

  const filteredVillagers = useMemo(() => {
    if (!socialData?.villagers) return [];

    let list = [...socialData.villagers];

    // Search query
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase().trim();
      list = list.filter(
        (v) => v.displayName.toLowerCase().includes(q) || v.name.toLowerCase().includes(q)
      );
    }

    // Filter type
    if (filterType === 'datable') {
      list = list.filter((v) => v.isDatable);
    } else if (filterType === 'notMax') {
      list = list.filter((v) => v.hearts < v.maxHearts);
    } else if (filterType === 'needTalk') {
      list = list.filter((v) => !v.talkedToToday);
    }

    // Sorting
    list.sort((a, b) => {
      if (sortBy === 'heartsDesc') {
        return b.points - a.points || a.displayName.localeCompare(b.displayName);
      }
      if (sortBy === 'heartsAsc') {
        return a.points - b.points || a.displayName.localeCompare(b.displayName);
      }
      if (sortBy === 'name') {
        return a.displayName.localeCompare(b.displayName);
      }
      if (sortBy === 'giftsRemaining') {
        const remA = a.maxGiftsPerWeek - a.giftsThisWeek;
        const remB = b.maxGiftsPerWeek - b.giftsThisWeek;
        return remB - remA || b.points - a.points;
      }
      return 0;
    });

    return list;
  }, [socialData, searchQuery, filterType, sortBy]);

  const renderHeartMeter = (hearts: number, maxHearts: number) => {
    return (
      <div className="flex items-center gap-1 flex-wrap">
        {Array.from({ length: maxHearts }).map((_, i) => {
          const isFilled = i < hearts;
          return (
            <Heart
              key={i}
              className={`w-3.5 h-3.5 transition-colors ${
                isFilled
                  ? 'fill-rose-500 text-rose-500'
                  : 'text-slate-300 dark:text-slate-700'
              }`}
            />
          );
        })}
      </div>
    );
  };

  if (isLoading && !socialData) {
    return (
      <div className="flex flex-col items-center justify-center p-12 bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl shadow-xs text-slate-400">
        <RefreshCw className="w-8 h-8 animate-spin mb-3 text-rose-500" />
        <span className="text-sm font-medium">Memuat data hubungan sosial penduduk...</span>
      </div>
    );
  }

  if (error && !socialData) {
    return (
      <div className="p-6 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900 rounded-2xl text-center space-y-3">
        <AlertCircle className="w-8 h-8 text-red-500 mx-auto" />
        <p className="text-sm text-red-700 dark:text-red-400">{error}</p>
        <button
          onClick={fetchSocial}
          className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-xl text-xs font-medium transition-colors"
        >
          Coba Lagi
        </button>
      </div>
    );
  }

  if (!socialData) return null;

  return (
    <div className="space-y-6">
      {/* BANNER: BIRTHDAY TODAY ALERT */}
      {birthdayTodayList.length > 0 && (
        <div className="p-4 bg-gradient-to-r from-amber-500/10 via-rose-500/10 to-purple-500/10 border border-amber-500/30 dark:border-amber-500/20 rounded-2xl flex items-center gap-4">
          <div className="w-10 h-10 rounded-xl bg-amber-500/20 text-amber-600 dark:text-amber-400 flex items-center justify-center shrink-0">
            <Gift className="w-5 h-5" />
          </div>
          <div className="flex-1">
            <div className="text-sm font-semibold text-slate-900 dark:text-white flex items-center gap-2">
              <span>Ulang Tahun Hari Ini!</span>
              <span className="text-xs px-2 py-0.5 rounded-full bg-amber-500/20 text-amber-700 dark:text-amber-300 font-medium">
                Poin Hadiah 8x
              </span>
            </div>
            <p className="text-xs text-slate-600 dark:text-slate-400 mt-0.5">
              {birthdayTodayList.map((v) => v.displayName).join(', ')} sedang berulang tahun hari ini. Berikan hadiah kesukaannya untuk bonus persahabatan besar!
            </p>
          </div>
        </div>
      )}

      {/* HEADER SUMMARY METRICS */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-5 shadow-xs flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-rose-500/10 text-rose-600 dark:text-rose-400 flex items-center justify-center shrink-0">
            <Users className="w-6 h-6" />
          </div>
          <div>
            <span className="text-xs font-medium text-slate-500 dark:text-slate-400">Total Dikenal</span>
            <div className="text-2xl font-bold text-slate-900 dark:text-white">
              {socialData.totalVillagersMet}
              <span className="text-xs font-normal text-slate-400 ml-1">penduduk</span>
            </div>
          </div>
        </div>

        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-5 shadow-xs flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-amber-500/10 text-amber-600 dark:text-amber-400 flex items-center justify-center shrink-0">
            <Award className="w-6 h-6" />
          </div>
          <div>
            <span className="text-xs font-medium text-slate-500 dark:text-slate-400">Hati Maksimal</span>
            <div className="text-2xl font-bold text-slate-900 dark:text-white">
              {socialData.maxHeartsAchieved}
              <span className="text-xs font-normal text-slate-400 ml-1">penduduk</span>
            </div>
          </div>
        </div>

        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-5 shadow-xs flex items-center gap-4">
          <div className="w-12 h-12 rounded-xl bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 flex items-center justify-center shrink-0">
            <Sparkles className="w-6 h-6" />
          </div>
          <div>
            <span className="text-xs font-medium text-slate-500 dark:text-slate-400">Petani Aktif</span>
            <div className="text-base font-bold text-slate-900 dark:text-white truncate max-w-[180px]">
              {socialData.farmerName}
            </div>
          </div>
        </div>
      </div>

      {/* FILTER & SEARCH TOOLBAR */}
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-4 shadow-xs flex flex-col md:flex-row gap-4 justify-between items-center">
        {/* Search Input */}
        <div className="relative w-full md:w-72">
          <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" />
          <input
            type="text"
            placeholder="Cari nama penduduk..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full pl-9 pr-4 py-2 text-xs bg-slate-50 dark:bg-[#162035] border border-slate-200 dark:border-[#1e2a42] rounded-xl text-slate-900 dark:text-white focus:outline-none focus:ring-1 focus:ring-rose-500 transition-colors"
          />
        </div>

        {/* Filter Buttons */}
        <div className="flex items-center gap-1.5 flex-wrap w-full md:w-auto">
          <button
            onClick={() => setFilterType('all')}
            className={`px-3 py-1.5 text-xs font-medium rounded-xl transition-colors ${
              filterType === 'all'
                ? 'bg-rose-500 text-white'
                : 'bg-slate-100 dark:bg-[#162035] text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-[#1e2a42]'
            }`}
          >
            Semua ({socialData.villagers.length})
          </button>
          <button
            onClick={() => setFilterType('datable')}
            className={`px-3 py-1.5 text-xs font-medium rounded-xl transition-colors ${
              filterType === 'datable'
                ? 'bg-rose-500 text-white'
                : 'bg-slate-100 dark:bg-[#162035] text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-[#1e2a42]'
            }`}
          >
            Bisa Dinikahi
          </button>
          <button
            onClick={() => setFilterType('notMax')}
            className={`px-3 py-1.5 text-xs font-medium rounded-xl transition-colors ${
              filterType === 'notMax'
                ? 'bg-rose-500 text-white'
                : 'bg-slate-100 dark:bg-[#162035] text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-[#1e2a42]'
            }`}
          >
            Belum Maksimal
          </button>
          <button
            onClick={() => setFilterType('needTalk')}
            className={`px-3 py-1.5 text-xs font-medium rounded-xl transition-colors ${
              filterType === 'needTalk'
                ? 'bg-rose-500 text-white'
                : 'bg-slate-100 dark:bg-[#162035] text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-[#1e2a42]'
            }`}
          >
            Belum Disapa
          </button>
        </div>

        {/* Sort Select & Refresh */}
        <div className="flex items-center gap-2 w-full md:w-auto justify-end">
          <div className="flex items-center gap-1.5 text-xs text-slate-500 dark:text-slate-400">
            <Filter className="w-3.5 h-3.5 text-slate-400" />
            <select
              value={sortBy}
              onChange={(e) => setSortBy(e.target.value as any)}
              className="bg-slate-50 dark:bg-[#162035] border border-slate-200 dark:border-[#1e2a42] rounded-xl px-2.5 py-1.5 text-xs text-slate-800 dark:text-slate-200 focus:outline-none"
            >
              <option value="heartsDesc">Hati Tertinggi</option>
              <option value="heartsAsc">Hati Terendah</option>
              <option value="giftsRemaining">Sisa Kuota Hadiah</option>
              <option value="name">Nama (A-Z)</option>
            </select>
          </div>

          <button
            onClick={fetchSocial}
            disabled={isLoading}
            className="p-2 text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-white bg-slate-100 dark:bg-[#162035] hover:bg-slate-200 dark:hover:bg-[#1e2a42] rounded-xl transition-colors"
            title="Muat Ulang"
          >
            <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin text-rose-500' : ''}`} />
          </button>
        </div>
      </div>

      {/* VILLAGER RELATIONSHIPS GRID */}
      {filteredVillagers.length === 0 ? (
        <div className="p-12 text-center bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl text-slate-400">
          <Users className="w-8 h-8 mx-auto mb-2 opacity-40" />
          <p className="text-sm font-medium">Tidak ada data penduduk yang cocok dengan filter.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {filteredVillagers.map((v) => {
            const giftsLeft = Math.max(0, v.maxGiftsPerWeek - v.giftsThisWeek);
            const isGiftQuotaFull = giftsLeft === 0;

            return (
              <div
                key={v.name}
                className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-5 shadow-xs flex flex-col justify-between hover:border-rose-400/50 dark:hover:border-rose-500/40 transition-colors"
              >
                {/* Top Section: Avatar, Name, Status */}
                <div className="flex items-start gap-4">
                  <div className="relative shrink-0">
                    <WikiImage
                      src={getNpcPortraitUrl(v.name)}
                      alt={v.displayName}
                      fallbackText={v.displayName}
                      className="w-14 h-14 rounded-2xl bg-slate-100 dark:bg-[#162035] border border-slate-200 dark:border-[#1e2a42] object-contain p-1"
                    />
                    {v.isBirthdayToday && (
                      <div
                        className="absolute -top-1.5 -right-1.5 w-5 h-5 rounded-full bg-amber-500 text-white flex items-center justify-center shadow-xs"
                        title="Ulang tahun hari ini!"
                      >
                        <Gift className="w-3 h-3" />
                      </div>
                    )}
                  </div>

                  <div className="flex-1 min-w-0">
                    <div className="flex items-center justify-between gap-1">
                      <h2 className="text-sm font-bold text-slate-900 dark:text-white truncate">
                        {v.displayName}
                      </h2>
                      {v.isDatable && (
                        <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-rose-500/10 text-rose-600 dark:text-rose-400 border border-rose-500/20 shrink-0">
                          {v.status === 'Married' ? 'Pasangan' : 'Bisa Dinikahi'}
                        </span>
                      )}
                    </div>

                    {/* Birthday & Status Info */}
                    <div className="flex items-center gap-2 mt-1 text-[11px] text-slate-500 dark:text-slate-400">
                      {v.birthdaySeason && (
                        <div className="flex items-center gap-1">
                          <Calendar className="w-3 h-3 text-slate-400" />
                          <span>
                            {v.birthdaySeason} {v.birthdayDay}
                          </span>
                        </div>
                      )}
                      <span>•</span>
                      <span>{v.status || 'Teman'}</span>
                    </div>

                    {/* Heart Meter */}
                    <div className="mt-3">
                      <div className="flex items-center justify-between text-[11px] mb-1">
                        <span className="font-semibold text-slate-700 dark:text-slate-300">
                          {v.hearts} / {v.maxHearts} Hati
                        </span>
                        <span className="text-slate-400 text-[10px]">
                          {v.points} pts
                        </span>
                      </div>
                      {renderHeartMeter(v.hearts, v.maxHearts)}
                    </div>
                  </div>
                </div>

                {/* Bottom Status: Weekly Gift Allowance & Daily Talk */}
                <div className="mt-4 pt-3 border-t border-slate-100 dark:border-[#162035] grid grid-cols-2 gap-2 text-[11px]">
                  {/* Weekly Gift Quota */}
                  <div className="flex items-center gap-1.5">
                    <Gift className={`w-3.5 h-3.5 ${isGiftQuotaFull ? 'text-amber-500' : 'text-slate-400'}`} />
                    <div>
                      <div className="text-[10px] text-slate-400">Hadiah Minggu Ini</div>
                      <div className="font-medium text-slate-700 dark:text-slate-300">
                        {v.giftsThisWeek} / {v.maxGiftsPerWeek}
                        {isGiftQuotaFull ? (
                          <span className="ml-1 text-[10px] text-amber-500">(Penuh)</span>
                        ) : (
                          <span className="ml-1 text-[10px] text-emerald-500">({giftsLeft} lagi)</span>
                        )}
                      </div>
                    </div>
                  </div>

                  {/* Daily Interaction */}
                  <div className="flex items-center gap-1.5 justify-end">
                    <MessageSquare className={`w-3.5 h-3.5 ${v.talkedToToday ? 'text-emerald-500' : 'text-slate-400'}`} />
                    <div className="text-right">
                      <div className="text-[10px] text-slate-400">Sapaan Hari Ini</div>
                      <div className="font-medium flex items-center gap-1 justify-end">
                        {v.talkedToToday ? (
                          <span className="text-emerald-600 dark:text-emerald-400 flex items-center gap-0.5">
                            <CheckCircle2 className="w-3 h-3" /> Sudah
                          </span>
                        ) : (
                          <span className="text-slate-400 dark:text-slate-500 flex items-center gap-0.5">
                            <Clock className="w-3 h-3" /> Belum (+20p)
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
