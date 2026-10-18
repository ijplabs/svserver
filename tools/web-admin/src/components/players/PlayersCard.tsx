import React from 'react';
import { useFarmStore } from '../../store/farmStore';
import { 
  Users, 
  Home, 
  MapPin, 
  Clock, 
  UserCheck
} from 'lucide-react';

export const PlayersCard: React.FC = () => {
  const { players, cabins, status } = useFarmStore();

  const assignedCabins = cabins.filter((c) => c.isAssigned);
  const unassignedCabins = cabins.filter((c) => !c.isAssigned);

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
      {/* Active Online Players */}
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl p-5 shadow-xs flex flex-col justify-between">
        <div>
          <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-[#1e2a42] mb-4">
            <div className="flex items-center space-x-2">
              <Users className="w-4 h-4 text-indigo-600 dark:text-indigo-400" />
              <h3 className="text-sm font-bold text-slate-900 dark:text-white tracking-tight">
                Online Farmers ({players.length})
              </h3>
            </div>
            <span className="text-[11px] font-medium text-slate-500 dark:text-slate-400">
              Live Netcode Session
            </span>
          </div>

          {players.length === 0 ? (
            <div className="py-8 text-center text-slate-400 dark:text-slate-500">
              <Clock className="w-6 h-6 mx-auto mb-1.5 opacity-60" />
              <p className="text-xs font-semibold text-slate-600 dark:text-slate-300">No farmers online right now</p>
              <p className="text-[11px] text-slate-400 dark:text-slate-500">
                Clock is currently {status?.isPaused ? 'paused' : 'running'}.
              </p>
            </div>
          ) : (
            <div className="space-y-2.5">
              {players.map((p) => (
                <div
                  key={p.id}
                  className="flex items-center justify-between p-3 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-xl"
                >
                  <div className="flex items-center space-x-3">
                    <div className="w-8 h-8 rounded-lg bg-indigo-100 dark:bg-indigo-950/80 text-indigo-700 dark:text-indigo-300 flex items-center justify-center font-bold text-xs border border-indigo-200 dark:border-indigo-800/50">
                      {p.name.charAt(0).toUpperCase()}
                    </div>
                    <div>
                      <div className="flex items-center space-x-2">
                        <span className="text-xs font-bold text-slate-900 dark:text-white">{p.name}</span>
                        <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-emerald-100 dark:bg-emerald-950/60 text-emerald-800 dark:text-emerald-300 border border-emerald-200 dark:border-emerald-800/50">
                          Online
                        </span>
                        {p.profession && p.profession !== 'None' && (
                          <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-amber-500/10 text-amber-700 dark:text-amber-400 border border-amber-500/20">
                            {p.profession === 'Farmer' ? 'Petani' :
                             p.profession === 'Miner' ? 'Penambang' :
                             p.profession === 'Fisherman' ? 'Nelayan' :
                             p.profession === 'Rancher' ? 'Peternak' :
                             p.profession === 'Forager' ? 'Penebang' : p.profession}
                          </span>
                        )}
                      </div>
                      <div className="flex items-center space-x-1 text-[11px] text-slate-500 dark:text-slate-400">
                        <MapPin className="w-3 h-3 text-slate-400" />
                        <span>{p.location || 'Farm'}</span>
                      </div>
                    </div>
                  </div>
                  <div className="text-[10px] font-mono text-slate-400 dark:text-slate-500">
                    ID: {p.id}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Cabins & Farmhand Slots */}
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl p-5 shadow-xs flex flex-col justify-between">
        <div>
          <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-[#1e2a42] mb-4">
            <div className="flex items-center space-x-2">
              <Home className="w-4 h-4 text-emerald-600 dark:text-emerald-400" />
              <h3 className="text-sm font-bold text-slate-900 dark:text-white tracking-tight">
                Farmhand Cabins ({cabins.length})
              </h3>
            </div>
            <span className="text-[11px] font-medium bg-slate-100 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] px-2 py-0.5 rounded-full text-slate-700 dark:text-slate-300">
              {assignedCabins.length} Assigned / {unassignedCabins.length} Free
            </span>
          </div>

          {cabins.length === 0 ? (
            <div className="py-8 text-center text-slate-400 dark:text-slate-500">
              <Home className="w-6 h-6 mx-auto mb-1.5 opacity-60" />
              <p className="text-xs font-semibold text-slate-600 dark:text-slate-300">No cabins detected</p>
              <p className="text-[11px] text-slate-400 dark:text-slate-500">Singleplayer or unassigned map save</p>
            </div>
          ) : (
            <div className="space-y-2 max-h-60 overflow-y-auto pr-1">
              {cabins.map((c, i) => (
                <div
                  key={`${c.tileX}-${c.tileY}-${i}`}
                  className="flex items-center justify-between p-2.5 bg-slate-50 dark:bg-[#0a0f1d] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-xs"
                >
                  <div className="flex items-center space-x-2.5">
                    <div
                      className={`w-6 h-6 rounded flex items-center justify-center ${
                        c.isAssigned
                          ? 'bg-emerald-100 dark:bg-emerald-950/80 text-emerald-700 dark:text-emerald-300'
                          : 'bg-slate-200 dark:bg-slate-800 text-slate-600 dark:text-slate-400'
                      }`}
                    >
                      {c.isAssigned ? (
                        <UserCheck className="w-3.5 h-3.5" />
                      ) : (
                        <Home className="w-3.5 h-3.5" />
                      )}
                    </div>
                    <div>
                      <span className="font-semibold text-slate-900 dark:text-white">
                        {c.isAssigned ? c.ownerName : 'Available Slot'}
                      </span>
                      <span className="text-[10px] text-slate-400 dark:text-slate-500 block">
                        Tile ({c.tileX}, {c.tileY}) • Type: {c.type}
                      </span>
                    </div>
                  </div>

                  <div>
                    {c.isAssigned ? (
                      <span className="inline-flex items-center text-[10px] font-medium text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/60 px-2 py-0.5 rounded-md border border-emerald-200 dark:border-emerald-800/50">
                        Assigned
                      </span>
                    ) : (
                      <span className="inline-flex items-center text-[10px] font-medium text-slate-600 dark:text-slate-400 bg-slate-100 dark:bg-[#162035] px-2 py-0.5 rounded-md border border-slate-200 dark:border-[#1e2a42]">
                        Open for Join
                      </span>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default PlayersCard;
