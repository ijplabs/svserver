import React, { useState, useEffect } from 'react';
import {
  Compass,
  MapPin,
  Home,
  Trees,
  Building2,
  Mountain,
  Waves,
  Pickaxe,
  RefreshCw,
  AlertCircle,
  Layers,
} from 'lucide-react';
import { api } from '../../lib/api';
import { MapTrackingResponse, PlayerMapPin } from '../../types/map';

export const ValleyMapCard: React.FC = () => {
  const [mapData, setMapData] = useState<MapTrackingResponse | null>(null);
  const [viewMode, setViewMode] = useState<'farm' | 'valley'>('farm');
  const [isLoading, setIsLoading] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedPin, setSelectedPin] = useState<PlayerMapPin | null>(null);

  const fetchMapData = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await api.map.getTracking();
      setMapData(data);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Gagal memuat visualisasi peta.';
      setError(message);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchMapData();
  }, []);

  useEffect(() => {
    if (!autoRefresh) return;
    const interval = setInterval(fetchMapData, 3500);
    return () => clearInterval(interval);
  }, [autoRefresh]);

  if (isLoading && !mapData) {
    return (
      <div className="flex flex-col items-center justify-center p-12 bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl shadow-xs text-slate-400">
        <RefreshCw className="w-8 h-8 animate-spin mb-3 text-amber-500" />
        <span className="text-sm font-medium">Memuat pelacak peta interaktif Lembah Stardew...</span>
      </div>
    );
  }

  if (error && !mapData) {
    return (
      <div className="p-6 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900 rounded-2xl text-center space-y-3">
        <AlertCircle className="w-8 h-8 text-red-500 mx-auto" />
        <p className="text-sm text-red-700 dark:text-red-400">{error}</p>
        <button
          onClick={fetchMapData}
          className="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-xl text-xs font-medium transition-colors"
        >
          Coba Lagi
        </button>
      </div>
    );
  }

  if (!mapData) return null;

  // Group players by zone for Valley view
  const playersByZone: Record<string, PlayerMapPin[]> = {
    farm: [],
    town: [],
    beach: [],
    mountain: [],
    forest: [],
    mines: [],
    desert: [],
    island: [],
    other: [],
  };

  mapData.players.forEach((p) => {
    const z = p.zone || 'farm';
    if (playersByZone[z]) {
      playersByZone[z].push(p);
    } else {
      playersByZone.other.push(p);
    }
  });

  const farmPlayers = mapData.players.filter(
    (p) => p.zone === 'farm' && p.isOnline
  );

  const totalCropsCount = Object.values(mapData.cropCountsByLocation).reduce(
    (acc, curr) => acc + curr,
    0
  );

  return (
    <div className="space-y-6">
      {/* HEADER & VIEW TOGGLES */}
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 shadow-xs transition-colors">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-amber-500/10 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400 flex items-center justify-center border border-slate-200 dark:border-[#1e2a42]">
              <Compass className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-semibold text-slate-900 dark:text-white">
                Peta Lembah Interaktif & Pelacak Petani
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                Pemantauan posisi pemain, tata letak ladang kebun, dan kabin secara real-time
              </p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            {/* View Switcher */}
            <div className="flex p-1 bg-slate-100 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-xl">
              <button
                onClick={() => setViewMode('farm')}
                className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all cursor-pointer ${
                  viewMode === 'farm'
                    ? 'bg-white dark:bg-[#162035] text-amber-600 dark:text-amber-400 shadow-xs font-bold'
                    : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                }`}
              >
                Tata Letak Kebun
              </button>
              <button
                onClick={() => setViewMode('valley')}
                className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all cursor-pointer ${
                  viewMode === 'valley'
                    ? 'bg-white dark:bg-[#162035] text-amber-600 dark:text-amber-400 shadow-xs font-bold'
                    : 'text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                }`}
              >
                Peta Regional Lembah
              </button>
            </div>

            {/* Auto refresh toggle */}
            <button
              onClick={() => setAutoRefresh(!autoRefresh)}
              className={`p-2 rounded-xl border text-xs font-medium transition-colors ${
                autoRefresh
                  ? 'border-indigo-200 dark:border-indigo-800 bg-indigo-50/50 dark:bg-indigo-950/30 text-indigo-600 dark:text-indigo-400'
                  : 'border-slate-200 dark:border-slate-700 text-slate-400'
              }`}
              title={autoRefresh ? 'Pembaruan otomatis aktif (3.5s)' : 'Pembaruan otomatis mati'}
            >
              <RefreshCw className={`w-4 h-4 ${autoRefresh && isLoading ? 'animate-spin' : ''}`} />
            </button>
          </div>
        </div>

        {/* SUMMARY STATS BAR */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 mt-6 pt-6 border-t border-slate-100 dark:border-slate-800 text-xs">
          <div className="p-3 bg-slate-50 dark:bg-slate-800/40 rounded-xl">
            <span className="text-slate-400 block text-[11px]">Tipe Ladang</span>
            <span className="font-semibold text-slate-800 dark:text-slate-200">
              {mapData.farmType}
            </span>
          </div>
          <div className="p-3 bg-slate-50 dark:bg-slate-800/40 rounded-xl">
            <span className="text-slate-400 block text-[11px]">Pemain di Kebun</span>
            <span className="font-semibold text-slate-800 dark:text-slate-200">
              {farmPlayers.length} Petani Aktif
            </span>
          </div>
          <div className="p-3 bg-slate-50 dark:bg-slate-800/40 rounded-xl">
            <span className="text-slate-400 block text-[11px]">Bangunan Ladang</span>
            <span className="font-semibold text-slate-800 dark:text-slate-200">
              {mapData.buildings.length} Bangunan / Kabin
            </span>
          </div>
          <div className="p-3 bg-slate-50 dark:bg-slate-800/40 rounded-xl">
            <span className="text-slate-400 block text-[11px]">Total Tanaman</span>
            <span className="font-semibold text-emerald-600 dark:text-emerald-400">
              {totalCropsCount} Petak
            </span>
          </div>
        </div>
      </div>

      {/* VIEW MODE 1: FARM LAYOUT VISUAL MAP */}
      {viewMode === 'farm' && (
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 shadow-xs space-y-4 transition-colors">
          <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-[#1e2a42]">
            <div className="flex items-center gap-2">
              <Layers className="w-4 h-4 text-amber-500" />
              <h3 className="font-semibold text-slate-900 dark:text-white text-sm">
                Denah visual Kebun ({mapData.farmWidth} x {mapData.farmHeight} Petak)
              </h3>
            </div>
            <div className="flex items-center gap-4 text-xs text-slate-500 dark:text-slate-400">
              <span className="flex items-center gap-1.5">
                <span className="w-2.5 h-2.5 rounded-full bg-amber-500" />
                Posisi Petani
              </span>
              <span className="flex items-center gap-1.5">
                <span className="w-2.5 h-2.5 rounded bg-emerald-500/30 border border-emerald-500" />
                Kabin
              </span>
              <span className="flex items-center gap-1.5">
                <span className="w-2.5 h-2.5 rounded bg-amber-500/30 border border-amber-500" />
                Kandang
              </span>
            </div>
          </div>

          {/* SVG Map Canvas */}
          <div className="relative w-full aspect-[80/65] bg-emerald-950/10 dark:bg-[#0a0f1d] border border-emerald-900/20 dark:border-[#1e2a42] rounded-xl overflow-hidden shadow-inner">
            <svg
              viewBox={`0 0 ${mapData.farmWidth} ${mapData.farmHeight}`}
              className="w-full h-full"
            >
              {/* Farm Grid Texture */}
              <defs>
                <pattern id="farmGrid" width="5" height="5" patternUnits="userSpaceOnUse">
                  <path
                    d="M 5 0 L 0 0 0 5"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="0.08"
                    className="text-slate-300 dark:text-slate-800"
                  />
                </pattern>
              </defs>
              <rect width="100%" height="100%" fill="url(#farmGrid)" />

              {/* Landmarks: Farmhouse, Greenhouse, Exit */}
              <rect
                x="58"
                y="11"
                width="9"
                height="6"
                className="fill-indigo-900/20 stroke-indigo-600 dark:stroke-indigo-400"
                strokeWidth="0.25"
                rx="0.5"
              />
              <text x="62.5" y="14.5" fontSize="1.8" textAnchor="middle" className="fill-indigo-600 dark:fill-indigo-300 font-semibold">
                Rumah Utama
              </text>

              <rect
                x="25"
                y="10"
                width="7"
                height="6"
                className="fill-teal-900/20 stroke-teal-600 dark:stroke-teal-400"
                strokeWidth="0.25"
                rx="0.5"
              />
              <text x="28.5" y="13.5" fontSize="1.6" textAnchor="middle" className="fill-teal-600 dark:fill-teal-300 font-semibold">
                Greenhouse
              </text>

              {/* Farm Buildings */}
              {mapData.buildings.map((b, i) => {
                const isLobby = b.isLobby;
                const isCabin = b.isCabin;
                const isAnimal = b.type.toLowerCase().includes('barn') || b.type.toLowerCase().includes('coop');

                return (
                  <g key={i}>
                    <rect
                      x={b.tileX}
                      y={b.tileY}
                      width={b.tilesWide}
                      height={b.tilesHigh}
                      className={
                        isLobby
                          ? 'fill-purple-500/20 stroke-purple-500'
                          : isCabin
                          ? 'fill-emerald-500/20 stroke-emerald-500'
                          : isAnimal
                          ? 'fill-amber-500/20 stroke-amber-500'
                          : 'fill-slate-500/20 stroke-slate-500'
                      }
                      strokeWidth="0.25"
                      rx="0.5"
                    />
                    <text
                      x={b.tileX + b.tilesWide / 2}
                      y={b.tileY + b.tilesHigh / 2}
                      fontSize="1.3"
                      textAnchor="middle"
                      className="fill-slate-700 dark:fill-slate-200 font-medium"
                    >
                      {b.isCabin ? (b.ownerName || 'Kabin Kosong') : b.type}
                    </text>
                  </g>
                );
              })}

              {/* Live Players Pins on Farm */}
              {farmPlayers.map((player) => (
                <g
                  key={player.id}
                  className="cursor-pointer transition-transform duration-300"
                  onClick={() => setSelectedPin(player)}
                >
                  {/* Pulse circle */}
                  <circle
                    cx={player.tileX}
                    cy={player.tileY}
                    r="2.5"
                    className="fill-indigo-500/30 animate-ping"
                  />
                  {/* Pin body */}
                  <circle
                    cx={player.tileX}
                    cy={player.tileY}
                    r="1.4"
                    className="fill-indigo-600 stroke-white dark:stroke-slate-900"
                    strokeWidth="0.4"
                  />
                  {/* Name tag */}
                  <text
                    x={player.tileX}
                    y={player.tileY - 2}
                    fontSize="1.6"
                    textAnchor="middle"
                    className="fill-indigo-900 dark:fill-indigo-200 font-bold"
                  >
                    {player.name}
                  </text>
                </g>
              ))}
            </svg>
          </div>

          {/* Selected Player Details */}
          {selectedPin && (
            <div className="p-4 bg-indigo-50 dark:bg-indigo-950/30 border border-indigo-200 dark:border-indigo-900 rounded-xl flex items-center justify-between text-xs">
              <div className="flex items-center gap-3">
                <MapPin className="w-5 h-5 text-indigo-600 dark:text-indigo-400" />
                <div>
                  <span className="font-semibold text-slate-900 dark:text-white">
                    {selectedPin.name}
                  </span>
                  <p className="text-slate-500 dark:text-slate-400 text-[11px]">
                    Lokasi: {selectedPin.location} ({selectedPin.tileX}, {selectedPin.tileY})
                  </p>
                </div>
              </div>
              <button
                onClick={() => setSelectedPin(null)}
                className="px-2.5 py-1 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 text-xs"
              >
                Tutup
              </button>
            </div>
          )}
        </div>
      )}

      {/* VIEW MODE 2: VALLEY REGIONAL MAP */}
      {viewMode === 'valley' && (
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 shadow-xs space-y-6 transition-colors">
          <div className="flex items-center gap-2 pb-3 border-b border-slate-100 dark:border-[#1e2a42]">
            <Compass className="w-4 h-4 text-amber-500" />
            <h3 className="font-semibold text-slate-900 dark:text-white text-sm">
              Distribusi Pemain di Wilayah Lembah Stardew
            </h3>
          </div>

          {/* Regional Cards Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {/* FARM */}
            <div className="p-4 bg-emerald-50/50 dark:bg-emerald-950/20 border border-emerald-200/80 dark:border-emerald-900/40 rounded-xl space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 text-emerald-800 dark:text-emerald-300 font-semibold text-xs">
                  <Home className="w-4 h-4 text-emerald-600 dark:text-emerald-400" />
                  <span>Ladang Kebun (Farm)</span>
                </div>
                <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-100 text-emerald-800 dark:bg-emerald-900/60 dark:text-emerald-300">
                  {playersByZone.farm.length} Petani
                </span>
              </div>
              <div className="space-y-1.5 text-xs">
                {playersByZone.farm.length === 0 ? (
                  <p className="text-slate-400 text-[11px]">Tidak ada petani di ladang.</p>
                ) : (
                  playersByZone.farm.map((p) => (
                    <div key={p.id} className="flex items-center justify-between text-slate-700 dark:text-slate-300">
                      <span>{p.name}</span>
                      <span className="font-mono text-slate-400 text-[11px]">
                        ({p.tileX}, {p.tileY})
                      </span>
                    </div>
                  ))
                )}
              </div>
            </div>

            {/* PELICAN TOWN */}
            <div className="p-4 bg-blue-50/50 dark:bg-blue-950/20 border border-blue-200/80 dark:border-blue-900/40 rounded-xl space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 text-blue-800 dark:text-blue-300 font-semibold text-xs">
                  <Building2 className="w-4 h-4 text-blue-600 dark:text-blue-400" />
                  <span>Kota Pelikan (Town)</span>
                </div>
                <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-blue-100 text-blue-800 dark:bg-blue-900/60 dark:text-blue-300">
                  {playersByZone.town.length} Petani
                </span>
              </div>
              <div className="space-y-1.5 text-xs">
                {playersByZone.town.length === 0 ? (
                  <p className="text-slate-400 text-[11px]">Tidak ada petani di kota.</p>
                ) : (
                  playersByZone.town.map((p) => (
                    <div key={p.id} className="flex items-center justify-between text-slate-700 dark:text-slate-300">
                      <span>{p.name}</span>
                      <span className="text-slate-400 text-[11px]">{p.location}</span>
                    </div>
                  ))
                )}
              </div>
            </div>

            {/* CINDERSAP FOREST */}
            <div className="p-4 bg-teal-50/50 dark:bg-teal-950/20 border border-teal-200/80 dark:border-teal-900/40 rounded-xl space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 text-teal-800 dark:text-teal-300 font-semibold text-xs">
                  <Trees className="w-4 h-4 text-teal-600 dark:text-teal-400" />
                  <span>Hutan Cindersap</span>
                </div>
                <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-teal-100 text-teal-800 dark:bg-teal-900/60 dark:text-teal-300">
                  {playersByZone.forest.length} Petani
                </span>
              </div>
              <div className="space-y-1.5 text-xs">
                {playersByZone.forest.length === 0 ? (
                  <p className="text-slate-400 text-[11px]">Tidak ada petani di hutan.</p>
                ) : (
                  playersByZone.forest.map((p) => (
                    <div key={p.id} className="flex items-center justify-between text-slate-700 dark:text-slate-300">
                      <span>{p.name}</span>
                      <span className="text-slate-400 text-[11px]">{p.location}</span>
                    </div>
                  ))
                )}
              </div>
            </div>

            {/* MOUNTAIN & LAKE */}
            <div className="p-4 bg-amber-50/50 dark:bg-amber-950/20 border border-amber-200/80 dark:border-amber-900/40 rounded-xl space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 text-amber-800 dark:text-amber-300 font-semibold text-xs">
                  <Mountain className="w-4 h-4 text-amber-600 dark:text-amber-400" />
                  <span>Pegunungan & Danau</span>
                </div>
                <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-amber-100 text-amber-800 dark:bg-amber-900/60 dark:text-amber-300">
                  {playersByZone.mountain.length} Petani
                </span>
              </div>
              <div className="space-y-1.5 text-xs">
                {playersByZone.mountain.length === 0 ? (
                  <p className="text-slate-400 text-[11px]">Tidak ada petani di pegunungan.</p>
                ) : (
                  playersByZone.mountain.map((p) => (
                    <div key={p.id} className="flex items-center justify-between text-slate-700 dark:text-slate-300">
                      <span>{p.name}</span>
                      <span className="text-slate-400 text-[11px]">{p.location}</span>
                    </div>
                  ))
                )}
              </div>
            </div>

            {/* MINES */}
            <div className="p-4 bg-slate-100/70 dark:bg-slate-800/40 border border-slate-300/80 dark:border-slate-700/60 rounded-xl space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 text-slate-800 dark:text-slate-300 font-semibold text-xs">
                  <Pickaxe className="w-4 h-4 text-slate-600 dark:text-slate-400" />
                  <span>Tambang (Mines / Skull Cave)</span>
                </div>
                <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-slate-200 text-slate-800 dark:bg-slate-700 dark:text-slate-300">
                  {playersByZone.mines.length} Petani
                </span>
              </div>
              <div className="space-y-1.5 text-xs">
                {playersByZone.mines.length === 0 ? (
                  <p className="text-slate-400 text-[11px]">Tidak ada petani di tambang.</p>
                ) : (
                  playersByZone.mines.map((p) => (
                    <div key={p.id} className="flex items-center justify-between text-slate-700 dark:text-slate-300">
                      <span>{p.name}</span>
                      <span className="text-slate-400 text-[11px]">{p.location}</span>
                    </div>
                  ))
                )}
              </div>
            </div>

            {/* BEACH */}
            <div className="p-4 bg-cyan-50/50 dark:bg-cyan-950/20 border border-cyan-200/80 dark:border-cyan-900/40 rounded-xl space-y-3">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 text-cyan-800 dark:text-cyan-300 font-semibold text-xs">
                  <Waves className="w-4 h-4 text-cyan-600 dark:text-cyan-400" />
                  <span>Pantai (Beach)</span>
                </div>
                <span className="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-cyan-100 text-cyan-800 dark:bg-cyan-900/60 dark:text-cyan-300">
                  {playersByZone.beach.length} Petani
                </span>
              </div>
              <div className="space-y-1.5 text-xs">
                {playersByZone.beach.length === 0 ? (
                  <p className="text-slate-400 text-[11px]">Tidak ada petani di pantai.</p>
                ) : (
                  playersByZone.beach.map((p) => (
                    <div key={p.id} className="flex items-center justify-between text-slate-700 dark:text-slate-300">
                      <span>{p.name}</span>
                      <span className="text-slate-400 text-[11px]">{p.location}</span>
                    </div>
                  ))
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
