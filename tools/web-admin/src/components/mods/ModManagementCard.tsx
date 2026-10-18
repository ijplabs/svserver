import React, { useState, useEffect, useRef } from 'react';
import {
  Blocks,
  ShieldCheck,
  ShieldAlert,
  CheckCircle2,
  XCircle,
  Plus,
  Trash2,
  RefreshCw,
  AlertTriangle,
  Users,
  Layers,
  Download,
  Laptop,
  HardDriveDownload,
  FileCode,
  Upload,
  HardDrive,
  FileArchive,
  Copy,
  Check
} from 'lucide-react';
import { api, type UploadProgressData } from '../../lib/api';
import type {
  ServerInstalledMod,
  ModPolicy,
  ConnectedPeerAudit,
  ModRejectionLog,
  CustomModItem
} from '../../types/mods';

export const ModManagementCard: React.FC = () => {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [installedMods, setInstalledMods] = useState<ServerInstalledMod[]>([]);
  const [customMods, setCustomMods] = useState<CustomModItem[]>([]);
  const [policy, setPolicy] = useState<ModPolicy>({
    enforcementMode: 'Disabled',
    unknownModsMode: 'Ignore',
    requiredMods: [],
    blacklistedMods: [],
    whitelistModIds: []
  });
  const [rejections, setRejections] = useState<ModRejectionLog[]>([]);
  const [connectedPeers, setConnectedPeers] = useState<ConnectedPeerAudit[]>([]);
  const [syncManifest, setSyncManifest] = useState<any>(null);
  const [activeSubTab, setActiveSubTab] = useState<'required' | 'blacklisted' | 'server' | 'custom' | 'audit' | 'distribution'>('required');
  const [message, setMessage] = useState<{ text: string; type: 'success' | 'error' } | null>(null);
  const [copiedScript, setCopiedScript] = useState(false);

  // Upload state
  const [uploading, setUploading] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploadProgressData, setUploadProgressData] = useState<UploadProgressData | null>(null);
  const [uploadLocalMessage, setUploadLocalMessage] = useState<{ text: string; type: 'success' | 'error' } | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // New Required Mod Form State
  const [newReqId, setNewReqId] = useState('');
  const [newReqName, setNewReqName] = useState('');
  const [newReqMinVer, setNewReqMinVer] = useState('');

  // New Blacklisted Mod Form State
  const [newBlackId, setNewBlackId] = useState('');
  const [newBlackName, setNewBlackName] = useState('');
  const [newBlackReason, setNewBlackReason] = useState('');

  const fetchData = async () => {
    try {
      setLoading(true);
      const [policyRes, installedRes, manifestRes, customRes] = await Promise.all([
        api.mods.getPolicy(),
        api.mods.getInstalled(),
        api.mods.getSyncManifest().catch(() => null),
        api.mods.getCustomMods().catch(() => [])
      ]);
      setPolicy(policyRes.policy);
      setRejections(policyRes.rejections || []);
      setConnectedPeers(policyRes.connectedPeers || []);
      setInstalledMods(installedRes || []);
      setSyncManifest(manifestRes);
      setCustomMods(customRes || []);
    } catch (err: any) {
      setMessage({ text: err.message || 'Gagal memuat data kebijakan mod', type: 'error' });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
    const interval = setInterval(fetchData, 8000);
    return () => clearInterval(interval);
  }, []);

  const handleSavePolicy = async (updatedPolicy: ModPolicy) => {
    try {
      setSaving(true);
      const res = await api.mods.updatePolicy(updatedPolicy);
      setPolicy(res.policy);
      setMessage({ text: 'Kebijakan mod berhasil disimpan', type: 'success' });
      setTimeout(() => setMessage(null), 4000);
    } catch (err: any) {
      setMessage({ text: err.message || 'Gagal menyimpan kebijakan mod', type: 'error' });
    } finally {
      setSaving(false);
    }
  };

  const handleSyncSve = async () => {
    try {
      setSyncing(true);
      const res = await api.mods.syncSve();
      setPolicy(res.policy);
      setMessage({ text: res.message || 'Sinkronisasi mod server berhasil', type: 'success' });
      setTimeout(() => setMessage(null), 4000);
    } catch (err: any) {
      setMessage({ text: err.message || 'Gagal menyinkronkan mod SVE', type: 'error' });
    } finally {
      setSyncing(false);
    }
  };

  const handleAddRequiredMod = () => {
    if (!newReqId.trim()) return;
    const updated: ModPolicy = {
      ...policy,
      requiredMods: [
        ...policy.requiredMods,
        {
          uniqueId: newReqId.trim(),
          name: newReqName.trim() || newReqId.trim(),
          minVersion: newReqMinVer.trim() || undefined,
          required: true
        }
      ]
    };
    setNewReqId('');
    setNewReqName('');
    setNewReqMinVer('');
    handleSavePolicy(updated);
  };

  const handleRemoveRequiredMod = (id: string) => {
    const updated: ModPolicy = {
      ...policy,
      requiredMods: policy.requiredMods.filter((m) => m.uniqueId !== id)
    };
    handleSavePolicy(updated);
  };

  const handleAddBlacklistedMod = () => {
    if (!newBlackId.trim()) return;
    const updated: ModPolicy = {
      ...policy,
      blacklistedMods: [
        ...policy.blacklistedMods,
        {
          uniqueId: newBlackId.trim(),
          name: newBlackName.trim() || newBlackId.trim(),
          reason: newBlackReason.trim() || 'Mod dilarang oleh peraturan server'
        }
      ]
    };
    setNewBlackId('');
    setNewBlackName('');
    setNewBlackReason('');
    handleSavePolicy(updated);
  };

  const handleRemoveBlacklistedMod = (id: string) => {
    const updated: ModPolicy = {
      ...policy,
      blacklistedMods: policy.blacklistedMods.filter((m) => m.uniqueId !== id)
    };
    handleSavePolicy(updated);
  };

  const handleFileUpload = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!selectedFile) {
      fileInputRef.current?.click();
      return;
    }
    try {
      setUploading(true);
      setUploadLocalMessage(null);
      setUploadProgressData({
        loadedBytes: 0,
        totalBytes: selectedFile.size,
        percentage: 0,
        stage: 'uploading',
        statusText: `Menyiapkan pengunggahan "${selectedFile.name}" (${(selectedFile.size / (1024 * 1024)).toFixed(1)} MB)...`
      });

      const res = await api.mods.uploadMod(selectedFile, (progress) => {
        setUploadProgressData(progress);
      });

      const successText = res?.message || `Mod "${selectedFile.name}" berhasil diunggah dan disimpan ke volume persisten.`;
      setMessage({ text: successText, type: 'success' });
      setUploadLocalMessage({ text: successText, type: 'success' });
      setSelectedFile(null);
      setUploadProgressData(null);
      if (fileInputRef.current) fileInputRef.current.value = '';
      await fetchData();
    } catch (err: any) {
      let errorText = err.message || 'Gagal mengunggah mod.';
      if (errorText.toLowerCase().includes('failed to fetch')) {
        errorText = 'Gagal mengirim file ke server (Failed to fetch). Pastikan koneksi internet stabil dan server aktif.';
      }
      setMessage({ text: errorText, type: 'error' });
      setUploadLocalMessage({ text: errorText, type: 'error' });
      setUploadProgressData(null);
    } finally {
      setUploading(false);
    }
  };

  const handleDeleteCustomMod = async (folderName: string) => {
    if (!window.confirm(`Hapus mod "${folderName}" dari server? Mod akan dihapus secara permanen dari penyimpanan persisten.`)) {
      return;
    }
    try {
      await api.mods.deleteCustomMod(folderName);
      setMessage({ text: `Mod "${folderName}" berhasil dihapus dari server.`, type: 'success' });
      await fetchData();
    } catch (err: any) {
      setMessage({ text: err.message || 'Gagal menghapus mod.', type: 'error' });
    }
  };

  const handleMakeRequired = (mod: CustomModItem) => {
    if (policy.requiredMods.some((r) => r.uniqueId === mod.uniqueId)) {
      setMessage({ text: `Mod "${mod.name}" sudah ada di daftar Mod Wajib.`, type: 'error' });
      return;
    }
    const updated: ModPolicy = {
      ...policy,
      requiredMods: [
        ...policy.requiredMods,
        {
          uniqueId: mod.uniqueId,
          name: mod.name,
          minVersion: mod.version !== 'custom' ? mod.version : undefined,
          required: true
        }
      ]
    };
    handleSavePolicy(updated);
    setMessage({ text: `Mod "${mod.name}" ditambahkan ke daftar Mod Wajib.`, type: 'success' });
  };

  return (
    <div className="space-y-6">
      {/* Top Banner / Actions */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] p-6 rounded-2xl">
        <div className="flex items-center gap-3">
          <div className="p-3 bg-indigo-500/10 border border-indigo-500/20 rounded-xl text-indigo-400">
            <Blocks className="w-6 h-6" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-slate-100">Pusat Mod & Validasi Klien</h2>
            <p className="text-sm text-slate-400">
              Validasi otomatis mod pemain langsung dari server menggunakan protokol SMAPI multiplayer.
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={fetchData}
            disabled={loading}
            className="flex items-center gap-2 px-3 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 text-sm font-medium rounded-xl border border-slate-700 transition"
            title="Muat Ulang"
          >
            <RefreshCw className={`w-4 h-4 ${loading ? 'animate-spin' : ''}`} />
          </button>
          <button
            onClick={handleSyncSve}
            disabled={syncing}
            className="flex items-center gap-2 px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 text-sm font-medium rounded-xl border border-slate-700 transition"
          >
            <RefreshCw className={`w-4 h-4 ${syncing ? 'animate-spin' : ''}`} />
            Auto-Sync Mod Server
          </button>
        </div>
      </div>

      {message && (
        <div
          className={`flex items-center gap-2 p-4 rounded-xl text-sm font-medium ${
            message.type === 'success'
              ? 'bg-emerald-500/10 border border-emerald-500/20 text-emerald-400'
              : 'bg-rose-500/10 border border-rose-500/20 text-rose-400'
          }`}
        >
          {message.type === 'success' ? <CheckCircle2 className="w-4 h-4" /> : <AlertTriangle className="w-4 h-4" />}
          {message.text}
        </div>
      )}

      {/* Enforcement Mode Selector */}
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] p-6 rounded-2xl space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-base font-semibold text-slate-200">Mode Penegakan Kebijakan Klien</h3>
            <p className="text-xs text-slate-400">
              Tentukan bagaimana server memeriksa klien yang mencoba login ke multiplayer.
            </p>
          </div>
          <span
            className={`text-xs px-2.5 py-1 rounded-full font-semibold border ${
              policy.enforcementMode === 'StrictRequired'
                ? 'bg-rose-500/10 border-rose-500/20 text-rose-400'
                : policy.enforcementMode === 'RequireSmapi'
                ? 'bg-amber-500/10 border-amber-500/20 text-amber-400'
                : 'bg-slate-800 border-slate-700 text-slate-400'
            }`}
          >
            {policy.enforcementMode === 'StrictRequired'
              ? 'Validasi Ketat Mod'
              : policy.enforcementMode === 'RequireSmapi'
              ? 'Wajib SMAPI'
              : 'Bebas / Nonaktif'}
          </span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div
            onClick={() => handleSavePolicy({ ...policy, enforcementMode: 'Disabled' })}
            className={`cursor-pointer p-4 rounded-xl border transition ${
              policy.enforcementMode === 'Disabled'
                ? 'bg-slate-800/80 border-indigo-500/50 shadow-sm'
                : 'bg-slate-950/40 border-slate-800 hover:border-slate-700'
            }`}
          >
            <div className="flex items-center gap-2 mb-2">
              <CheckCircle2
                className={`w-4 h-4 ${
                  policy.enforcementMode === 'Disabled' ? 'text-indigo-400' : 'text-slate-500'
                }`}
              />
              <span className="text-sm font-semibold text-slate-200">Nonaktif (Bebas)</span>
            </div>
            <p className="text-xs text-slate-400">
              Pemain vanilla maupun bermod bebas bergabung tanpa pengecekan integritas mod.
            </p>
          </div>

          <div
            onClick={() => handleSavePolicy({ ...policy, enforcementMode: 'RequireSmapi' })}
            className={`cursor-pointer p-4 rounded-xl border transition ${
              policy.enforcementMode === 'RequireSmapi'
                ? 'bg-slate-800/80 border-amber-500/50 shadow-sm'
                : 'bg-slate-950/40 border-slate-800 hover:border-slate-700'
            }`}
          >
            <div className="flex items-center gap-2 mb-2">
              <ShieldCheck
                className={`w-4 h-4 ${
                  policy.enforcementMode === 'RequireSmapi' ? 'text-amber-400' : 'text-slate-500'
                }`}
              />
              <span className="text-sm font-semibold text-slate-200">Wajib SMAPI</span>
            </div>
            <p className="text-xs text-slate-400">
              Menolak klien vanilla murni. Pemain wajib menggunakan SMAPI untuk login.
            </p>
          </div>

          <div
            onClick={() => handleSavePolicy({ ...policy, enforcementMode: 'StrictRequired' })}
            className={`cursor-pointer p-4 rounded-xl border transition ${
              policy.enforcementMode === 'StrictRequired'
                ? 'bg-slate-800/80 border-rose-500/50 shadow-sm'
                : 'bg-slate-950/40 border-slate-800 hover:border-slate-700'
            }`}
          >
            <div className="flex items-center gap-2 mb-2">
              <ShieldAlert
                className={`w-4 h-4 ${
                  policy.enforcementMode === 'StrictRequired' ? 'text-rose-400' : 'text-slate-500'
                }`}
              />
              <span className="text-sm font-semibold text-slate-200">Validasi Ketat Mod</span>
            </div>
            <p className="text-xs text-slate-400">
              Pemain wajib memiliki seluruh mod yang ada pada Daftar Mod Wajib dengan versi yang kompatibel.
            </p>
          </div>
        </div>
      </div>

      {/* Unknown Mods Behavior Selector */}
      <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] p-6 rounded-2xl space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-base font-semibold text-slate-200">Kebijakan Mod Asing / Mod Klien Lain</h3>
            <p className="text-xs text-slate-400">
              Pengaturan perlakuan terhadap mod lain di PC pemain (misal mod yang pernah dicoba sebelumnya atau mod kosmetik).
            </p>
          </div>
          <span
            className={`text-xs px-2.5 py-1 rounded-full font-semibold border ${
              (policy.unknownModsMode || 'Ignore') === 'StrictReject'
                ? 'bg-rose-500/10 border-rose-500/20 text-rose-400'
                : 'bg-emerald-500/10 border-emerald-500/20 text-emerald-400'
            }`}
          >
            {(policy.unknownModsMode || 'Ignore') === 'StrictReject'
              ? 'Whitelist Ketat (Tolak Lainnya)'
              : 'Abaikan Mod Lain (Direkomendasikan)'}
          </span>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div
            onClick={() => handleSavePolicy({ ...policy, unknownModsMode: 'Ignore' })}
            className={`cursor-pointer p-4 rounded-xl border transition ${
              (policy.unknownModsMode || 'Ignore') === 'Ignore'
                ? 'bg-slate-800/80 border-emerald-500/50 shadow-sm'
                : 'bg-slate-950/40 border-slate-800 hover:border-slate-700'
            }`}
          >
            <div className="flex items-center gap-2 mb-2">
              <CheckCircle2
                className={`w-4 h-4 ${
                  (policy.unknownModsMode || 'Ignore') === 'Ignore' ? 'text-emerald-400' : 'text-slate-500'
                }`}
              />
              <span className="text-sm font-semibold text-slate-200">Abaikan Mod Lain (Rekomendasi Fleksibel)</span>
            </div>
            <p className="text-xs text-slate-400">
              Mod kosmetik pribadi, visual, UI info, atau mod yang pernah dicoba pemain sebelumnya dibiarkan berjalan aman. Pemain dapat login lancar tanpa ditolak selama memiliki mod server wajib.
            </p>
          </div>

          <div
            onClick={() => handleSavePolicy({ ...policy, unknownModsMode: 'StrictReject' })}
            className={`cursor-pointer p-4 rounded-xl border transition ${
              policy.unknownModsMode === 'StrictReject'
                ? 'bg-slate-800/80 border-rose-500/50 shadow-sm'
                : 'bg-slate-950/40 border-slate-800 hover:border-slate-700'
            }`}
          >
            <div className="flex items-center gap-2 mb-2">
              <ShieldAlert
                className={`w-4 h-4 ${
                  policy.unknownModsMode === 'StrictReject' ? 'text-rose-400' : 'text-slate-500'
                }`}
              />
              <span className="text-sm font-semibold text-slate-200">Whitelist Ketat (Tolak Mod di Luar Daftar)</span>
            </div>
            <p className="text-xs text-slate-400">
              Hanya mod resmi server dan whitelist yang diizinkan. Pemain yang membawa mod asing lain akan ditolak masuk ke dalam server demi keseragaman gameplay 100%.
            </p>
          </div>
        </div>
      </div>

      {/* Sub-tab Navigation */}
      <div className="flex items-center gap-2 border-b border-slate-800 pb-2 overflow-x-auto">
        <button
          onClick={() => setActiveSubTab('required')}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-medium transition whitespace-nowrap ${
            activeSubTab === 'required'
              ? 'bg-indigo-600 text-white'
              : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800'
          }`}
        >
          <ShieldCheck className="w-4 h-4" />
          Mod Wajib ({policy.requiredMods.length})
        </button>

        <button
          onClick={() => setActiveSubTab('blacklisted')}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-medium transition whitespace-nowrap ${
            activeSubTab === 'blacklisted'
              ? 'bg-indigo-600 text-white'
              : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800'
          }`}
        >
          <XCircle className="w-4 h-4" />
          Mod Dilarang ({policy.blacklistedMods.length})
        </button>

        <button
          onClick={() => setActiveSubTab('custom')}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-medium transition whitespace-nowrap ${
            activeSubTab === 'custom'
              ? 'bg-indigo-600 text-white'
              : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800'
          }`}
        >
          <Upload className="w-4 h-4" />
          Unggah & Mod Persisten ({customMods.length})
        </button>

        <button
          onClick={() => setActiveSubTab('server')}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-medium transition whitespace-nowrap ${
            activeSubTab === 'server'
              ? 'bg-indigo-600 text-white'
              : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800'
          }`}
        >
          <Layers className="w-4 h-4" />
          Mod Server ({installedMods.length})
        </button>

        <button
          onClick={() => setActiveSubTab('audit')}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-medium transition ${
            activeSubTab === 'audit'
              ? 'bg-indigo-600 text-white'
              : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800'
          }`}
        >
          <Users className="w-4 h-4" />
          Audit & Log Penolakan
        </button>

        <button
          onClick={() => setActiveSubTab('distribution')}
          className={`flex items-center gap-2 px-4 py-2 rounded-xl text-sm font-medium transition ${
            activeSubTab === 'distribution'
              ? 'bg-indigo-600 text-white'
              : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800'
          }`}
        >
          <Download className="w-4 h-4" />
          Distribusi Klien (EZ Setup)
        </button>
      </div>

      {/* Sub-tab 1: Mod Wajib (Required Mods) */}
      {activeSubTab === 'required' && (
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 space-y-6">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div>
              <h3 className="text-base font-semibold text-slate-100">Daftar Mod Wajib (Required Mods)</h3>
              <p className="text-xs text-slate-400">
                Pemain yang tidak memasang mod-mod ini akan otomatis ditolak dan diputuskan dari server.
              </p>
            </div>
          </div>

          {/* Form Tambah Mod Wajib */}
          <div className="bg-slate-950/60 border border-slate-800 p-4 rounded-xl space-y-3">
            <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-400">Tambah Aturan Mod Wajib</h4>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <input
                type="text"
                placeholder="Unique ID (misal: Pathoschild.ContentPatcher)"
                value={newReqId}
                onChange={(e) => setNewReqId(e.target.value)}
                className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
              />
              <input
                type="text"
                placeholder="Nama Mod (misal: Content Patcher)"
                value={newReqName}
                onChange={(e) => setNewReqName(e.target.value)}
                className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
              />
              <div className="flex gap-2">
                <input
                  type="text"
                  placeholder="Min Versi (opsional, misal: 2.0.0)"
                  value={newReqMinVer}
                  onChange={(e) => setNewReqMinVer(e.target.value)}
                  className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
                />
                <button
                  onClick={handleAddRequiredMod}
                  disabled={saving || !newReqId.trim()}
                  className="flex items-center gap-1 px-3 py-2 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold whitespace-nowrap transition"
                >
                  <Plus className="w-3.5 h-3.5" />
                  Tambah
                </button>
              </div>
            </div>
          </div>

          {/* Tabel Mod Wajib */}
          {policy.requiredMods.length === 0 ? (
            <div className="text-center py-8 text-slate-500 text-sm">
              Belum ada mod wajib yang dikonfigurasi. Klik tombol "Auto-Sync Mod Server" di atas untuk menambahkan mod ekspansi seperti SVE secara otomatis.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="bg-slate-950/40 text-slate-400 font-semibold border-b border-slate-800">
                  <tr>
                    <th className="p-3">Nama Mod</th>
                    <th className="p-3">Unique ID</th>
                    <th className="p-3">Versi Minimum</th>
                    <th className="p-3 text-right">Aksi</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/60 text-slate-200">
                  {policy.requiredMods.map((mod) => (
                    <tr key={mod.uniqueId} className="hover:bg-slate-800/30 transition">
                      <td className="p-3 font-medium text-slate-100">{mod.name}</td>
                      <td className="p-3 font-mono text-slate-400">{mod.uniqueId}</td>
                      <td className="p-3">
                        {mod.minVersion ? (
                          <span className="px-2 py-0.5 rounded bg-slate-800 text-slate-300 font-mono">
                            {mod.minVersion}
                          </span>
                        ) : (
                          <span className="text-slate-500">Versi apa saja</span>
                        )}
                      </td>
                      <td className="p-3 text-right">
                        <button
                          onClick={() => handleRemoveRequiredMod(mod.uniqueId)}
                          disabled={saving}
                          className="p-1.5 hover:bg-rose-500/10 text-rose-400 rounded-lg transition"
                          title="Hapus dari daftar wajib"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* Sub-tab 2: Mod Dilarang (Blacklisted Mods) */}
      {activeSubTab === 'blacklisted' && (
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 space-y-6">
          <div>
            <h3 className="text-base font-semibold text-slate-100">Daftar Mod Dilarang (Blacklisted Mods)</h3>
            <p className="text-xs text-slate-400">
              Pemain yang terdeteksi membawa mod-mod ini akan langsung ditolak saat mencoba login.
            </p>
          </div>

          {/* Form Tambah Mod Dilarang */}
          <div className="bg-slate-950/60 border border-slate-800 p-4 rounded-xl space-y-3">
            <h4 className="text-xs font-semibold uppercase tracking-wider text-slate-400">Tambah Mod Terlarang</h4>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <input
                type="text"
                placeholder="Unique ID (misal: CJBok.CheatsMenu)"
                value={newBlackId}
                onChange={(e) => setNewBlackId(e.target.value)}
                className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
              />
              <input
                type="text"
                placeholder="Nama Mod (misal: Cheats Menu)"
                value={newBlackName}
                onChange={(e) => setNewBlackName(e.target.value)}
                className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
              />
              <div className="flex gap-2">
                <input
                  type="text"
                  placeholder="Alasan penolakan"
                  value={newBlackReason}
                  onChange={(e) => setNewBlackReason(e.target.value)}
                  className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-100 focus:outline-none focus:border-indigo-500"
                />
                <button
                  onClick={handleAddBlacklistedMod}
                  disabled={saving || !newBlackId.trim()}
                  className="flex items-center gap-1 px-3 py-2 bg-rose-600 hover:bg-rose-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold whitespace-nowrap transition"
                >
                  <Plus className="w-3.5 h-3.5" />
                  Tambah
                </button>
              </div>
            </div>
          </div>

          {/* Tabel Mod Terlarang */}
          {policy.blacklistedMods.length === 0 ? (
            <div className="text-center py-8 text-slate-500 text-sm">
              Tidak ada mod yang dilarang saat ini.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="bg-slate-950/40 text-slate-400 font-semibold border-b border-slate-800">
                  <tr>
                    <th className="p-3">Nama Mod</th>
                    <th className="p-3">Unique ID</th>
                    <th className="p-3">Alasan Penolakan</th>
                    <th className="p-3 text-right">Aksi</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800/60 text-slate-200">
                  {policy.blacklistedMods.map((mod) => (
                    <tr key={mod.uniqueId} className="hover:bg-slate-800/30 transition">
                      <td className="p-3 font-medium text-slate-100">{mod.name}</td>
                      <td className="p-3 font-mono text-slate-400">{mod.uniqueId}</td>
                      <td className="p-3 text-slate-300">{mod.reason}</td>
                      <td className="p-3 text-right">
                        <button
                          onClick={() => handleRemoveBlacklistedMod(mod.uniqueId)}
                          disabled={saving}
                          className="p-1.5 hover:bg-rose-500/10 text-rose-400 rounded-lg transition"
                          title="Hapus dari daftar hitam"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* Sub-tab 3: Mod Terpasang di Server (Server-Installed Mods) */}
      {activeSubTab === 'server' && (
        <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-base font-semibold text-slate-100">Mod Terpasang di Server ({installedMods.length})</h3>
              <p className="text-xs text-slate-400">
                Daftar mod yang sedang berjalan di dalam kontainer SMAPI server host.
              </p>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {installedMods.map((mod) => (
              <div
                key={mod.uniqueId}
                className="p-4 bg-slate-950/50 border border-slate-800 rounded-xl space-y-2 hover:border-slate-700 transition"
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="space-y-0.5">
                    <h4 className="text-sm font-semibold text-slate-200">{mod.name}</h4>
                    <p className="text-xs font-mono text-slate-400">{mod.uniqueId}</p>
                  </div>
                  <span className="px-2 py-0.5 text-xs font-mono rounded bg-slate-800 text-indigo-400 border border-slate-700">
                    v{mod.version}
                  </span>
                </div>
                {mod.author && <p className="text-xs text-slate-500">Author: {mod.author}</p>}
                {mod.description && (
                  <p className="text-xs text-slate-400 line-clamp-2">{mod.description}</p>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Sub-tab 4: Audit & Log Penolakan */}
      {activeSubTab === 'audit' && (
        <div className="space-y-6">
          {/* Pemain Online Mod Audit */}
          <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 space-y-4">
            <h3 className="text-base font-semibold text-slate-100">Mod Pemain yang Sedang Online</h3>
            {connectedPeers.length === 0 ? (
              <p className="text-xs text-slate-500">Tidak ada pemain yang sedang terhubung.</p>
            ) : (
              <div className="space-y-3">
                {connectedPeers.map((peer) => (
                  <div key={peer.playerId} className="p-4 bg-slate-950/60 border border-slate-800 rounded-xl space-y-2">
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <Users className="w-4 h-4 text-indigo-400" />
                        <span className="text-sm font-semibold text-slate-200">{peer.farmerName}</span>
                        <span className="text-xs text-slate-400 font-mono">#{peer.playerId}</span>
                      </div>
                      <span className="text-xs px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-medium">
                        SMAPI: {peer.smapiVersion} ({peer.modCount} mod)
                      </span>
                    </div>

                    <div className="flex flex-wrap gap-1.5 pt-1">
                      {peer.mods.map((m) => (
                        <span
                          key={m.uniqueId}
                          className="px-2 py-0.5 text-xs bg-slate-800/80 text-slate-300 rounded border border-slate-700 font-mono"
                        >
                          {m.uniqueId} <span className="text-slate-500">v{m.version}</span>
                        </span>
                      ))}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Riwayat Penolakan Login */}
          <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 space-y-4">
            <h3 className="text-base font-semibold text-slate-100">Riwayat Penolakan Login Klien</h3>
            {rejections.length === 0 ? (
              <p className="text-xs text-slate-500">Belum ada riwayat penolakan klien.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs">
                  <thead className="bg-slate-950/40 text-slate-400 font-semibold border-b border-slate-800">
                    <tr>
                      <th className="p-3">Waktu (UTC)</th>
                      <th className="p-3">Nama Pemain</th>
                      <th className="p-3">Alasan Penolakan</th>
                      <th className="p-3">Target Mod</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800/60 text-slate-200">
                    {rejections.map((rej, idx) => (
                      <tr key={idx} className="hover:bg-slate-800/30 transition">
                        <td className="p-3 text-slate-400 font-mono">
                          {new Date(rej.timestampUtc).toLocaleTimeString()}
                        </td>
                        <td className="p-3 font-semibold text-slate-100">{rej.farmerName}</td>
                        <td className="p-3 text-rose-400">{rej.reason}</td>
                        <td className="p-3 font-mono text-slate-400">{rej.modId || '-'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Sub-tab 5: Distribusi Klien & In-Game Lobby Sync */}
      {activeSubTab === 'distribution' && (
        <div className="space-y-6">
          {/* Header Info Card */}
          <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 space-y-4">
            <div className="flex items-start justify-between gap-4">
              <div className="space-y-1">
                <div className="flex items-center gap-2">
                  <h3 className="text-base font-semibold text-slate-100">
                    Sistem Distribusi Mod Terpusat & In-Game Lobby Sync
                  </h3>
                  <span className="px-2 py-0.5 text-xs font-medium rounded-full bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
                    EZ Setup Klien
                  </span>
                </div>
                <p className="text-xs text-slate-400 max-w-3xl">
                  Pemain tidak perlu lagi mengunduh mod manual dari NexusMods. Cukup pasang mod klien kecil
                  <code className="mx-1 px-1.5 py-0.5 bg-slate-950 text-indigo-300 rounded border border-slate-800">
                    JunimoClientSync
                  </code>
                  atau jalankan script otomatis. Di layar Title dan Lobby Co-op Stardew Valley, tombol
                  <span className="text-slate-200 font-semibold mx-1">"Sync Server Mods"</span>
                  akan muncul otomatis untuk mengunduh dan menyinkronkan seluruh mod langsung dari server host.
                </p>
              </div>
            </div>

            {/* 3 Download Action Cards */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 pt-2">
              {/* Card 1: Mod Klien JunimoClientSync */}
              <div className="p-5 bg-slate-950/60 border border-slate-800 rounded-xl space-y-3 flex flex-col justify-between">
                <div className="space-y-2">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2 text-indigo-400">
                      <Laptop className="w-5 h-5" />
                      <h4 className="text-sm font-semibold text-slate-200">Mod Klien SMAPI</h4>
                      <span className="px-1.5 py-0.5 rounded text-[10px] font-mono font-bold bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                        v1.1.1
                      </span>
                    </div>
                    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                      <ShieldCheck className="w-3 h-3 text-emerald-400" /> Terverifikasi
                    </span>
                  </div>
                  <p className="text-xs text-slate-400">
                    Mod klien berukuran kecil (~25 KB) dengan dukungan <strong>Chunked Download (5 MB parts)</strong> & sertifikat digital resmi. Tombol sinkronisasi langsung di Lobby Co-op & Title Menu.
                  </p>
                  <div className="space-y-1">
                    <div className="flex items-center justify-between text-[11px]">
                      <span className="text-slate-400">Versi Saat Ini:</span>
                      <span className="font-mono text-emerald-400 font-semibold">v1.1.1 (Chunked & Path Fixed)</span>
                    </div>
                    <p className="text-[11px] font-mono text-slate-400 truncate">Penerbit: Junimo Dedicated Server Security Authority</p>
                    <p className="text-[10px] font-mono text-slate-500 truncate" title="SHA-256: 7D37BC62637ADF216B4CB0530893E1811AE197FA5F24F3AA6717CF4669BCFAE2">
                      SHA256: 7D37BC62637ADF21...
                    </p>
                  </div>
                </div>

                <div className="space-y-2 pt-2">
                  <a
                    href="/JunimoClientSync.zip"
                    download="JunimoClientSync-v1.1.1.zip"
                    className="w-full inline-flex items-center justify-center gap-2 px-4 py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-medium rounded-xl transition shadow-sm"
                  >
                    <Download className="w-4 h-4" />
                    Unduh JunimoClientSync v1.1.1 (.zip)
                  </a>
                  <a
                    href="/JunimoSecurityCert.cer"
                    download="JunimoSecurityCert.cer"
                    className="w-full inline-flex items-center justify-center gap-2 px-3 py-1.5 bg-slate-900 hover:bg-slate-800 text-slate-300 hover:text-white text-[11px] font-medium rounded-lg border border-slate-800 transition"
                  >
                    <ShieldCheck className="w-3.5 h-3.5 text-emerald-400" />
                    Unduh Sertifikat Keamanan (.cer)
                  </a>
                </div>
              </div>

              {/* Card 2: 1-Click EZ Setup Batch Script */}
              <div className="p-5 bg-slate-950/60 border border-slate-800 rounded-xl space-y-3 flex flex-col justify-between">
                <div className="space-y-2">
                  <div className="flex items-center gap-2 text-emerald-400">
                    <FileCode className="w-5 h-5" />
                    <h4 className="text-sm font-semibold text-slate-200">Script 1-Klik (Windows)</h4>
                  </div>
                  <p className="text-xs text-slate-400">
                    Script otomatis untuk teman yang belum paham cara pasang mod. Otomatis mendeteksi folder Steam, memasang mod sync, dan mendaftarkan sertifikat keamanan ke Windows.
                  </p>
                  <p className="text-xs font-mono text-slate-500">Eksekusi: sync-mods.bat</p>
                </div>

                <div className="space-y-2 pt-2">
                  <a
                    href="/sync-mods.bat"
                    download="sync-mods.bat"
                    className="w-full inline-flex items-center justify-center gap-2 px-4 py-2.5 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-medium rounded-xl transition shadow-sm"
                  >
                    <Download className="w-4 h-4" />
                    Unduh sync-mods.bat
                  </a>
                  <button
                    type="button"
                    onClick={() => {
                      const cmd = `powershell -NoProfile -Command "try { $wc = New-Object System.Net.WebClient; $wc.DownloadFile('https://stardew-api.ijplabs.dev/api/mods/sync/client-mod', 'JunimoClientSync.zip'); Add-Type -AssemblyName System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::ExtractToDirectory('JunimoClientSync.zip', '$env:APPDATA\\StardewValley\\Mods', $true); Remove-Item 'JunimoClientSync.zip'; Write-Host '[OK] JunimoClientSync v1.1.1 Berhasil Dipasang!' -ForegroundColor Green } catch { Write-Host $_.Exception.Message -ForegroundColor Red }"`;
                      navigator.clipboard.writeText(cmd);
                      setCopiedScript(true);
                      setTimeout(() => setCopiedScript(false), 2500);
                    }}
                    className="w-full inline-flex items-center justify-center gap-2 px-3 py-1.5 bg-slate-900 hover:bg-slate-800 text-slate-300 hover:text-white text-[11px] font-medium rounded-lg border border-slate-800 transition cursor-pointer"
                  >
                    {copiedScript ? (
                      <>
                        <Check className="w-3.5 h-3.5 text-emerald-400" />
                        <span className="text-emerald-400 font-semibold">Perintah Disalin ke Clipboard!</span>
                      </>
                    ) : (
                      <>
                        <Copy className="w-3.5 h-3.5 text-slate-400" />
                        <span>Salin Perintah PowerShell (Tanpa Download)</span>
                      </>
                    )}
                  </button>
                </div>
              </div>

              {/* Card 3: Full Server Modpack */}
              <div className="p-5 bg-slate-950/60 border border-slate-800 rounded-xl space-y-3 flex flex-col justify-between">
                <div className="space-y-2">
                  <div className="flex items-center gap-2 text-cyan-400">
                    <HardDriveDownload className="w-5 h-5" />
                    <h4 className="text-sm font-semibold text-slate-200">Paket Lengkap Modpack</h4>
                  </div>
                  <p className="text-xs text-slate-400">
                    Paket zip utuh berisi seluruh folder mod yang aktif di server (SVE, Content Patcher, dsb) untuk pemain yang ingin ekstraksi manual secara offline.
                  </p>
                  <p className="text-xs font-mono text-slate-500">Ukuran: Sesuai total folder mod server</p>
                </div>

                <a
                  href="https://stardew-api.ijplabs.dev/api/mods/sync/bundle"
                  target="_blank"
                  rel="noreferrer"
                  className="w-full inline-flex items-center justify-center gap-2 px-4 py-2.5 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium rounded-xl border border-slate-700 transition"
                >
                  <Download className="w-4 h-4" />
                  Unduh Modpack Server (.zip)
                </a>
              </div>
            </div>
          </div>

          {/* Sync Manifest Preview */}
          <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-base font-semibold text-slate-100">
                  Mod yang Tersedia untuk Sinkronisasi Klien
                </h3>
                <p className="text-xs text-slate-400">
                  Daftar mod pada folder server yang akan otomatis diunduh oleh klien saat menekan tombol sync di game.
                </p>
              </div>
              <span className="text-xs px-2.5 py-1 rounded-full bg-slate-800 text-slate-300 border border-slate-700 font-mono">
                {syncManifest?.mods?.length || 0} mod terdistribusi
              </span>
            </div>

            {syncManifest?.mods?.length > 0 ? (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {syncManifest.mods.map((mod: any) => (
                  <div
                    key={mod.uniqueId}
                    className="p-4 bg-slate-950/50 border border-slate-800 rounded-xl space-y-2 hover:border-slate-700 transition"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="space-y-0.5">
                        <h4 className="text-sm font-semibold text-slate-200">{mod.name}</h4>
                        <p className="text-xs font-mono text-slate-400">{mod.uniqueId}</p>
                      </div>
                      <span className="px-2 py-0.5 text-xs font-mono rounded bg-slate-800 text-indigo-400 border border-slate-700">
                        v{mod.version}
                      </span>
                    </div>
                    <div className="flex items-center justify-between text-xs text-slate-500 pt-1">
                      <span>Folder: <code className="text-slate-400 font-mono">{mod.folderName}</code></span>
                      <span>{(mod.sizeBytes / 1024 / 1024).toFixed(2)} MB</span>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="p-6 bg-slate-950/40 rounded-xl border border-dashed border-slate-800 text-center space-y-1">
                <p className="text-xs text-slate-400">
                  Mod server yang kompatibel dengan klien belum terdeteksi di folder /data/Mods.
                </p>
                <p className="text-xs text-slate-500">
                  Setelah mod seperti StardewValleyExpanded dimasukkan ke folder Mods server, daftar akan otomatis muncul di sini.
                </p>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Sub-tab 6: Unggah Mod & Penyimpanan Persisten */}
      {activeSubTab === 'custom' && (
        <div className="space-y-6">
          {/* Persistent Storage Notice Banner */}
          <div className="bg-indigo-950/30 border border-indigo-500/30 rounded-2xl p-6 space-y-3">
            <div className="flex items-start gap-3">
              <div className="p-2.5 bg-indigo-500/10 border border-indigo-500/20 rounded-xl text-indigo-400 shrink-0">
                <HardDrive className="w-5 h-5" />
              </div>
              <div className="space-y-1">
                <div className="flex items-center gap-2">
                  <h3 className="text-base font-semibold text-slate-100">
                    Penyimpanan Mod Persisten (/data/game/custom-mods)
                  </h3>
                  <span className="px-2 py-0.5 text-[11px] font-semibold rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                    Persistent Volume (Aman Rebuild)
                  </span>
                </div>
                <p className="text-xs text-slate-300 leading-relaxed">
                  Mod yang diunggah di sini disimpan langsung di Docker named volume (<code className="text-indigo-300 font-mono">game-data</code>) dan disinkronkan ke folder SMAPI (<code className="text-indigo-300 font-mono">/data/Mods</code>). Data mod Anda <strong className="text-emerald-300 font-semibold">tidak akan terhapus</strong> saat kontainer Coolify di-rebuild, restart, atau saat deploy ulang kode baru.
                </p>
                <div className="flex items-center gap-4 text-xs text-slate-400 pt-1">
                  <span>Akses: <strong className="text-slate-200">Khusus Sysadmin</strong></span>
                  <span>Format: <strong className="text-slate-200">.zip (Mendukung Multi-mod / SVE)</strong></span>
                  <span>Status Volume: <strong className="text-emerald-400">Aktif & Terhubung</strong></span>
                </div>
              </div>
            </div>
          </div>

          {/* Upload Form Card */}
          <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-base font-semibold text-slate-100 flex items-center gap-2">
                  <Upload className="w-4 h-4 text-indigo-400" />
                  Unggah File ZIP Mod
                </h3>
                <p className="text-xs text-slate-400">
                  Unggah mod baru untuk server dan klien tanpa perlu menyetel URL eksternal di environment variable.
                </p>
              </div>
              <span className="text-xs px-2.5 py-1 rounded-full bg-indigo-500/10 text-indigo-400 border border-indigo-500/20 font-medium">
                Sysadmin Only
              </span>
            </div>

            <form onSubmit={handleFileUpload} className="space-y-4">
              {/* Interactive Dropzone */}
              <div
                onClick={() => fileInputRef.current?.click()}
                onDragOver={(e) => {
                  e.preventDefault();
                  setIsDragging(true);
                }}
                onDragLeave={() => setIsDragging(false)}
                onDrop={(e) => {
                  e.preventDefault();
                  setIsDragging(false);
                  if (e.dataTransfer.files && e.dataTransfer.files[0]) {
                    const file = e.dataTransfer.files[0];
                    if (file.name.toLowerCase().endsWith('.zip')) {
                      setSelectedFile(file);
                      setUploadLocalMessage(null);
                    } else {
                      setUploadLocalMessage({ text: 'Hanya file dengan format .zip yang didukung.', type: 'error' });
                    }
                  }
                }}
                className={`p-6 border-2 border-dashed rounded-xl transition flex flex-col items-center justify-center text-center space-y-3 cursor-pointer select-none ${
                  isDragging
                    ? 'bg-indigo-500/15 border-indigo-400 scale-[1.01]'
                    : selectedFile
                    ? 'bg-indigo-950/20 border-indigo-500/40'
                    : 'bg-slate-950/60 border-slate-800 hover:border-indigo-500/50 hover:bg-slate-900/40'
                }`}
              >
                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".zip"
                  disabled={uploading}
                  onChange={(e) => {
                    if (e.target.files && e.target.files[0]) {
                      setSelectedFile(e.target.files[0]);
                      setUploadLocalMessage(null);
                    }
                  }}
                  className="hidden"
                />

                {selectedFile ? (
                  <div className="flex flex-col items-center space-y-2">
                    <div className="p-3 bg-indigo-500/20 rounded-xl text-indigo-400">
                      <FileArchive className="w-8 h-8" />
                    </div>
                    <div className="space-y-1">
                      <p className="text-sm font-semibold text-slate-100">{selectedFile.name}</p>
                      <p className="text-xs text-indigo-300 font-mono">
                        Ukuran: {(selectedFile.size / (1024 * 1024)).toFixed(2)} MB
                      </p>
                    </div>
                    <span className="text-xs text-slate-400 underline hover:text-slate-300">
                      Klik untuk mengganti file lain
                    </span>
                  </div>
                ) : (
                  <>
                    <FileArchive className="w-10 h-10 text-slate-500" />
                    <div className="space-y-1">
                      <p className="text-sm font-medium text-slate-200">
                        Klik di sini atau seret (drag & drop) file .zip mod ke area ini
                      </p>
                      <p className="text-xs text-slate-500">
                        Mendukung bundle ZIP mod individual maupun paket ekspansi besar (SVE, FTM, dsb)
                      </p>
                    </div>
                    <span className="text-xs px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg border border-slate-700 font-medium transition">
                      Pilih File Mod (.zip)
                    </span>
                  </>
                )}
              </div>

              {/* Local Feedback Alert directly in the form */}
              {uploadLocalMessage && (
                <div
                  className={`flex items-center gap-2 p-3.5 rounded-xl text-xs font-medium border ${
                    uploadLocalMessage.type === 'success'
                      ? 'bg-emerald-500/10 border-emerald-500/20 text-emerald-400'
                      : 'bg-rose-500/10 border-rose-500/20 text-rose-400'
                  }`}
                >
                  {uploadLocalMessage.type === 'success' ? (
                    <CheckCircle2 className="w-4 h-4 shrink-0" />
                  ) : (
                    <AlertTriangle className="w-4 h-4 shrink-0" />
                  )}
                  <span>{uploadLocalMessage.text}</span>
                </div>
              )}

              {/* Upload Real-time Progress Bar */}
              {uploadProgressData && (
                <div className="p-4 bg-slate-950/80 border border-indigo-500/30 rounded-xl space-y-3 shadow-inner">
                  <div className="flex items-center justify-between text-xs">
                    <div className="flex items-center gap-2 text-indigo-300 font-medium">
                      <RefreshCw className="w-3.5 h-3.5 animate-spin text-indigo-400 shrink-0" />
                      <span>
                        {uploadProgressData.currentChunk && uploadProgressData.totalChunks
                          ? `Mengunggah Bagian ${uploadProgressData.currentChunk} dari ${uploadProgressData.totalChunks}`
                          : uploadProgressData.stage === 'assembling'
                          ? 'Server Menggabungkan & Memverifikasi Mod'
                          : 'Mengunggah File Mod'}
                      </span>
                    </div>
                    <span className="font-mono font-bold text-sm text-indigo-400">
                      {uploadProgressData.percentage}%
                    </span>
                  </div>

                  {/* Progress Bar Track */}
                  <div className="w-full h-2.5 bg-slate-900 rounded-full overflow-hidden border border-slate-800 p-0.5">
                    <div
                      className="h-full bg-gradient-to-r from-indigo-500 via-indigo-400 to-emerald-400 rounded-full transition-all duration-200 ease-out shadow-sm"
                      style={{ width: `${Math.max(2, uploadProgressData.percentage)}%` }}
                    />
                  </div>

                  <div className="flex items-center justify-between text-[11px] text-slate-400">
                    <span className="font-mono">
                      {(uploadProgressData.loadedBytes / (1024 * 1024)).toFixed(1)} MB / {(uploadProgressData.totalBytes / (1024 * 1024)).toFixed(1)} MB
                    </span>
                    <span className="text-slate-300 font-medium truncate max-w-[260px] sm:max-w-xs text-right">
                      {uploadProgressData.statusText}
                    </span>
                  </div>
                </div>
              )}

              {/* Action Buttons */}
              <div className="flex items-center justify-end gap-3 pt-1">
                {selectedFile && (
                  <button
                    type="button"
                    onClick={() => {
                      setSelectedFile(null);
                      setUploadLocalMessage(null);
                      if (fileInputRef.current) fileInputRef.current.value = '';
                    }}
                    disabled={uploading}
                    className="flex items-center gap-1.5 px-3.5 py-2 text-xs text-slate-400 hover:text-slate-200 transition"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                    Batalkan Pilihan
                  </button>
                )}
                <button
                  type="submit"
                  disabled={uploading}
                  className="flex items-center gap-2 px-5 py-2.5 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold rounded-xl transition disabled:opacity-50 disabled:cursor-not-allowed shadow-sm"
                >
                  <Upload className="w-4 h-4" />
                  {uploading
                    ? 'Mengunggah & Mengekstrak...'
                    : selectedFile
                    ? `Unggah "${selectedFile.name.length > 25 ? selectedFile.name.substring(0, 22) + '...' : selectedFile.name}"`
                    : 'Pilih File Mod (.zip)'}
                </button>
              </div>
            </form>
          </div>

          {/* Custom Uploaded Mods List Table */}
          <div className="bg-white dark:bg-[#111a2e] border border-slate-200 dark:border-[#1e2a42] rounded-2xl p-6 space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-base font-semibold text-slate-100">
                  Daftar Mod Kustom Terpasang di Penyimpanan Persisten
                </h3>
                <p className="text-xs text-slate-400">
                  Mod yang tersimpan di volume persisten dan otomatis didistribusikan ke klien SMAPI.
                </p>
              </div>
              <span className="text-xs px-2.5 py-1 rounded-full bg-slate-800 text-slate-300 border border-slate-700 font-mono">
                {customMods.length} mod persisten
              </span>
            </div>

            {customMods.length > 0 ? (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {customMods.map((mod) => {
                  const isRequired = policy.requiredMods.some((r) => r.uniqueId === mod.uniqueId);
                  return (
                    <div
                      key={mod.folderName}
                      className="p-5 bg-slate-950/60 border border-slate-800 rounded-xl space-y-3 flex flex-col justify-between hover:border-slate-700 transition"
                    >
                      <div className="space-y-1.5">
                        <div className="flex items-start justify-between gap-2">
                          <div>
                            <h4 className="text-sm font-semibold text-slate-200">{mod.name}</h4>
                            <p className="text-xs font-mono text-slate-400">{mod.uniqueId}</p>
                          </div>
                          <span className="px-2 py-0.5 text-xs font-mono rounded bg-slate-800 text-indigo-400 border border-slate-700 shrink-0">
                            v{mod.version}
                          </span>
                        </div>
                        {mod.description && (
                          <p className="text-xs text-slate-400 line-clamp-2">{mod.description}</p>
                        )}
                        <div className="flex items-center justify-between text-[11px] text-slate-500 pt-1">
                          <span>Folder: <code className="text-slate-400 font-mono">{mod.folderName}</code></span>
                          <span>{(mod.sizeBytes / 1024 / 1024).toFixed(2)} MB</span>
                        </div>
                      </div>

                      <div className="flex items-center justify-between gap-2 pt-2 border-t border-slate-800/80">
                        {isRequired ? (
                          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-medium bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                            <CheckCircle2 className="w-3.5 h-3.5" />
                            Mod Wajib Aktif
                          </span>
                        ) : (
                          <button
                            type="button"
                            onClick={() => handleMakeRequired(mod)}
                            className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-indigo-500/10 hover:bg-indigo-500/20 text-indigo-300 border border-indigo-500/30 transition cursor-pointer"
                          >
                            <Plus className="w-3.5 h-3.5" />
                            Jadikan Mod Wajib
                          </button>
                        )}

                        <button
                          type="button"
                          onClick={() => handleDeleteCustomMod(mod.folderName)}
                          className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 border border-rose-500/20 transition cursor-pointer"
                        >
                          <Trash2 className="w-3.5 h-3.5" />
                          Hapus
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            ) : (
              <div className="p-8 bg-slate-950/40 rounded-xl border border-dashed border-slate-800 text-center space-y-2">
                <FileArchive className="w-8 h-8 text-slate-600 mx-auto" />
                <p className="text-xs font-medium text-slate-300">Belum ada mod kustom yang diunggah ke penyimpanan persisten.</p>
                <p className="text-xs text-slate-500 max-w-md mx-auto">
                  Gunakan formulir di atas untuk mengunggah file .zip mod (seperti mod ekspansi atau mod gameplay) agar tersimpan permanen di server.
                </p>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
