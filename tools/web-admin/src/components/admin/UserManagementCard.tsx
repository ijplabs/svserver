import React, { useState, useEffect } from 'react';
import {
  Users,
  UserCheck,
  UserPlus,
  Shield,
  Trash2,
  Clock,
  Key,
  RefreshCw,
  AlertCircle,
  CheckCircle2,
  Lock,
  UserX,
} from 'lucide-react';
import { api } from '../../lib/api';
import { PendingPlayer, WebUser, AvailableFarmer } from '../../types/user';

export const UserManagementCard: React.FC = () => {
  const [pendingPlayers, setPendingPlayers] = useState<PendingPlayer[]>([]);
  const [users, setUsers] = useState<WebUser[]>([]);
  const [availableFarmers, setAvailableFarmers] = useState<AvailableFarmer[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isApproving, setIsApproving] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Form states for provisioning account
  const [showAddModal, setShowAddModal] = useState(false);
  const [selectedFarmerId, setSelectedFarmerId] = useState<number>(0);
  const [newUsername, setNewUsername] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [newRole, setNewRole] = useState<'PLAYER' | 'ADMIN'>('PLAYER');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const fetchData = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const [pending, userList, farmers] = await Promise.all([
        api.admin.getPendingPlayers().catch(() => []),
        api.admin.getUsers().catch(() => []),
        api.admin.getAvailableFarmers().catch(() => []),
      ]);
      setPendingPlayers(pending);
      setUsers(userList);
      setAvailableFarmers(farmers);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Gagal memuat data pengguna.';
      setError(message);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
    const interval = setInterval(() => {
      api.admin.getPendingPlayers()
        .then((data) => setPendingPlayers(data))
        .catch(() => {});
    }, 4000);
    return () => clearInterval(interval);
  }, []);

  const handleApprove = async (playerId: number) => {
    setIsApproving(playerId);
    setError(null);
    setSuccessMsg(null);
    try {
      const res = await api.admin.approvePlayer(playerId);
      setSuccessMsg(res.message || `Pemain ${playerId} berhasil disetujui.`);
      await fetchData();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Gagal menyetujui pemain.';
      setError(message);
    } finally {
      setIsApproving(null);
    }
  };

  const handleCreateAccount = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newUsername.trim() || newPassword.length < 6) {
      setError('Nama pengguna wajib diisi dan kata sandi minimal 6 karakter.');
      return;
    }

    setIsSubmitting(true);
    setError(null);
    setSuccessMsg(null);
    try {
      const res = await api.admin.createUser({
        farmerId: Number(selectedFarmerId),
        username: newUsername.trim(),
        password: newPassword,
        role: newRole,
      });
      setSuccessMsg(res.message);
      setShowAddModal(false);
      setNewUsername('');
      setNewPassword('');
      setSelectedFarmerId(0);
      await fetchData();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Gagal menyimpan akun pengguna.';
      setError(message);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDeleteUser = async (farmerId: number, username: string) => {
    if (!window.confirm(`Hapus akses akun web untuk petani '${username}'?`)) {
      return;
    }

    setError(null);
    setSuccessMsg(null);
    try {
      const res = await api.admin.deleteUser(farmerId);
      setSuccessMsg(res.message);
      await fetchData();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Gagal menghapus pengguna.';
      setError(message);
    }
  };

  const handleSelectFarmerChange = (farmerIdStr: string) => {
    const fId = Number(farmerIdStr);
    setSelectedFarmerId(fId);
    const farmer = availableFarmers.find((f) => f.farmerId === fId);
    if (farmer && farmer.farmerName) {
      setNewUsername(farmer.farmerName);
    }
  };

  return (
    <div className="space-y-6">
      {/* Notifications */}
      {error && (
        <div className="p-4 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900 rounded-xl flex items-center gap-3 text-red-700 dark:text-red-400 text-sm">
          <AlertCircle className="w-5 h-5 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {successMsg && (
        <div className="p-4 bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-900 rounded-xl flex items-center gap-3 text-emerald-700 dark:text-emerald-400 text-sm">
          <CheckCircle2 className="w-5 h-5 shrink-0" />
          <span>{successMsg}</span>
        </div>
      )}

      {/* SECTION 1: PENDING LOBBY APPROVALS (MANUAL ACCESS BYPASS) */}
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-6 shadow-sm">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-amber-500/10 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400 flex items-center justify-center">
              <UserCheck className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-semibold text-slate-900 dark:text-white">
                Persetujuan Akses Lobby Langsung
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                Pemain di lobby yang menunggu kata sandi atau mengalami kendala in-game login
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <span className="px-2.5 py-1 text-xs font-semibold rounded-full bg-amber-100 text-amber-800 dark:bg-amber-950/50 dark:text-amber-300">
              {pendingPlayers.length} Menunggu
            </span>
            <button
              onClick={fetchData}
              disabled={isLoading}
              className="p-2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-300 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
              title="Perbarui daftar"
            >
              <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
            </button>
          </div>
        </div>

        {pendingPlayers.length === 0 ? (
          <div className="py-8 text-center text-slate-400 dark:text-slate-500 text-sm border border-dashed border-slate-200 dark:border-slate-800 rounded-xl">
            Tidak ada pemain yang tertahan di lobby saat ini. Semua pemain telah terautentikasi.
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {pendingPlayers.map((player) => (
              <div
                key={player.playerId}
                className="p-4 bg-slate-50 dark:bg-slate-800/50 border border-slate-200 dark:border-slate-700/60 rounded-xl flex flex-col justify-between"
              >
                <div className="space-y-2 mb-4">
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-slate-900 dark:text-white text-sm">
                      {player.farmerName !== 'Unknown' ? player.farmerName : `Pemain #${player.playerId}`}
                    </span>
                    <span className="px-2 py-0.5 text-xs font-medium bg-amber-500/10 text-amber-600 dark:text-amber-400 rounded-full">
                      Lobby
                    </span>
                  </div>
                  <div className="text-xs text-slate-500 dark:text-slate-400 space-y-1">
                    <div className="flex items-center gap-1.5">
                      <Clock className="w-3.5 h-3.5" />
                      <span>Tertahan: {Math.round(player.secondsInLobby)} detik</span>
                    </div>
                    <div className="flex items-center gap-1.5">
                      <Lock className="w-3.5 h-3.5" />
                      <span>Percobaan Gagal: {player.failedAttempts}</span>
                    </div>
                    <div className="text-[11px] font-mono text-slate-400">
                      ID: {player.playerId}
                    </div>
                  </div>
                </div>

                <button
                  onClick={() => handleApprove(player.playerId)}
                  disabled={isApproving === player.playerId}
                  className="w-full py-2 px-3 bg-emerald-600 hover:bg-emerald-700 active:bg-emerald-800 text-white rounded-lg text-xs font-medium transition-colors flex items-center justify-center gap-2 shadow-sm disabled:opacity-50"
                >
                  <UserCheck className="w-4 h-4" />
                  {isApproving === player.playerId ? 'Menyetujui...' : 'Izinkan Masuk (Bypass)'}
                </button>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* SECTION 2: WEB ACCOUNT MANAGEMENT & WHITELIST */}
      <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl p-6 shadow-sm">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-indigo-500/10 dark:bg-indigo-500/20 text-indigo-600 dark:text-indigo-400 flex items-center justify-center">
              <Users className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-semibold text-slate-900 dark:text-white">
                Manajemen Akun Web & Whitelist
              </h2>
              <p className="text-xs text-slate-500 dark:text-slate-400">
                Pemberian akses manual untuk petani tanpa mengharuskan perintah !webpass
              </p>
            </div>
          </div>
          <button
            onClick={() => setShowAddModal(true)}
            className="inline-flex items-center gap-2 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 active:bg-indigo-800 text-white rounded-xl text-xs font-medium transition-colors shadow-sm"
          >
            <UserPlus className="w-4 h-4" />
            <span>Tambah / Tautkan Akun</span>
          </button>
        </div>

        {/* Users Table */}
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs text-slate-600 dark:text-slate-300">
            <thead className="bg-slate-50 dark:bg-slate-800/60 text-slate-500 dark:text-slate-400 uppercase tracking-wider text-[11px] font-semibold border-b border-slate-200 dark:border-slate-800">
              <tr>
                <th className="py-3 px-4">Nama Petani</th>
                <th className="py-3 px-4">Farmer ID</th>
                <th className="py-3 px-4">Peran Web</th>
                <th className="py-3 px-4">Dibuat Pada</th>
                <th className="py-3 px-4">Login Terakhir</th>
                <th className="py-3 px-4 text-right">Tindakan</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800/60">
              {users.length === 0 ? (
                <tr>
                  <td colSpan={6} className="py-8 text-center text-slate-400 dark:text-slate-500">
                    Belum ada akun web terdaftar. Gunakan tombol &apos;Tambah / Tautkan Akun&apos; di atas.
                  </td>
                </tr>
              ) : (
                users.map((user) => (
                  <tr key={user.farmerId} className="hover:bg-slate-50/50 dark:hover:bg-slate-800/30">
                    <td className="py-3.5 px-4 font-semibold text-slate-900 dark:text-white">
                      {user.username}
                    </td>
                    <td className="py-3.5 px-4 font-mono text-slate-400">
                      {user.farmerId}
                    </td>
                    <td className="py-3.5 px-4">
                      <span
                        className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full font-medium ${
                          user.role === 'SUPERADMIN'
                            ? 'bg-purple-100 text-purple-800 dark:bg-purple-950/60 dark:text-purple-300'
                            : user.role === 'ADMIN'
                            ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/60 dark:text-emerald-300'
                            : 'bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300'
                        }`}
                      >
                        <Shield className="w-3 h-3" />
                        {user.role}
                      </span>
                    </td>
                    <td className="py-3.5 px-4 text-slate-500">
                      {new Date(user.createdAtUtc).toLocaleDateString('id-ID', {
                        day: 'numeric',
                        month: 'short',
                        year: 'numeric',
                      })}
                    </td>
                    <td className="py-3.5 px-4 text-slate-500">
                      {user.lastLoginUtc
                        ? new Date(user.lastLoginUtc).toLocaleDateString('id-ID', {
                            day: 'numeric',
                            month: 'short',
                            hour: '2-digit',
                            minute: '2-digit',
                          })
                        : 'Belum pernah'}
                    </td>
                    <td className="py-3.5 px-4 text-right">
                      {user.role !== 'SUPERADMIN' && (
                        <button
                          onClick={() => handleDeleteUser(user.farmerId, user.username)}
                          className="p-1.5 text-slate-400 hover:text-red-600 dark:hover:text-red-400 rounded-lg hover:bg-red-50 dark:hover:bg-red-950/30 transition-colors"
                          title="Hapus akses akun"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* PROVISION USER MODAL */}
      {showAddModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm">
          <div className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-2xl max-w-md w-full p-6 shadow-xl space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100 dark:border-slate-800">
              <div className="flex items-center gap-2">
                <UserPlus className="w-5 h-5 text-indigo-600 dark:text-indigo-400" />
                <h3 className="font-semibold text-slate-900 dark:text-white text-sm">
                  Tautkan Akun Web Petani
                </h3>
              </div>
              <button
                onClick={() => setShowAddModal(false)}
                className="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300"
              >
                <UserX className="w-5 h-5" />
              </button>
            </div>

            <form onSubmit={handleCreateAccount} className="space-y-4 text-xs">
              <div>
                <label className="block font-medium text-slate-700 dark:text-slate-300 mb-1">
                  Pilih Petani dari Save Game
                </label>
                <select
                  value={selectedFarmerId}
                  onChange={(e) => handleSelectFarmerChange(e.target.value)}
                  className="w-full px-3 py-2 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                >
                  <option value={0}>-- Masukkan ID Kustom atau Pilih --</option>
                  {availableFarmers.map((f) => (
                    <option key={f.farmerId} value={f.farmerId}>
                      {f.farmerName} (ID: {f.farmerId}) {f.hasWebAccount ? '[Sudah ada akun]' : ''}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block font-medium text-slate-700 dark:text-slate-300 mb-1">
                  Farmer ID (ID Pemain)
                </label>
                <input
                  type="number"
                  value={selectedFarmerId || ''}
                  onChange={(e) => setSelectedFarmerId(Number(e.target.value))}
                  placeholder="e.g. -741289410294"
                  required
                  className="w-full px-3 py-2 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-slate-900 dark:text-white font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
              </div>

              <div>
                <label className="block font-medium text-slate-700 dark:text-slate-300 mb-1">
                  Username Akun Web
                </label>
                <input
                  type="text"
                  value={newUsername}
                  onChange={(e) => setNewUsername(e.target.value)}
                  placeholder="Nama login petani"
                  required
                  className="w-full px-3 py-2 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
              </div>

              <div>
                <label className="block font-medium text-slate-700 dark:text-slate-300 mb-1">
                  Kata Sandi (Minimal 6 Karakter)
                </label>
                <div className="relative">
                  <input
                    type="password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    placeholder="Masukkan kata sandi baru"
                    required
                    minLength={6}
                    className="w-full pl-8 pr-3 py-2 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                  />
                  <Key className="w-4 h-4 text-slate-400 absolute left-2.5 top-2.5" />
                </div>
              </div>

              <div>
                <label className="block font-medium text-slate-700 dark:text-slate-300 mb-1">
                  Hak Akses Web
                </label>
                <select
                  value={newRole}
                  onChange={(e) => setNewRole(e.target.value as 'PLAYER' | 'ADMIN')}
                  className="w-full px-3 py-2 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-xl text-slate-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                >
                  <option value="PLAYER">Petani Biasa (Farmer View Pribadi)</option>
                  <option value="ADMIN">Administrator Server (Akses Penuh)</option>
                </select>
              </div>

              <div className="flex items-center justify-end gap-2 pt-3 border-t border-slate-100 dark:border-slate-800">
                <button
                  type="button"
                  onClick={() => setShowAddModal(false)}
                  className="px-4 py-2 border border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800 rounded-xl font-medium transition-colors"
                >
                  Batal
                </button>
                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 active:bg-indigo-800 text-white rounded-xl font-medium transition-colors disabled:opacity-50"
                >
                  {isSubmitting ? 'Menyimpan...' : 'Simpan Akun'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
