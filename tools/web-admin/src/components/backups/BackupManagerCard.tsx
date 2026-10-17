import React, { useState, useEffect, useCallback } from 'react';
import { api } from '../../lib/api';
import { BackupEntry } from '../../types/backup';
import { useAuthStore } from '../../store/authStore';
import { 
  Archive, 
  Download, 
  Trash2, 
  Plus, 
  RefreshCw, 
  Clock, 
  HardDrive, 
  CheckCircle2, 
  AlertCircle 
} from 'lucide-react';

export const BackupManagerCard: React.FC = () => {
  const { user } = useAuthStore();
  const [backups, setBackups] = useState<BackupEntry[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isCreating, setIsCreating] = useState(false);
  const [deletingFile, setDeletingFile] = useState<string | null>(null);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  const fetchBackups = useCallback(async () => {
    setIsLoading(true);
    try {
      const data = await api.backups.list();
      setBackups(data || []);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Gagal memuat daftar backup';
      setMessage({ type: 'error', text: msg });
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchBackups();
  }, [fetchBackups]);

  const handleCreateBackup = async () => {
    setIsCreating(true);
    setMessage(null);
    try {
      const newBackup = await api.backups.create();
      setMessage({
        type: 'success',
        text: `Backup berhasil dibuat: ${newBackup.fileName}`,
      });
      await fetchBackups();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Gagal membuat backup';
      setMessage({ type: 'error', text: msg });
    } finally {
      setIsCreating(false);
    }
  };

  const handleDeleteBackup = async (fileName: string) => {
    if (!window.confirm(`Hapus arsip backup ${fileName}?`)) {
      return;
    }

    setDeletingFile(fileName);
    setMessage(null);
    try {
      await api.backups.delete(fileName);
      setMessage({
        type: 'success',
        text: `Backup ${fileName} berhasil dihapus.`,
      });
      await fetchBackups();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Gagal menghapus backup';
      setMessage({ type: 'error', text: msg });
    } finally {
      setDeletingFile(null);
    }
  };

  const handleDownload = (fileName: string) => {
    const url = api.backups.getDownloadUrl(fileName);
    window.open(url, '_blank');
  };

  const formatSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  };

  const formatDate = (dateStr: string) => {
    try {
      const d = new Date(dateStr);
      return d.toLocaleString();
    } catch {
      return dateStr;
    }
  };

  const isSuperadmin = user?.role === 'SUPERADMIN';

  return (
    <div className="bg-white border border-slate-200 rounded-xl shadow-xs overflow-hidden">
      {/* Header */}
      <div className="px-6 py-4 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3 bg-slate-50/50">
        <div className="flex items-center space-x-3">
          <div className="p-2 bg-slate-900 text-white rounded-lg">
            <Archive className="w-5 h-5" />
          </div>
          <div>
            <h2 className="text-base font-semibold text-slate-900 tracking-tight">
              Manajemen Cadangan (World Save Backups)
            </h2>
            <p className="text-xs text-slate-500">
              Penyimpanan persisten di volume /data/backups/ (Retensi otomatis 30 snapshot)
            </p>
          </div>
        </div>

        <div className="flex items-center space-x-2">
          <button
            type="button"
            onClick={fetchBackups}
            disabled={isLoading}
            className="inline-flex items-center space-x-1.5 px-3 py-1.5 bg-white hover:bg-slate-50 border border-slate-200 rounded-lg text-xs font-medium text-slate-700 transition-colors disabled:opacity-50 cursor-pointer"
            title="Muat ulang daftar"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
            <span>Segarkan</span>
          </button>

          {isSuperadmin && (
            <button
              type="button"
              onClick={handleCreateBackup}
              disabled={isCreating}
              className="inline-flex items-center space-x-1.5 px-3 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-medium transition-colors shadow-xs disabled:opacity-50 cursor-pointer"
            >
              {isCreating ? (
                <>
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                  <span>Membuat Archive...</span>
                </>
              ) : (
                <>
                  <Plus className="w-3.5 h-3.5" />
                  <span>Buat Snapshot Baru</span>
                </>
              )}
            </button>
          )}
        </div>
      </div>

      {/* Notification banner */}
      {message && (
        <div
          className={`px-6 py-3 border-b text-xs flex items-center space-x-2 ${
            message.type === 'success'
              ? 'bg-emerald-50 border-emerald-200 text-emerald-800'
              : 'bg-rose-50 border-rose-200 text-rose-800'
          }`}
        >
          {message.type === 'success' ? (
            <CheckCircle2 className="w-4 h-4 shrink-0" />
          ) : (
            <AlertCircle className="w-4 h-4 shrink-0" />
          )}
          <span>{message.text}</span>
        </div>
      )}

      {/* Backup list table */}
      <div className="overflow-x-auto">
        {backups.length === 0 ? (
          <div className="py-12 text-center text-slate-500">
            <HardDrive className="w-10 h-10 mx-auto text-slate-300 mb-2" />
            <p className="text-sm font-medium text-slate-700">Belum Ada Snapshot Tersimpan</p>
            <p className="text-xs text-slate-400 mt-1">
              Backup otomatis dibuat setiap hari in-game disimpan (GameLoop.Saved) atau klik tombol &quot;Buat Snapshot Baru&quot;.
            </p>
          </div>
        ) : (
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50/75 border-b border-slate-200 text-slate-500 uppercase tracking-wider font-semibold">
              <tr>
                <th className="px-6 py-3">Nama Arsip</th>
                <th className="px-6 py-3">Waktu Pembuatan (UTC)</th>
                <th className="px-6 py-3">Ukuran File</th>
                <th className="px-6 py-3 text-right">Aksi</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 text-slate-700">
              {backups.map((b) => (
                <tr key={b.fileName} className="hover:bg-slate-50/75 transition-colors">
                  <td className="px-6 py-3.5 font-mono text-slate-900 font-medium">
                    <div className="flex items-center space-x-2">
                      <Archive className="w-4 h-4 text-slate-400 shrink-0" />
                      <span>{b.fileName}</span>
                    </div>
                  </td>
                  <td className="px-6 py-3.5">
                    <div className="flex items-center space-x-1.5 text-slate-600">
                      <Clock className="w-3.5 h-3.5 text-slate-400" />
                      <span>{formatDate(b.createdAtUtc)}</span>
                    </div>
                  </td>
                  <td className="px-6 py-3.5 font-medium text-slate-800">
                    {formatSize(b.sizeBytes)}
                  </td>
                  <td className="px-6 py-3.5 text-right space-x-2 whitespace-nowrap">
                    <button
                      type="button"
                      onClick={() => handleDownload(b.fileName)}
                      className="inline-flex items-center space-x-1 px-2.5 py-1 bg-slate-100 hover:bg-slate-200 text-slate-700 border border-slate-200 rounded-md font-medium transition-colors cursor-pointer"
                      title="Unduh file zip"
                    >
                      <Download className="w-3.5 h-3.5" />
                      <span>Unduh</span>
                    </button>

                    {isSuperadmin && (
                      <button
                        type="button"
                        onClick={() => handleDeleteBackup(b.fileName)}
                        disabled={deletingFile === b.fileName}
                        className="inline-flex items-center space-x-1 px-2.5 py-1 bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 rounded-md font-medium transition-colors disabled:opacity-50 cursor-pointer"
                        title="Hapus backup"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                        <span>Hapus</span>
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
};
