import React, { useState } from 'react';
import { api } from '../../lib/api';
import { 
  Terminal, 
  Play, 
  Trash2, 
  ShieldCheck, 
  AlertCircle 
} from 'lucide-react';

interface LogItem {
  id: string;
  command: string;
  output: string;
  success: boolean;
  timestamp: string;
}

export const ConsoleTerminalCard: React.FC = () => {
  const [commandInput, setCommandInput] = useState('');
  const [isExecuting, setIsExecuting] = useState(false);
  const [logs, setLogs] = useState<LogItem[]>([]);
  const [error, setError] = useState<string | null>(null);

  const quickCommands = [
    { label: 'list_players', cmd: 'list_players' },
    { label: 'save', cmd: 'save' },
    { label: 'help', cmd: 'help' },
    { label: '!votesleep status', cmd: '!votesleep status' },
    { label: '!bank', cmd: '!bank' },
  ];

  const handleExecute = async (cmdToRun?: string) => {
    const cmd = (cmdToRun || commandInput).trim();
    if (!cmd || isExecuting) return;

    setIsExecuting(true);
    setError(null);
    try {
      const res = await api.console.execute(cmd);
      setLogs((prev) => [
        ...prev,
        {
          id: Math.random().toString(36).substring(7),
          command: cmd,
          output: res.output || (res.success ? 'Perintah berhasil dijalankan.' : 'Perintah tidak dikenal.'),
          success: res.success,
          timestamp: new Date().toLocaleTimeString(),
        },
      ]);
      if (!cmdToRun) {
        setCommandInput('');
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Gagal mengeksekusi perintah konsol';
      setError(msg);
      setLogs((prev) => [
        ...prev,
        {
          id: Math.random().toString(36).substring(7),
          command: cmd,
          output: `ERROR: ${msg}`,
          success: false,
          timestamp: new Date().toLocaleTimeString(),
        },
      ]);
    } finally {
      setIsExecuting(false);
    }
  };

  const handleClearLogs = () => {
    setLogs([]);
    setError(null);
  };

  return (
    <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-xl shadow-xs overflow-hidden flex flex-col h-[560px] transition-colors">
      {/* Header */}
      <div className="px-6 py-4 border-b border-slate-200 dark:border-[#1e2a42] flex flex-col sm:flex-row sm:items-center justify-between gap-3 bg-slate-50/50 dark:bg-[#0a0f1d]/50">
        <div className="flex items-center space-x-3">
          <div className="p-2 bg-slate-900 dark:bg-[#162035] text-amber-400 rounded-lg border border-slate-700/50 dark:border-[#1e2a42]">
            <Terminal className="w-5 h-5" />
          </div>
          <div>
            <div className="flex items-center space-x-2">
              <h2 className="text-base font-semibold text-slate-900 dark:text-white tracking-tight">
                Konsol Interaktif SMAPI Remote
              </h2>
              <span className="inline-flex items-center space-x-1 px-2 py-0.5 rounded-full text-[11px] font-semibold bg-indigo-50 dark:bg-indigo-950/60 text-indigo-700 dark:text-indigo-300 border border-indigo-200 dark:border-indigo-800">
                <ShieldCheck className="w-3 h-3" />
                <span>Superadmin</span>
              </span>
            </div>
            <p className="text-xs text-slate-500 dark:text-slate-400">
              Jalankan perintah langsung ke game thread MonoGame atau sistem chat
            </p>
          </div>
        </div>

        <button
          type="button"
          onClick={handleClearLogs}
          className="inline-flex items-center space-x-1.5 px-3 py-1.5 bg-white dark:bg-[#141e33] hover:bg-slate-50 dark:hover:bg-[#1a2845] border border-slate-200 dark:border-[#1e2a42] rounded-lg text-xs font-medium text-slate-700 dark:text-slate-300 transition-colors cursor-pointer shadow-2xs"
        >
          <Trash2 className="w-3.5 h-3.5 text-slate-400" />
          <span>Bersihkan Layar</span>
        </button>
      </div>

      {/* Quick Command Bar */}
      <div className="px-6 py-2.5 bg-slate-100/75 dark:bg-[#0a0f1d] border-b border-slate-200 dark:border-[#1e2a42] flex items-center space-x-2 overflow-x-auto">
        <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 shrink-0">
          Perintah Cepat:
        </span>
        <div className="flex items-center space-x-1.5">
          {quickCommands.map((qc) => (
            <button
              key={qc.cmd}
              type="button"
              onClick={() => handleExecute(qc.cmd)}
              disabled={isExecuting}
              className="px-2.5 py-1 rounded bg-white dark:bg-[#141e33] hover:bg-slate-200 dark:hover:bg-[#1e2c4a] text-slate-700 dark:text-slate-200 border border-slate-300 dark:border-[#1e2a42] text-[11px] font-mono font-medium transition-colors cursor-pointer whitespace-nowrap disabled:opacity-50 shadow-2xs"
            >
              {qc.label}
            </button>
          ))}
        </div>
      </div>

      {error && (
        <div className="px-6 py-2 bg-rose-50 border-b border-rose-200 text-rose-800 text-xs flex items-center space-x-2">
          <AlertCircle className="w-4 h-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {/* Terminal Output Area */}
      <div className="flex-1 bg-slate-950 p-6 overflow-y-auto font-mono text-xs text-slate-200 space-y-3">
        <div className="text-slate-500 text-[11px] border-b border-slate-800 pb-2">
          <div>JunimoServer Interactive Terminal v1.6 (Headless MonoGame Loop)</div>
          <div>Ketik perintah SMAPI (misal: help, list_players) atau chat command (!votesleep)</div>
        </div>

        {logs.length === 0 ? (
          <div className="text-slate-600 italic py-8 text-center">
            Siap menerima perintah. Gunakan tombol perintah cepat di atas atau masukkan perintah di bawah.
          </div>
        ) : (
          logs.map((item) => (
            <div key={item.id} className="space-y-1">
              <div className="flex items-center space-x-2 text-indigo-400">
                <span className="text-slate-500">[{item.timestamp}]</span>
                <span className="text-emerald-400 font-bold">&gt;</span>
                <span className="text-slate-100">{item.command}</span>
              </div>
              <div
                className={`pl-6 whitespace-pre-wrap ${
                  item.success ? 'text-slate-300' : 'text-rose-400'
                }`}
              >
                {item.output}
              </div>
            </div>
          ))
        )}
      </div>

      {/* Terminal Input Form */}
      <form
        onSubmit={(e) => {
          e.preventDefault();
          handleExecute();
        }}
        className="p-3 bg-slate-900 border-t border-slate-800 flex items-center space-x-2"
      >
        <span className="text-emerald-400 font-mono font-bold pl-2">&gt;</span>
        <input
          type="text"
          value={commandInput}
          onChange={(e) => setCommandInput(e.target.value)}
          placeholder="Ketik perintah (contoh: help, list_players, !backup)..."
          className="flex-1 px-3 py-2 text-xs bg-slate-800 border border-slate-700 rounded text-slate-100 font-mono focus:outline-none focus:border-indigo-500 transition-colors"
          disabled={isExecuting}
        />
        <button
          type="submit"
          disabled={isExecuting || !commandInput.trim()}
          className="inline-flex items-center space-x-1.5 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded text-xs font-semibold font-mono transition-colors disabled:opacity-50 cursor-pointer shadow-xs"
        >
          <Play className="w-3 h-3" />
          <span>Jalankan</span>
        </button>
      </form>
    </div>
  );
};
