import React, { useState, useEffect } from 'react';
import {
  Scroll,
  Building2,
  CheckCircle2,
  Circle,
  Clock,
  Coins,
  Sparkles,
  RefreshCw,
  AlertCircle,
  HelpCircle,
  Calendar,
  ChevronRight,
  Package,
} from 'lucide-react';
import { api } from '../../lib/api';
import {
  FarmerQuestsResponse,
  CommunityCenterResponse,
} from '../../types/quests';
import {
  getRoomImageUrl,
  getBundleImageUrl,
  getItemImageUrl,
  getJojaProjectImageUrl,
  getNpcPortraitUrl,
} from '../../lib/wikiAssets';
import { WikiImage } from '../common/WikiImage';

export const QuestsAndCommunityCard: React.FC = () => {
  const [activeSubTab, setActiveSubTab] = useState<'community' | 'quests'>('community');
  const [communityData, setCommunityData] = useState<CommunityCenterResponse | null>(null);
  const [questsData, setQuestsData] = useState<FarmerQuestsResponse | null>(null);
  const [selectedRoomIndex, setSelectedRoomIndex] = useState<number | 'all'>('all');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchData = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const [ccRes, questsRes] = await Promise.all([
        api.farmer.getCommunityCenter(),
        api.farmer.getQuests(),
      ]);
      setCommunityData(ccRes);
      setQuestsData(questsRes);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Gagal memuat data misi & balai kota.';
      setError(message);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
    const interval = setInterval(fetchData, 15000);
    return () => clearInterval(interval);
  }, []);

  const getQualityBadge = (quality: number) => {
    if (quality === 1) {
      return (
        <span className="text-[10px] font-semibold px-1.5 py-0.2 rounded bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300">
          Perak
        </span>
      );
    }
    if (quality === 2) {
      return (
        <span className="text-[10px] font-semibold px-1.5 py-0.2 rounded bg-amber-500/20 text-amber-700 dark:text-amber-400">
          Emas
        </span>
      );
    }
    if (quality === 4) {
      return (
        <span className="text-[10px] font-semibold px-1.5 py-0.2 rounded bg-purple-500/20 text-purple-700 dark:text-purple-300">
          Iridium
        </span>
      );
    }
    return null;
  };

  if (isLoading && !communityData && !questsData) {
    return (
      <div className="flex flex-col items-center justify-center p-12 bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl shadow-xs text-slate-400">
        <RefreshCw className="w-8 h-8 animate-spin mb-3 text-emerald-500" />
        <span className="text-sm font-medium">Memuat data misi dan balai kota...</span>
      </div>
    );
  }

  if (error && !communityData && !questsData) {
    return (
      <div className="p-6 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900 rounded-2xl text-center space-y-3">
        <AlertCircle className="w-8 h-8 text-red-500 mx-auto" />
        <p className="text-sm text-red-700 dark:text-red-400">{error}</p>
        <button
          onClick={fetchData}
          className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-xl text-xs font-medium transition-colors"
        >
          Coba Lagi
        </button>
      </div>
    );
  }

  const filteredRooms = communityData?.rooms.filter(
    (room) => selectedRoomIndex === 'all' || room.roomIndex === selectedRoomIndex
  ) || [];

  const ccCompletionPercent = communityData?.totalBundles
    ? Math.round((communityData.completedBundles / communityData.totalBundles) * 100)
    : 0;

  return (
    <div className="space-y-6">
      {/* TOP TOGGLE: BALAI KOTA vs MISI PRIBADI */}
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-4 shadow-xs flex flex-col sm:flex-row items-center justify-between gap-4">
        <div className="flex items-center gap-2 w-full sm:w-auto">
          <button
            onClick={() => setActiveSubTab('community')}
            className={`flex-1 sm:flex-none flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl text-xs font-semibold transition-all ${
              activeSubTab === 'community'
                ? 'bg-emerald-600 text-white shadow-xs'
                : 'bg-slate-100 dark:bg-[#162035] text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-[#1e2a42]'
            }`}
          >
            <Building2 className="w-4 h-4" />
            <span>Balai Kota & Bundel</span>
            {communityData && (
              <span className="ml-1 px-1.5 py-0.5 rounded-full text-[10px] bg-emerald-700/50 text-white">
                {ccCompletionPercent}%
              </span>
            )}
          </button>

          <button
            onClick={() => setActiveSubTab('quests')}
            className={`flex-1 sm:flex-none flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl text-xs font-semibold transition-all ${
              activeSubTab === 'quests'
                ? 'bg-emerald-600 text-white shadow-xs'
                : 'bg-slate-100 dark:bg-[#162035] text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-[#1e2a42]'
            }`}
          >
            <Scroll className="w-4 h-4" />
            <span>Misi & Pesanan</span>
            {questsData && (
              <span className="ml-1 px-1.5 py-0.5 rounded-full text-[10px] bg-emerald-700/50 text-white">
                {(questsData.activeQuests?.length || 0) + (questsData.specialOrders?.length || 0)}
              </span>
            )}
          </button>
        </div>

        <button
          onClick={fetchData}
          disabled={isLoading}
          className="p-2 text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-white bg-slate-100 dark:bg-[#162035] hover:bg-slate-200 dark:hover:bg-[#1e2a42] rounded-xl transition-colors self-end sm:self-auto"
          title="Muat Ulang Data"
        >
          <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin text-emerald-500' : ''}`} />
        </button>
      </div>

      {/* SUB-TAB 1: BALAI KOTA / COMMUNITY CENTER */}
      {activeSubTab === 'community' && communityData && (
        <div className="space-y-6">
          {/* OVERVIEW PROGRESS CARD */}
          <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 shadow-xs">
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div className="space-y-1">
                <div className="flex items-center gap-2">
                  <h1 className="text-lg font-bold text-slate-900 dark:text-white">
                    {communityData.isJojaRoute
                      ? 'Formulir Proyek Pembangunan Komunitas Joja'
                      : 'Progres Pemugaran Balai Kota (Community Center)'}
                  </h1>
                  <span
                    className={`text-[10px] font-semibold px-2.5 py-0.5 rounded-full ${
                      communityData.isJojaRoute
                        ? 'bg-blue-500/10 text-blue-600 dark:text-blue-400 border border-blue-500/20'
                        : 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-500/20'
                    }`}
                  >
                    {communityData.isJojaRoute ? 'Rute Joja' : 'Rute Junimo'}
                  </span>
                </div>
                <p className="text-xs text-slate-500 dark:text-slate-400">
                  {communityData.isComplete
                    ? 'Selamat! Balai Kota telah sepenuhnya dipugar dan selesai!'
                    : `Selesaikan bundel persembahan Junimo untuk memulihkan ruangan dan membuka fitur baru.`}
                </p>
              </div>

              {/* Progress Count */}
              <div className="text-right">
                <div className="text-2xl font-bold text-emerald-600 dark:text-emerald-400">
                  {communityData.completedBundles} / {communityData.totalBundles}
                </div>
                <div className="text-[11px] text-slate-400">Bundel Selesai ({ccCompletionPercent}%)</div>
              </div>
            </div>

            {/* Progress bar */}
            <div className="w-full h-2.5 bg-slate-100 dark:bg-[#162035] rounded-full overflow-hidden mt-4 border border-slate-200/50 dark:border-slate-800">
              <div
                className="h-full bg-emerald-500 transition-all duration-500 rounded-full"
                style={{ width: `${ccCompletionPercent}%` }}
              />
            </div>

            {/* Room Filter Pills */}
            {!communityData.isJojaRoute && (
              <div className="flex items-center gap-2 overflow-x-auto pt-5 border-t border-slate-100 dark:border-[#162035] mt-5">
                <button
                  onClick={() => setSelectedRoomIndex('all')}
                  className={`px-3 py-1.5 text-xs font-medium rounded-xl whitespace-nowrap transition-colors ${
                    selectedRoomIndex === 'all'
                      ? 'bg-emerald-600 text-white'
                      : 'bg-slate-100 dark:bg-[#162035] text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-[#1e2a42]'
                  }`}
                >
                  Semua Ruangan ({communityData.rooms.length})
                </button>
                {communityData.rooms.map((room) => {
                  const isRoomDone = room.isComplete;
                  return (
                    <button
                      key={room.roomIndex}
                      onClick={() => setSelectedRoomIndex(room.roomIndex)}
                      className={`flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-xl whitespace-nowrap transition-colors ${
                        selectedRoomIndex === room.roomIndex
                          ? 'bg-emerald-600 text-white'
                          : 'bg-slate-100 dark:bg-[#162035] text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-[#1e2a42]'
                      }`}
                    >
                      <span>{room.displayName || room.name}</span>
                      {isRoomDone ? (
                        <CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" />
                      ) : (
                        <span className="text-[10px] text-slate-400">
                          {room.completedBundles}/{room.totalBundles}
                        </span>
                      )}
                    </button>
                  );
                })}
              </div>
            )}
          </div>

          {/* IF JOJA ROUTE: LIST PROJECTS */}
          {communityData.isJojaRoute && (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {communityData.jojaProjects.map((proj) => (
                <div
                  key={proj.key}
                  className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-5 shadow-xs flex flex-col justify-between"
                >
                  <div className="flex items-start gap-4">
                    <WikiImage
                      src={getJojaProjectImageUrl(proj.key)}
                      alt={proj.name}
                      fallbackIcon={<Building2 className="w-6 h-6" />}
                      className="w-14 h-14 rounded-2xl bg-blue-500/10 border border-blue-500/20 shrink-0 p-1"
                    />
                    <div>
                      <h2 className="text-sm font-bold text-slate-900 dark:text-white">{proj.name}</h2>
                      <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">{proj.description}</p>
                    </div>
                  </div>

                  <div className="mt-4 pt-3 border-t border-slate-100 dark:border-[#162035] flex items-center justify-between">
                    <div className="flex items-center gap-1 text-xs font-semibold text-amber-600 dark:text-amber-400">
                      <Coins className="w-3.5 h-3.5" />
                      <span>{proj.cost.toLocaleString()} g</span>
                    </div>
                    {proj.isComplete ? (
                      <span className="flex items-center gap-1 text-xs font-semibold text-emerald-600 dark:text-emerald-400">
                        <CheckCircle2 className="w-3.5 h-3.5" /> Selesai Dibeli
                      </span>
                    ) : (
                      <span className="text-xs text-slate-400">Belum Selesai</span>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* ROOMS & BUNDLES DISPLAY */}
          {!communityData.isJojaRoute && (
            <div className="space-y-6">
              {filteredRooms.map((room) => (
                <div
                  key={room.roomIndex}
                  className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 shadow-xs space-y-4"
                >
                  {/* Room Header */}
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-slate-100 dark:border-[#162035]">
                    <div className="flex items-center gap-3">
                      <WikiImage
                        src={getRoomImageUrl(room.name)}
                        alt={room.displayName}
                        fallbackIcon={<Building2 className="w-5 h-5" />}
                        className="w-10 h-10 rounded-xl bg-slate-100 dark:bg-[#162035] border border-slate-200 dark:border-[#1e2a42] p-1 shrink-0"
                      />
                      <div>
                        <div className="flex items-center gap-2">
                          <h2 className="text-base font-bold text-slate-900 dark:text-white">
                            {room.displayName || room.name}
                          </h2>
                          {room.isComplete && (
                            <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-500/20">
                              Pulih Sepenuhnya
                            </span>
                          )}
                        </div>
                        {room.reward && (
                          <div className="text-xs text-slate-500 dark:text-slate-400 flex items-center gap-1 mt-0.5">
                            <Sparkles className="w-3 h-3 text-amber-500" />
                            <span>Hadiah Ruangan: {room.reward}</span>
                          </div>
                        )}
                      </div>
                    </div>

                    <div className="text-xs font-semibold text-slate-600 dark:text-slate-300">
                      {room.completedBundles} dari {room.totalBundles} Bundel Selesai
                    </div>
                  </div>

                  {/* Bundles Grid inside Room */}
                  <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4 pt-2">
                    {room.bundles.map((bundle) => {
                      const isComplete = bundle.isComplete;
                      const progressPercent = bundle.requiredItemsCount
                        ? Math.min(100, Math.round((bundle.fulfilledCount / bundle.requiredItemsCount) * 100))
                        : 0;

                      return (
                        <div
                          key={bundle.bundleIndex}
                          className={`rounded-2xl p-4 border transition-colors flex flex-col justify-between ${
                            isComplete
                              ? 'bg-emerald-500/5 dark:bg-emerald-950/10 border-emerald-500/30 dark:border-emerald-500/20'
                              : 'bg-slate-50 dark:bg-[#162035] border-slate-200 dark:border-[#1e2a42]'
                          }`}
                        >
                          <div>
                            {/* Bundle Title & Reward */}
                            <div className="flex items-start gap-3">
                              <WikiImage
                                src={getBundleImageUrl(bundle.name)}
                                alt={bundle.name}
                                fallbackIcon={<Scroll className="w-5 h-5 text-emerald-500" />}
                                className="w-10 h-10 rounded-xl bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] p-1 shrink-0"
                              />
                              <div className="flex-1 min-w-0">
                                <div className="flex items-center justify-between gap-1">
                                  <h3 className="text-xs font-bold text-slate-900 dark:text-white truncate">
                                    {bundle.name}
                                  </h3>
                                  {isComplete ? (
                                    <span className="text-[10px] font-semibold text-emerald-600 dark:text-emerald-400 shrink-0 flex items-center gap-0.5">
                                      <CheckCircle2 className="w-3 h-3" /> Selesai
                                    </span>
                                  ) : (
                                    <span className="text-[10px] text-slate-400 shrink-0">
                                      {bundle.fulfilledCount}/{bundle.requiredItemsCount}
                                    </span>
                                  )}
                                </div>

                                {bundle.rewardName && (
                                  <div className="text-[11px] text-slate-500 dark:text-slate-400 truncate mt-0.5">
                                    Hadiah: {bundle.rewardCount}x {bundle.rewardName}
                                  </div>
                                )}
                              </div>
                            </div>

                            {/* Progress bar */}
                            <div className="w-full h-1.5 bg-slate-200 dark:bg-[#111a2e] rounded-full overflow-hidden mt-3">
                              <div
                                className={`h-full rounded-full transition-all duration-300 ${
                                  isComplete ? 'bg-emerald-500' : 'bg-amber-500'
                                }`}
                                style={{ width: `${progressPercent}%` }}
                              />
                            </div>

                            {/* Ingredients Checklist */}
                            <div className="mt-3 space-y-1.5">
                              {bundle.ingredients.map((ing, i) => (
                                <div
                                  key={i}
                                  className={`flex items-center justify-between p-1.5 rounded-xl text-xs transition-colors ${
                                    ing.isFulfilled
                                      ? 'bg-emerald-500/10 text-slate-700 dark:text-slate-300'
                                      : 'bg-white dark:bg-[#111a2e] text-slate-500 dark:text-slate-400 border border-slate-100 dark:border-[#1e2a42]'
                                  }`}
                                >
                                  <div className="flex items-center gap-2 min-w-0">
                                    <WikiImage
                                      src={getItemImageUrl(ing.name)}
                                      alt={ing.name}
                                      fallbackIcon={<Package className="w-3 h-3" />}
                                      className="w-5 h-5 rounded-md shrink-0"
                                    />
                                    <span className="truncate text-[11px] font-medium">
                                      {ing.stack > 1 ? `${ing.stack}x ` : ''}
                                      {ing.name}
                                    </span>
                                    {getQualityBadge(ing.quality)}
                                  </div>

                                  <div className="shrink-0 ml-1">
                                    {ing.isFulfilled ? (
                                      <CheckCircle2 className="w-3.5 h-3.5 text-emerald-500" />
                                    ) : (
                                      <Circle className="w-3.5 h-3.5 text-slate-300 dark:text-slate-600" />
                                    )}
                                  </div>
                                </div>
                              ))}
                            </div>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* SUB-TAB 2: MISI & PESANAN PRIBADI */}
      {activeSubTab === 'quests' && questsData && (
        <div className="space-y-6">
          {/* SECTION 1: ACTIVE QUESTS */}
          <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 shadow-xs space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-[#162035]">
              <div className="flex items-center gap-2">
                <Scroll className="w-5 h-5 text-amber-500" />
                <h2 className="text-base font-bold text-slate-900 dark:text-white">
                  Buku Catatan Misi ({questsData.activeQuests?.length || 0})
                </h2>
              </div>
              <span className="text-xs text-slate-400">
                Misi Help Wanted & Alur Cerita
              </span>
            </div>

            {(!questsData.activeQuests || questsData.activeQuests.length === 0) ? (
              <div className="p-8 text-center text-slate-400">
                <Scroll className="w-8 h-8 mx-auto mb-2 opacity-40" />
                <p className="text-xs font-medium">Tidak ada misi aktif saat ini. Periksa papan buletin Pierre!</p>
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {questsData.activeQuests.map((quest) => (
                  <div
                    key={quest.id}
                    className="p-4 bg-slate-50 dark:bg-[#162035] border border-slate-200 dark:border-[#1e2a42] rounded-2xl flex flex-col justify-between space-y-3"
                  >
                    <div>
                      <div className="flex items-start justify-between gap-2">
                        <h3 className="text-xs font-bold text-slate-900 dark:text-white">
                          {quest.title}
                        </h3>
                        {quest.isDailyQuest && (
                          <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-500/20 shrink-0">
                            Harian
                          </span>
                        )}
                      </div>

                      <p className="text-xs text-slate-600 dark:text-slate-400 mt-1 leading-relaxed">
                        {quest.description}
                      </p>

                      {quest.currentObjective && (
                        <div className="mt-2 text-xs font-medium text-slate-700 dark:text-slate-300 flex items-center gap-1.5 bg-white dark:bg-[#111a2e] p-2 rounded-xl border border-slate-200 dark:border-[#1e2a42]">
                          <ChevronRight className="w-3.5 h-3.5 text-amber-500 shrink-0" />
                          <span>{quest.currentObjective}</span>
                        </div>
                      )}
                    </div>

                    <div className="pt-2 border-t border-slate-200 dark:border-[#1e2a42] flex items-center justify-between text-xs">
                      <div className="flex items-center gap-1 text-amber-600 dark:text-amber-400 font-semibold">
                        <Coins className="w-3.5 h-3.5" />
                        <span>{quest.rewardMoney > 0 ? `${quest.rewardMoney} g` : 'Poin Persahabatan'}</span>
                      </div>

                      <div className="flex items-center gap-1 text-slate-400 text-[11px]">
                        <Clock className="w-3 h-3" />
                        <span>
                          {quest.daysLeft > 0 ? `${quest.daysLeft} hari tersisa` : 'Tanpa batas'}
                        </span>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* SECTION 2: SPECIAL ORDERS */}
          <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 shadow-xs space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-[#162035]">
              <div className="flex items-center gap-2">
                <Sparkles className="w-5 h-5 text-indigo-500" />
                <h2 className="text-base font-bold text-slate-900 dark:text-white">
                  Pesanan Khusus Komunitas & Qi ({questsData.specialOrders?.length || 0})
                </h2>
              </div>
              <span className="text-xs text-slate-400">
                Papan Pesanan Khusus Kota & Ruang Qi
              </span>
            </div>

            {(!questsData.specialOrders || questsData.specialOrders.length === 0) ? (
              <div className="p-8 text-center text-slate-400">
                <HelpCircle className="w-8 h-8 mx-auto mb-2 opacity-40" />
                <p className="text-xs font-medium">Belum ada pesanan khusus yang sedang berjalan.</p>
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {questsData.specialOrders.map((order) => (
                  <div
                    key={order.key}
                    className="p-5 bg-slate-50 dark:bg-[#162035] border border-slate-200 dark:border-[#1e2a42] rounded-2xl flex flex-col justify-between space-y-4"
                  >
                    <div>
                      <div className="flex items-start justify-between gap-3">
                        <div className="flex items-center gap-2.5">
                          {order.requester && (
                            <WikiImage
                              src={getNpcPortraitUrl(order.requester)}
                              alt={order.requester}
                              fallbackIcon={<HelpCircle className="w-4 h-4" />}
                              className="w-9 h-9 rounded-xl bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] p-0.5 shrink-0"
                            />
                          )}
                          <div>
                            <h3 className="text-xs font-bold text-slate-900 dark:text-white">
                              {order.name}
                            </h3>
                            {order.requester && (
                              <div className="text-[10px] text-slate-400">
                                Pemesan: {order.requester}
                              </div>
                            )}
                          </div>
                        </div>

                        <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border border-indigo-500/20 shrink-0">
                          {order.orderType || 'Pesanan Khusus'}
                        </span>
                      </div>

                      <p className="text-xs text-slate-600 dark:text-slate-400 mt-2 leading-relaxed">
                        {order.description}
                      </p>

                      {/* Objectives Progress */}
                      {order.objectives && order.objectives.length > 0 && (
                        <div className="mt-3 space-y-2">
                          {order.objectives.map((obj, idx) => {
                            const percent = obj.maxCount > 0 ? Math.min(100, Math.round((obj.currentCount / obj.maxCount) * 100)) : 0;
                            return (
                              <div
                                key={idx}
                                className="p-2.5 bg-white dark:bg-[#111a2e] rounded-xl border border-slate-200 dark:border-[#1e2a42]"
                              >
                                <div className="flex items-center justify-between text-xs mb-1.5">
                                  <span className="text-[11px] font-medium text-slate-700 dark:text-slate-300">
                                    {obj.description}
                                  </span>
                                  <span className="text-[10px] font-semibold text-indigo-600 dark:text-indigo-400">
                                    {obj.currentCount} / {obj.maxCount}
                                  </span>
                                </div>
                                <div className="w-full h-1.5 bg-slate-100 dark:bg-[#162035] rounded-full overflow-hidden">
                                  <div
                                    className="h-full bg-indigo-500 rounded-full transition-all duration-300"
                                    style={{ width: `${percent}%` }}
                                  />
                                </div>
                              </div>
                            );
                          })}
                        </div>
                      )}
                    </div>

                    <div className="pt-2 border-t border-slate-200 dark:border-[#1e2a42] flex items-center justify-between text-xs">
                      <div className="flex items-center gap-1 text-slate-400 text-[11px]">
                        <Calendar className="w-3 h-3" />
                        <span>{order.daysLeft} hari tersisa</span>
                      </div>

                      {order.isComplete ? (
                        <span className="flex items-center gap-1 text-xs font-semibold text-emerald-600 dark:text-emerald-400">
                          <CheckCircle2 className="w-3.5 h-3.5" /> Selesai
                        </span>
                      ) : (
                        <span className="text-xs text-amber-500 font-medium">Dalam Pengerjaan</span>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
