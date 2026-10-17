import React, { useState, useMemo } from 'react';
import { useFarmStore } from '../../store/farmStore';
import { getCropImage, getFertilizerImage } from '../../lib/items';
import { 
  Sprout, 
  Droplets, 
  Sparkles, 
  MapPin, 
  Search, 
  AlertCircle
} from 'lucide-react';

export const CropsDashboard: React.FC = () => {
  const { cropsSnapshot } = useFarmStore();

  const [selectedLocation, setSelectedLocation] = useState<string>('all');
  const [selectedFilter, setSelectedFilter] = useState<'all' | 'ready' | 'unwatered' | 'growing'>('all');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [viewMode, setViewMode] = useState<'grouped' | 'tiles'>('grouped');

  const allCrops = cropsSnapshot?.crops || [];

  // Extract unique locations
  const availableLocations = useMemo(() => {
    const locSet = new Set<string>();
    for (const crop of allCrops) {
      if (crop.location) {
        locSet.add(crop.location);
      }
    }
    return Array.from(locSet).sort();
  }, [allCrops]);

  // Filter crops
  const filteredCrops = useMemo(() => {
    return allCrops.filter((c) => {
      // Location filter
      if (selectedLocation !== 'all' && c.location !== selectedLocation) {
        return false;
      }
      // Status filter
      if (selectedFilter === 'ready' && !c.isReadyForHarvest) {
        return false;
      }
      if (selectedFilter === 'unwatered' && (c.isWatered || c.isDead)) {
        return false;
      }
      if (selectedFilter === 'growing' && (c.isReadyForHarvest || c.isDead)) {
        return false;
      }
      // Search query
      if (searchQuery.trim()) {
        const query = searchQuery.toLowerCase();
        const nameMatch = (c.harvestItemName || c.cropId).toLowerCase().includes(query);
        const locMatch = c.location.toLowerCase().includes(query);
        if (!nameMatch && !locMatch) return false;
      }
      return true;
    });
  }, [allCrops, selectedLocation, selectedFilter, searchQuery]);

  // Aggregate grouped summary
  const groupedCrops = useMemo(() => {
    const map = new Map<
      string,
      {
        id: string;
        name: string;
        total: number;
        ready: number;
        watered: number;
        dead: number;
        minDays: number;
        maxDays: number;
        regrowDays: number;
      }
    >();

    for (const crop of filteredCrops) {
      const key = crop.harvestItemName || crop.cropId || 'Unknown Crop';
      const existing = map.get(key);

      if (existing) {
        existing.total += 1;
        if (crop.isReadyForHarvest) existing.ready += 1;
        if (crop.isWatered) existing.watered += 1;
        if (crop.isDead) existing.dead += 1;
        existing.minDays = Math.min(existing.minDays, crop.daysUntilHarvest);
        existing.maxDays = Math.max(existing.maxDays, crop.daysUntilHarvest);
      } else {
        map.set(key, {
          id: crop.cropId,
          name: key,
          total: 1,
          ready: crop.isReadyForHarvest ? 1 : 0,
          watered: crop.isWatered ? 1 : 0,
          dead: crop.isDead ? 1 : 0,
          minDays: crop.daysUntilHarvest,
          maxDays: crop.daysUntilHarvest,
          regrowDays: crop.regrowDays,
        });
      }
    }

    return Array.from(map.values()).sort((a, b) => b.ready - a.ready || b.total - a.total);
  }, [filteredCrops]);

  return (
    <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl p-5 shadow-xs space-y-5 transition-colors">
      {/* Top Header & Metrics */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-slate-100 dark:border-[#1e2a42]">
        <div>
          <div className="flex items-center space-x-2">
            <Sprout className="w-5 h-5 text-emerald-600 dark:text-emerald-400" />
            <h2 className="text-base font-bold text-slate-900 dark:text-white tracking-tight">
              Agricultural Monitoring & Harvest Tracker
            </h2>
          </div>
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
            Real-time crop growth telemetry synced across all farm plots and green houses
          </p>
        </div>

        {/* View Toggle */}
        <div className="flex items-center space-x-1 bg-slate-100 dark:bg-[#0a0f1d] p-1 rounded-lg text-xs font-semibold self-start sm:self-auto border border-slate-200 dark:border-[#1e2a42]">
          <button
            type="button"
            onClick={() => setViewMode('grouped')}
            className={`px-3 py-1.5 rounded-md transition-colors cursor-pointer ${
              viewMode === 'grouped'
                ? 'bg-white dark:bg-[#162035] text-slate-900 dark:text-white shadow-2xs font-bold'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            }`}
          >
            Grouped Crops
          </button>
          <button
            type="button"
            onClick={() => setViewMode('tiles')}
            className={`px-3 py-1.5 rounded-md transition-colors cursor-pointer ${
              viewMode === 'tiles'
                ? 'bg-white dark:bg-[#162035] text-slate-900 dark:text-white shadow-2xs font-bold'
                : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
            }`}
          >
            Tile Matrix ({filteredCrops.length})
          </button>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div className="grid grid-cols-1 sm:grid-cols-3 lg:grid-cols-4 gap-3 text-xs">
        {/* Search */}
        <div className="relative">
          <Search className="w-3.5 h-3.5 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search crops or location..."
            className="w-full pl-8 pr-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-xs text-slate-900 dark:text-slate-200 placeholder-slate-400 focus:outline-hidden focus:border-slate-400 focus:bg-white dark:focus:bg-[#162035] transition-colors"
          />
        </div>

        {/* Location Dropdown */}
        <div className="relative">
          <MapPin className="w-3.5 h-3.5 text-slate-400 absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none" />
          <select
            value={selectedLocation}
            onChange={(e) => setSelectedLocation(e.target.value)}
            className="w-full pl-8 pr-3 py-2 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-xs text-slate-900 dark:text-slate-200 focus:outline-hidden focus:border-slate-400 focus:bg-white dark:focus:bg-[#162035] transition-colors cursor-pointer appearance-none"
          >
            <option value="all">All Locations ({allCrops.length} tiles)</option>
            {availableLocations.map((loc) => {
              const count = allCrops.filter((c) => c.location === loc).length;
              return (
                <option key={loc} value={loc}>
                  {loc} ({count})
                </option>
              );
            })}
          </select>
        </div>

        {/* Status Filter Buttons */}
        <div className="sm:col-span-1 lg:col-span-2 flex items-center space-x-1.5 overflow-x-auto pb-1 sm:pb-0">
          <button
            type="button"
            onClick={() => setSelectedFilter('all')}
            className={`px-3 py-1.5 rounded-lg font-semibold transition-colors cursor-pointer whitespace-nowrap ${
              selectedFilter === 'all'
                ? 'bg-slate-900 dark:bg-amber-500 text-white dark:text-slate-950 font-bold shadow-xs'
                : 'bg-slate-100 dark:bg-[#0a0f1d] text-slate-700 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-[#162035] dark:hover:text-amber-300 border border-slate-200 dark:border-[#1e2a42]'
            }`}
          >
            All ({allCrops.length})
          </button>
          <button
            type="button"
            onClick={() => setSelectedFilter('ready')}
            className={`px-3 py-1.5 rounded-lg font-semibold transition-colors cursor-pointer whitespace-nowrap flex items-center space-x-1 ${
              selectedFilter === 'ready'
                ? 'bg-emerald-600 text-white font-bold shadow-xs'
                : 'bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 hover:bg-emerald-100 dark:hover:bg-emerald-900/60 dark:hover:text-emerald-200 border border-emerald-200 dark:border-emerald-800/50'
            }`}
          >
            <Sparkles className="w-3 h-3 mr-1" />
            <span>Ready ({cropsSnapshot?.readyCount ?? 0})</span>
          </button>
          <button
            type="button"
            onClick={() => setSelectedFilter('unwatered')}
            className={`px-3 py-1.5 rounded-lg font-semibold transition-colors cursor-pointer whitespace-nowrap flex items-center space-x-1 ${
              selectedFilter === 'unwatered'
                ? 'bg-amber-600 text-white font-bold shadow-xs'
                : 'bg-amber-50 dark:bg-amber-950/60 text-amber-700 dark:text-amber-300 hover:bg-amber-100 dark:hover:bg-amber-900/60 dark:hover:text-amber-200 border border-amber-200 dark:border-amber-800/50'
            }`}
          >
            <Droplets className="w-3 h-3 mr-1" />
            <span>Dry ({cropsSnapshot?.unwateredCount ?? 0})</span>
          </button>
          <button
            type="button"
            onClick={() => setSelectedFilter('growing')}
            className={`px-3 py-1.5 rounded-lg font-semibold transition-colors cursor-pointer whitespace-nowrap ${
              selectedFilter === 'growing'
                ? 'bg-indigo-600 text-white font-bold shadow-xs'
                : 'bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300 hover:bg-indigo-100 dark:hover:bg-indigo-900/60 dark:hover:text-indigo-200 border border-indigo-200 dark:border-indigo-800/50'
            }`}
          >
            Growing
          </button>
        </div>
      </div>

      {/* Main View: Grouped vs Detailed Tiles */}
      {filteredCrops.length === 0 ? (
        <div className="py-12 text-center">
          <Sprout className="w-8 h-8 text-slate-300 dark:text-slate-600 mx-auto mb-2" />
          <p className="text-sm font-semibold text-slate-700 dark:text-slate-300">No crops found</p>
          <p className="text-xs text-slate-400 mt-0.5">
            {allCrops.length === 0
              ? 'No crops currently planted on the farm.'
              : 'Try clearing your filters or search query.'}
          </p>
        </div>
      ) : viewMode === 'grouped' ? (
        /* Grouped Crops Grid */
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {groupedCrops.map((item) => {
            const imgUrl = getCropImage(item.name);
            const waterPercent = Math.round((item.watered / item.total) * 100);

            return (
              <div
                key={item.name}
                className="bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-xl p-4 flex flex-col justify-between hover:border-slate-300 dark:hover:border-slate-700 transition-colors shadow-2xs"
              >
                <div>
                  <div className="flex items-start justify-between">
                    <div className="flex items-center space-x-3">
                      <div className="w-10 h-10 rounded-lg bg-white dark:bg-[#141e33] border border-slate-200 dark:border-[#1e2a42] flex items-center justify-center p-1 overflow-hidden shrink-0">
                        {imgUrl ? (
                          <img
                            src={imgUrl}
                            alt={item.name}
                            className="w-full h-full object-contain"
                            loading="lazy"
                          />
                        ) : (
                          <Sprout className="w-5 h-5 text-emerald-600 dark:text-emerald-400" />
                        )}
                      </div>
                      <div>
                        <h3 className="text-sm font-bold text-slate-900 dark:text-white leading-tight">
                          {item.name}
                        </h3>
                        <span className="text-[11px] text-slate-500 dark:text-slate-400 font-medium">
                          {item.total} planted
                        </span>
                      </div>
                    </div>

                    {item.ready > 0 && (
                      <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 dark:bg-emerald-950/80 text-emerald-800 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800/50">
                        <Sparkles className="w-3 h-3 mr-1" />
                        {item.ready} Ready
                      </span>
                    )}
                  </div>

                  {/* Harvest / Regrow Info */}
                  <div className="mt-4 pt-3 border-t border-slate-200/60 dark:border-[#1e2a42] grid grid-cols-2 gap-2 text-xs">
                    <div>
                      <span className="text-[10px] text-slate-400 block uppercase font-medium">
                        Harvest Time
                      </span>
                      <span className="font-semibold text-slate-700 dark:text-slate-300">
                        {item.ready === item.total
                          ? 'Ready now'
                          : item.minDays === item.maxDays
                          ? `${item.minDays} days`
                          : `${item.minDays}-${item.maxDays} days`}
                      </span>
                    </div>

                    <div>
                      <span className="text-[10px] text-slate-400 block uppercase font-medium">
                        Regrow Cycle
                      </span>
                      <span className="font-semibold text-slate-700 dark:text-slate-300">
                        {item.regrowDays > 0 ? `Every ${item.regrowDays} days` : 'Single harvest'}
                      </span>
                    </div>
                  </div>
                </div>

                {/* Watering Bar */}
                <div className="mt-4">
                  <div className="flex items-center justify-between text-[11px] font-medium mb-1">
                    <span className="text-slate-600 dark:text-slate-400 flex items-center space-x-1">
                      <Droplets className="w-3 h-3 text-sky-500" />
                      <span>{item.watered} of {item.total} watered</span>
                    </span>
                    <span className="text-slate-500 dark:text-slate-400 font-mono">{waterPercent}%</span>
                  </div>
                  <div className="w-full bg-slate-200/70 dark:bg-[#141e33] rounded-full h-1.5 overflow-hidden">
                    <div
                      className="bg-sky-500 h-1.5 rounded-full"
                      style={{ width: `${waterPercent}%` }}
                    />
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        /* Detailed Tile Matrix Table */
        <div className="overflow-x-auto border border-slate-200 dark:border-[#1e2a42] rounded-xl">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 dark:bg-[#0a0f1d] text-slate-500 dark:text-slate-400 uppercase tracking-wider font-semibold border-b border-slate-200 dark:border-[#1e2a42]">
              <tr>
                <th className="py-2.5 px-3">Crop</th>
                <th className="py-2.5 px-3">Location</th>
                <th className="py-2.5 px-3">Coordinates</th>
                <th className="py-2.5 px-3">Growth Stage</th>
                <th className="py-2.5 px-3">Harvest Days</th>
                <th className="py-2.5 px-3">Watered</th>
                <th className="py-2.5 px-3">Fertilizer</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-[#1e2a42] text-slate-700 dark:text-slate-300">
              {filteredCrops.map((c, i) => {
                const img = getCropImage(c.harvestItemName || c.cropId);
                const fertImg = getFertilizerImage(c.fertilizerName || c.fertilizerId || '');

                return (
                  <tr key={`${c.location}-${c.x}-${c.y}-${i}`} className="hover:bg-slate-50/80 dark:hover:bg-[#141e33] transition-colors">
                    <td className="py-2.5 px-3 font-semibold text-slate-900 dark:text-white flex items-center space-x-2">
                      <div className="w-6 h-6 rounded bg-slate-100 dark:bg-[#141e33] border border-slate-200 dark:border-[#1e2a42] flex items-center justify-center overflow-hidden shrink-0">
                        {img ? (
                          <img src={img} alt="" className="w-full h-full object-contain" />
                        ) : (
                          <Sprout className="w-3.5 h-3.5 text-emerald-600 dark:text-emerald-400" />
                        )}
                      </div>
                      <span>{c.harvestItemName || c.cropId || 'Unknown'}</span>
                    </td>
                    <td className="py-2.5 px-3 text-slate-600 dark:text-slate-400">{c.location}</td>
                    <td className="py-2.5 px-3 font-mono text-slate-500 dark:text-slate-400">
                      ({c.x}, {c.y})
                    </td>
                    <td className="py-2.5 px-3">
                      {c.isReadyForHarvest ? (
                        <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold bg-emerald-100 dark:bg-emerald-950/80 text-emerald-800 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800/50">
                          Ready
                        </span>
                      ) : (
                        <span className="text-slate-600 dark:text-slate-400">
                          Phase {c.currentPhase} / {c.totalPhases}
                        </span>
                      )}
                    </td>
                    <td className="py-2.5 px-3 font-medium">
                      {c.isReadyForHarvest ? (
                        <span className="text-emerald-600 dark:text-emerald-400 font-semibold">Today</span>
                      ) : (
                        <span>{c.daysUntilHarvest} days</span>
                      )}
                    </td>
                    <td className="py-2.5 px-3">
                      {c.isWatered ? (
                        <span className="inline-flex items-center text-sky-700 dark:text-sky-400 space-x-1 font-medium">
                          <Droplets className="w-3.5 h-3.5 text-sky-500" />
                          <span>Watered</span>
                        </span>
                      ) : (
                        <span className="inline-flex items-center text-amber-700 dark:text-amber-400 space-x-1 font-medium">
                          <AlertCircle className="w-3.5 h-3.5 text-amber-500" />
                          <span>Dry</span>
                        </span>
                      )}
                    </td>
                    <td className="py-2.5 px-3">
                      {c.fertilizerName ? (
                        <div className="flex items-center space-x-1 text-slate-600 dark:text-slate-400">
                          {fertImg && (
                            <img src={fertImg} alt="" className="w-4 h-4 object-contain" />
                          )}
                          <span>{c.fertilizerName}</span>
                        </div>
                      ) : (
                        <span className="text-slate-400 dark:text-slate-500 font-normal">None</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};

export default CropsDashboard;
