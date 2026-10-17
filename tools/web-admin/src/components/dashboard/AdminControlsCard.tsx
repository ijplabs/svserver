import React, { useState } from 'react';
import { api } from '../../lib/api';
import { useFarmStore } from '../../store/farmStore';
import { 
  Sliders, 
  Clock, 
  Gauge, 
  Monitor, 
  RotateCw, 
  Check, 
  AlertCircle 
} from 'lucide-react';

export const AdminControlsCard: React.FC = () => {
  const { fetchAll } = useFarmStore();
  const [actionMessage, setActionMessage] = useState<{ text: string; error?: boolean } | null>(null);
  const [isProcessing, setIsProcessing] = useState(false);

  const notify = (text: string, error = false) => {
    setActionMessage({ text, error });
    setTimeout(() => setActionMessage(null), 3500);
  };

  const handleSetTime = async (timeVal: number) => {
    setIsProcessing(true);
    try {
      await api.server.setTime(timeVal);
      notify(`In-game time updated to ${timeVal.toString().padStart(4, '0')}`);
      await fetchAll();
    } catch (err: unknown) {
      notify(err instanceof Error ? err.message : 'Failed to set time', true);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleClockSpeed = async (multiplier: number) => {
    setIsProcessing(true);
    try {
      await api.server.setClockSpeed(multiplier);
      notify(`Clock speed multiplier set to ${multiplier}x`);
      await fetchAll();
    } catch (err: unknown) {
      notify(err instanceof Error ? err.message : 'Failed to set clock speed', true);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleRendering = async (fps: number) => {
    setIsProcessing(true);
    try {
      await api.server.setRendering(fps);
      notify(`Server rendering set to ${fps} FPS`);
      await fetchAll();
    } catch (err: unknown) {
      notify(err instanceof Error ? err.message : 'Failed to set rendering FPS', true);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleReload = async () => {
    if (!window.confirm('Are you sure you want to reload the server? Connected players may be affected.')) {
      return;
    }
    setIsProcessing(true);
    try {
      await api.server.reloadServer();
      notify('Server reload command dispatched');
      await fetchAll();
    } catch (err: unknown) {
      notify(err instanceof Error ? err.message : 'Failed to reload server', true);
    } finally {
      setIsProcessing(false);
    }
  };

  return (
    <div className="bg-white border border-slate-200 rounded-xl p-5 shadow-xs">
      <div className="flex items-center justify-between mb-4 pb-3 border-b border-slate-100">
        <div className="flex items-center space-x-2">
          <Sliders className="w-4 h-4 text-indigo-600" />
          <h2 className="text-sm font-bold text-slate-900 tracking-tight">
            Superadmin Server Controls
          </h2>
        </div>
        <span className="text-[11px] font-medium text-indigo-700 bg-indigo-50 border border-indigo-100 px-2 py-0.5 rounded-full">
          Superadmin Protected
        </span>
      </div>

      {actionMessage && (
        <div
          className={`mb-4 p-3 rounded-lg text-xs flex items-center space-x-2 ${
            actionMessage.error
              ? 'bg-rose-50 text-rose-700 border border-rose-200'
              : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
          }`}
        >
          {actionMessage.error ? (
            <AlertCircle className="w-4 h-4 shrink-0" />
          ) : (
            <Check className="w-4 h-4 shrink-0" />
          )}
          <span>{actionMessage.text}</span>
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        {/* Set Time */}
        <div className="space-y-2">
          <label className="text-xs font-semibold text-slate-600 flex items-center space-x-1.5">
            <Clock className="w-3.5 h-3.5 text-slate-400" />
            <span>Set Time of Day</span>
          </label>
          <div className="grid grid-cols-2 gap-1.5">
            <button
              type="button"
              disabled={isProcessing}
              onClick={() => handleSetTime(600)}
              className="py-1.5 px-2 bg-slate-50 hover:bg-slate-100 border border-slate-200 rounded-lg text-xs font-medium text-slate-700 transition-colors cursor-pointer disabled:opacity-50"
            >
              06:00 (Dawn)
            </button>
            <button
              type="button"
              disabled={isProcessing}
              onClick={() => handleSetTime(1200)}
              className="py-1.5 px-2 bg-slate-50 hover:bg-slate-100 border border-slate-200 rounded-lg text-xs font-medium text-slate-700 transition-colors cursor-pointer disabled:opacity-50"
            >
              12:00 (Noon)
            </button>
            <button
              type="button"
              disabled={isProcessing}
              onClick={() => handleSetTime(1800)}
              className="py-1.5 px-2 bg-slate-50 hover:bg-slate-100 border border-slate-200 rounded-lg text-xs font-medium text-slate-700 transition-colors cursor-pointer disabled:opacity-50"
            >
              18:00 (Dusk)
            </button>
            <button
              type="button"
              disabled={isProcessing}
              onClick={() => handleSetTime(2200)}
              className="py-1.5 px-2 bg-slate-50 hover:bg-slate-100 border border-slate-200 rounded-lg text-xs font-medium text-slate-700 transition-colors cursor-pointer disabled:opacity-50"
            >
              22:00 (Night)
            </button>
          </div>
        </div>

        {/* Clock Speed Multiplier */}
        <div className="space-y-2">
          <label className="text-xs font-semibold text-slate-600 flex items-center space-x-1.5">
            <Gauge className="w-3.5 h-3.5 text-slate-400" />
            <span>Clock Speed</span>
          </label>
          <div className="grid grid-cols-4 gap-1">
            <button
              type="button"
              disabled={isProcessing}
              onClick={() => handleClockSpeed(0.5)}
              className="py-1.5 px-2 bg-slate-50 hover:bg-slate-100 border border-slate-200 rounded-lg text-xs font-medium text-slate-700 transition-colors cursor-pointer disabled:opacity-50"
            >
              0.5x
            </button>
            <button
              type="button"
              disabled={isProcessing}
              onClick={() => handleClockSpeed(1.0)}
              className="py-1.5 px-2 bg-slate-50 hover:bg-slate-100 border border-slate-200 rounded-lg text-xs font-medium text-slate-700 transition-colors cursor-pointer disabled:opacity-50"
            >
              1.0x
            </button>
            <button
              type="button"
              disabled={isProcessing}
              onClick={() => handleClockSpeed(2.0)}
              className="py-1.5 px-2 bg-slate-50 hover:bg-slate-100 border border-slate-200 rounded-lg text-xs font-medium text-slate-700 transition-colors cursor-pointer disabled:opacity-50"
            >
              2.0x
            </button>
            <button
              type="button"
              disabled={isProcessing}
              onClick={() => handleClockSpeed(5.0)}
              className="py-1.5 px-2 bg-slate-50 hover:bg-slate-100 border border-slate-200 rounded-lg text-xs font-medium text-slate-700 transition-colors cursor-pointer disabled:opacity-50"
            >
              5.0x
            </button>
          </div>
        </div>

        {/* Rendering Throttle */}
        <div className="space-y-2">
          <label className="text-xs font-semibold text-slate-600 flex items-center space-x-1.5">
            <Monitor className="w-3.5 h-3.5 text-slate-400" />
            <span>Rendering Mode</span>
          </label>
          <div className="grid grid-cols-2 gap-1.5">
            <button
              type="button"
              disabled={isProcessing}
              onClick={() => handleRendering(0)}
              className="py-1.5 px-2 bg-slate-50 hover:bg-slate-100 border border-slate-200 rounded-lg text-xs font-medium text-slate-700 transition-colors cursor-pointer disabled:opacity-50"
              title="0 FPS: Minimum CPU usage"
            >
              0 FPS (Headless)
            </button>
            <button
              type="button"
              disabled={isProcessing}
              onClick={() => handleRendering(60)}
              className="py-1.5 px-2 bg-slate-50 hover:bg-slate-100 border border-slate-200 rounded-lg text-xs font-medium text-slate-700 transition-colors cursor-pointer disabled:opacity-50"
              title="60 FPS: Smooth VNC screen"
            >
              60 FPS (Active)
            </button>
          </div>
        </div>

        {/* Maintenance Actions */}
        <div className="space-y-2">
          <label className="text-xs font-semibold text-slate-600 flex items-center space-x-1.5">
            <RotateCw className="w-3.5 h-3.5 text-slate-400" />
            <span>Maintenance</span>
          </label>
          <button
            type="button"
            disabled={isProcessing}
            onClick={handleReload}
            className="w-full py-2 px-3 bg-rose-50 hover:bg-rose-100 border border-rose-200 text-rose-700 rounded-lg text-xs font-semibold transition-colors cursor-pointer disabled:opacity-50 flex items-center justify-center space-x-1.5"
          >
            <RotateCw className="w-3.5 h-3.5" />
            <span>Reload Mod Engine</span>
          </button>
        </div>
      </div>
    </div>
  );
};
