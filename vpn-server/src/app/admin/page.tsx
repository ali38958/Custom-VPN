"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  Users,
  Network as NetworkIcon,
  ShieldCheck,
  Plus,
  Trash2,
  KeyRound,
  LogOut,
  RefreshCw,
  Loader2,
  Laptop,
  CheckCircle2,
  XCircle,
  AlertCircle
} from "lucide-react";

interface UserItem {
  id: number;
  username: string;
  assignedIp: string | null;
  disabled: boolean;
  network?: { id: number; name: string } | null;
  session?: {
    deviceId: string;
    deviceName: string | null;
    isOnline: boolean;
    lastSeenAt: string;
  } | null;
}

interface NetworkItem {
  id: number;
  name: string;
  subnet: string;
  description: string | null;
  users: Array<{ id: number; username: string }>;
}

export default function AdminDashboardPage() {
  const router = useRouter();
  const [users, setUsers] = useState<UserItem[]>([]);
  const [networks, setNetworks] = useState<NetworkItem[]>([]);
  const [loading, setLoading] = useState(true);

  // Modal states
  const [showAddUser, setShowAddUser] = useState(false);
  const [newUsername, setNewUsername] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [newAssignedIp, setNewAssignedIp] = useState("10.77.0.2");
  const [newNetworkId, setNewNetworkId] = useState("");

  const [showAddNetwork, setShowAddNetwork] = useState(false);
  const [netName, setNetName] = useState("");
  const [netSubnet, setNetSubnet] = useState("10.77.0.0/24");
  const [netDesc, setNetDesc] = useState("");

  const [feedback, setFeedback] = useState<{ type: "success" | "error"; msg: string } | null>(null);

  const loadData = async () => {
    try {
      const [authRes, userRes, netRes] = await Promise.all([
        fetch("/api/auth/me"),
        fetch("/api/admin/users"),
        fetch("/api/admin/networks"),
      ]);

      const authData = await authRes.json();
      if (!authData.authenticated) {
        router.push("/login");
        return;
      }

      if (userRes.ok) {
        const uData = await userRes.json();
        setUsers(uData.users || []);
      }
      if (netRes.ok) {
        const nData = await netRes.json();
        setNetworks(nData.networks || []);
        if (nData.networks?.length > 0 && !newNetworkId) {
          setNewNetworkId(nData.networks[0].id.toString());
        }
      }
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleCreateUser = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const res = await fetch("/api/admin/users", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          username: newUsername,
          password: newPassword,
          assignedIp: newAssignedIp,
          networkId: newNetworkId,
        }),
      });

      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Failed to create user");

      setFeedback({ type: "success", msg: `User ${newUsername} created successfully!` });
      setShowAddUser(false);
      setNewUsername("");
      setNewPassword("");
      loadData();
    } catch (err: unknown) {
      setFeedback({ type: "error", msg: err instanceof Error ? err.message : "Error" });
    }
  };

  const handleCreateNetwork = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const res = await fetch("/api/admin/networks", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: netName,
          subnet: netSubnet,
          description: netDesc,
        }),
      });

      const data = await res.json();
      if (!res.ok) throw new Error(data.error || "Failed to create network");

      setFeedback({ type: "success", msg: `Network ${netName} established!` });
      setShowAddNetwork(false);
      setNetName("");
      loadData();
    } catch (err: unknown) {
      setFeedback({ type: "error", msg: err instanceof Error ? err.message : "Error" });
    }
  };

  const toggleUserStatus = async (user: UserItem) => {
    try {
      const res = await fetch(`/api/admin/users/${user.id}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ disabled: !user.disabled }),
      });
      if (res.ok) {
        setFeedback({
          type: "success",
          msg: `User ${user.username} is now ${!user.disabled ? "disabled" : "enabled"}.`,
        });
        loadData();
      }
    } catch (err) {
      console.error(err);
    }
  };

  const clearUserSession = async (user: UserItem) => {
    if (!confirm(`Force release device session for ${user.username}? This allows other devices to log in.`)) return;
    try {
      const res = await fetch(`/api/admin/users/${user.id}`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ clearSession: true }),
      });
      if (res.ok) {
        setFeedback({ type: "success", msg: `Device session freed for ${user.username}.` });
        loadData();
      }
    } catch (err) {
      console.error(err);
    }
  };

  const deleteUser = async (user: UserItem) => {
    if (!confirm(`Permanently delete user ${user.username}?`)) return;
    try {
      const res = await fetch(`/api/admin/users/${user.id}`, { method: "DELETE" });
      if (res.ok) {
        setFeedback({ type: "success", msg: `User ${user.username} removed.` });
        loadData();
      }
    } catch (err) {
      console.error(err);
    }
  };

  const handleLogout = async () => {
    await fetch("/api/auth/logout", { method: "POST" });
    router.push("/login");
  };

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-zinc-950 text-zinc-400">
        <Loader2 className="w-8 h-8 animate-spin text-cyan-500" />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-zinc-950 text-zinc-100 p-6 md:p-10 font-sans">
      <div className="max-w-7xl mx-auto space-y-8">
        {/* Header */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-6 border-b border-zinc-800/80">
          <div>
            <div className="flex items-center gap-3">
              <span className="p-2 rounded-xl bg-cyan-500/10 border border-cyan-500/20 text-cyan-400">
                <ShieldCheck className="w-6 h-6" />
              </span>
              <div>
                <h1 className="text-2xl font-black tracking-tight flex items-center gap-2">
                  Admin Command Core
                  <span className="text-xs px-2.5 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-mono">
                    BOwnerNo1
                  </span>
                </h1>
                <p className="text-xs text-zinc-400 mt-0.5 font-mono">
                  Full Root Authority &bull; SQLite3 Lightweight Engine
                </p>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={() => setShowAddNetwork(true)}
              className="px-4 py-2 rounded-xl bg-zinc-900 border border-zinc-800 hover:bg-zinc-850 text-zinc-300 text-xs font-semibold flex items-center gap-2 transition-all"
            >
              <NetworkIcon className="w-4 h-4 text-cyan-400" />
              Create Network
            </button>
            <button
              onClick={() => setShowAddUser(true)}
              className="px-4 py-2.5 rounded-xl bg-gradient-to-r from-cyan-500 to-emerald-500 text-zinc-950 font-bold text-xs flex items-center gap-2 shadow-lg shadow-cyan-500/20 hover:opacity-95 transition-all"
            >
              <Plus className="w-4 h-4" />
              Add User Account
            </button>
            <button
              onClick={handleLogout}
              className="p-2.5 rounded-xl bg-zinc-900 border border-zinc-800 text-zinc-400 hover:text-red-400 transition-colors"
              title="Sign Out"
            >
              <LogOut className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Feedback Alert */}
        {feedback && (
          <div
            className={`p-4 rounded-2xl flex items-center gap-3 text-xs ${
              feedback.type === "success"
                ? "bg-emerald-950/40 border border-emerald-800/40 text-emerald-300"
                : "bg-red-950/40 border border-red-800/40 text-red-300"
            }`}
          >
            {feedback.type === "success" ? (
              <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
            ) : (
              <AlertCircle className="w-4 h-4 text-red-400 shrink-0" />
            )}
            <span>{feedback.msg}</span>
            <button
              onClick={() => setFeedback(null)}
              className="ml-auto text-zinc-500 hover:text-zinc-300"
            >
              &times;
            </button>
          </div>
        )}

        {/* Network Nodes Grid */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {networks.map((net) => (
            <div
              key={net.id}
              className="p-5 rounded-2xl bg-zinc-900/60 border border-zinc-800/80 backdrop-blur-md"
            >
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs font-mono px-2 py-0.5 rounded bg-cyan-950/60 border border-cyan-800/50 text-cyan-400">
                  {net.subnet}
                </span>
                <span className="text-[11px] text-zinc-500 font-mono">
                  {net.users.length} Clients
                </span>
              </div>
              <h3 className="font-bold text-zinc-200 text-base">{net.name}</h3>
              <p className="text-xs text-zinc-400 mt-1 line-clamp-2">
                {net.description || "Active WireGuard relay network segment"}
              </p>
            </div>
          ))}
        </div>

        {/* User Management Table */}
        <div className="bg-zinc-900/40 border border-zinc-800 rounded-3xl overflow-hidden shadow-2xl">
          <div className="p-6 border-b border-zinc-800/80 flex items-center justify-between">
            <div>
              <h2 className="text-lg font-bold text-zinc-200 flex items-center gap-2">
                <Users className="w-5 h-5 text-cyan-400" />
                Authorized Clients & Device Locks
              </h2>
              <p className="text-xs text-zinc-500 mt-0.5">
                Strict 1:1 single-device concurrent lock enforced per user
              </p>
            </div>
            <button
              onClick={loadData}
              className="p-2 rounded-xl bg-zinc-850 hover:bg-zinc-800 text-zinc-400 transition-colors"
            >
              <RefreshCw className="w-4 h-4" />
            </button>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="border-b border-zinc-800/80 text-zinc-400 font-mono uppercase tracking-wider text-[11px] bg-zinc-950/40">
                  <th className="py-4 px-6">User / Identity</th>
                  <th className="py-4 px-6">Assigned Virtual IP</th>
                  <th className="py-4 px-6">Subnet</th>
                  <th className="py-4 px-6">Device Lock / Status</th>
                  <th className="py-4 px-6">Account Status</th>
                  <th className="py-4 px-6 text-right">Admin Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-800/60 font-mono">
                {users.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="py-8 text-center text-zinc-500">
                      No user accounts registered. Add one using the top button.
                    </td>
                  </tr>
                ) : (
                  users.map((u) => (
                    <tr key={u.id} className="hover:bg-zinc-850/30 transition-colors">
                      <td className="py-4 px-6 font-semibold text-zinc-200">
                        {u.username}
                      </td>
                      <td className="py-4 px-6 text-cyan-400 font-bold">
                        {u.assignedIp || "Unassigned"}
                      </td>
                      <td className="py-4 px-6 text-zinc-400">
                        {u.network?.name || "None"}
                      </td>
                      <td className="py-4 px-6">
                        {u.session ? (
                          <div className="flex items-center gap-2">
                            <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
                            <span className="text-zinc-300 flex items-center gap-1">
                              <Laptop className="w-3.5 h-3.5 text-zinc-500" />
                              {u.session.deviceName || u.session.deviceId.substring(0, 8)}
                            </span>
                            <button
                              onClick={() => clearUserSession(u)}
                              className="ml-2 text-[10px] text-amber-400 hover:text-amber-300 underline font-sans"
                            >
                              Release Lock
                            </button>
                          </div>
                        ) : (
                          <span className="text-zinc-600">No Device Bound</span>
                        )}
                      </td>
                      <td className="py-4 px-6">
                        {u.disabled ? (
                          <span className="px-2 py-0.5 rounded-full bg-red-950/60 border border-red-800/60 text-red-400 text-[10px]">
                            Disabled
                          </span>
                        ) : (
                          <span className="px-2 py-0.5 rounded-full bg-emerald-950/60 border border-emerald-800/60 text-emerald-400 text-[10px]">
                            Active
                          </span>
                        )}
                      </td>
                      <td className="py-4 px-6 text-right space-x-2 font-sans">
                        <button
                          onClick={() => toggleUserStatus(u)}
                          className="px-2.5 py-1 rounded-lg bg-zinc-850 hover:bg-zinc-800 text-zinc-300 text-[11px] font-medium"
                        >
                          {u.disabled ? "Enable" : "Disable"}
                        </button>
                        <button
                          onClick={() => deleteUser(u)}
                          className="px-2.5 py-1 rounded-lg bg-red-950/30 hover:bg-red-950/60 border border-red-800/40 text-red-400 text-[11px] font-medium"
                        >
                          Delete
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>

        {/* Modal: Add User */}
        {showAddUser && (
          <div className="fixed inset-0 bg-black/80 backdrop-blur-sm flex items-center justify-center p-4 z-50">
            <div className="w-full max-w-md bg-zinc-900 border border-zinc-800 rounded-3xl p-6 sm:p-8 shadow-2xl">
              <h2 className="text-lg font-bold text-zinc-100 mb-4">Add VPN Client User</h2>
              <form onSubmit={handleCreateUser} className="space-y-4">
                <div>
                  <label className="block text-xs font-semibold text-zinc-400 mb-1">Username</label>
                  <input
                    type="text"
                    required
                    value={newUsername}
                    onChange={(e) => setNewUsername(e.target.value)}
                    placeholder="alice"
                    className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-xs text-zinc-100"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-zinc-400 mb-1">Initial Password</label>
                  <input
                    type="password"
                    required
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    placeholder="••••••••"
                    className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-xs text-zinc-100"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-zinc-400 mb-1">Assigned Virtual IP</label>
                  <input
                    type="text"
                    required
                    value={newAssignedIp}
                    onChange={(e) => setNewAssignedIp(e.target.value)}
                    placeholder="10.77.0.2"
                    className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-xs text-zinc-100 font-mono"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-zinc-400 mb-1">Assign Network</label>
                  <select
                    value={newNetworkId}
                    onChange={(e) => setNewNetworkId(e.target.value)}
                    className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-xs text-zinc-100"
                  >
                    {networks.map((n) => (
                      <option key={n.id} value={n.id}>
                        {n.name} ({n.subnet})
                      </option>
                    ))}
                  </select>
                </div>
                <div className="flex gap-2 pt-2">
                  <button
                    type="button"
                    onClick={() => setShowAddUser(false)}
                    className="flex-1 py-2.5 rounded-xl bg-zinc-800 text-zinc-300 text-xs font-semibold"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="flex-1 py-2.5 rounded-xl bg-cyan-500 text-zinc-950 font-bold text-xs"
                  >
                    Create User
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Modal: Add Network */}
        {showAddNetwork && (
          <div className="fixed inset-0 bg-black/80 backdrop-blur-sm flex items-center justify-center p-4 z-50">
            <div className="w-full max-w-md bg-zinc-900 border border-zinc-800 rounded-3xl p-6 sm:p-8 shadow-2xl">
              <h2 className="text-lg font-bold text-zinc-100 mb-4">Create Network Segment</h2>
              <form onSubmit={handleCreateNetwork} className="space-y-4">
                <div>
                  <label className="block text-xs font-semibold text-zinc-400 mb-1">Network Name</label>
                  <input
                    type="text"
                    required
                    value={netName}
                    onChange={(e) => setNetName(e.target.value)}
                    placeholder="Finance VPN Subnet"
                    className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-xs text-zinc-100"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-zinc-400 mb-1">Subnet Range</label>
                  <input
                    type="text"
                    required
                    value={netSubnet}
                    onChange={(e) => setNetSubnet(e.target.value)}
                    placeholder="10.77.0.0/24"
                    className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-xs text-zinc-100 font-mono"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-zinc-400 mb-1">Description</label>
                  <input
                    type="text"
                    value={netDesc}
                    onChange={(e) => setNetDesc(e.target.value)}
                    placeholder="Private network segment"
                    className="w-full bg-zinc-950 border border-zinc-800 rounded-xl px-4 py-2.5 text-xs text-zinc-100"
                  />
                </div>
                <div className="flex gap-2 pt-2">
                  <button
                    type="button"
                    onClick={() => setShowAddNetwork(false)}
                    className="flex-1 py-2.5 rounded-xl bg-zinc-800 text-zinc-300 text-xs font-semibold"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="flex-1 py-2.5 rounded-xl bg-cyan-500 text-zinc-950 font-bold text-xs"
                  >
                    Create Network
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
