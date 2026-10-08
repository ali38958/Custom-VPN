"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  Globe,
  Plus,
  RefreshCw,
  Loader2,
  Laptop,
  CheckCircle2,
  AlertCircle,
  X,
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

      setFeedback({ type: "success", msg: `User ${newUsername} created successfully.` });
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

      setFeedback({ type: "success", msg: `Network ${netName} established successfully.` });
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

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center min-h-[60vh] text-[var(--text-secondary)]">
        <Loader2 className="w-7 h-7 animate-spin text-[var(--accent)]" />
      </div>
    );
  }

  const activeClientsCount = users.filter((u) => !u.disabled).length;
  const boundSessionsCount = users.filter((u) => u.session != null).length;
  const primarySubnet = networks.length > 0 ? networks[0].subnet : "10.77.0.0/24";

  return (
    <div className="max-w-[1280px] mx-auto px-5 sm:px-10 py-8 pb-16 w-full">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-6 mb-7">
        <div>
          <h1 className="text-[22px] font-semibold tracking-tight text-[var(--text-primary)]">
            Admin Overview
          </h1>
          <p className="text-[13.5px] text-[var(--text-secondary)] mt-1">
            Manage VPN networks, authorized clients, and device locks.
          </p>
        </div>
        <div className="flex items-center gap-2.5">
          <button
            onClick={() => setShowAddNetwork(true)}
            className="pn-btn pn-btn-secondary"
          >
            <Globe className="w-3.5 h-3.5" />
            <span>Create Network</span>
          </button>
          <button
            onClick={() => setShowAddUser(true)}
            className="pn-btn pn-btn-primary"
          >
            <Plus className="w-3.5 h-3.5" />
            <span>Add User</span>
          </button>
        </div>
      </div>

      {/* Feedback Alert */}
      {feedback && (
        <div
          className={`flex items-start gap-2.5 p-3.5 rounded-lg text-[13.5px] mb-6 border ${
            feedback.type === "success"
              ? "bg-[var(--success-soft)] border-[var(--success-border)] text-[var(--success-text)]"
              : "bg-[var(--danger-soft)] border-[var(--danger-border)] text-[var(--danger-text)]"
          }`}
        >
          {feedback.type === "success" ? (
            <CheckCircle2 className="w-4 h-4 shrink-0 mt-0.5" />
          ) : (
            <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
          )}
          <span className="flex-1">{feedback.msg}</span>
          <button
            onClick={() => setFeedback(null)}
            className="opacity-70 hover:opacity-100 p-0.5 cursor-pointer ml-auto"
            aria-label="Dismiss"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Stat Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 mb-7">
        <div className="pn-card p-5">
          <div className="text-xs text-[var(--text-muted)] uppercase tracking-wider font-semibold mb-2">
            Default Network
          </div>
          <div className="font-mono text-xl font-medium text-[var(--text-primary)] tracking-tight">
            {primarySubnet}
          </div>
          <div className="text-[12.5px] text-[var(--text-secondary)] mt-1.5">
            Primary VPN network subnet
          </div>
        </div>

        <div className="pn-card p-5">
          <div className="text-xs text-[var(--text-muted)] uppercase tracking-wider font-semibold mb-2">
            Active Clients
          </div>
          <div className="font-mono text-xl font-medium text-[var(--text-primary)] tracking-tight">
            {activeClientsCount}
          </div>
          <div className="text-[12.5px] text-[var(--text-secondary)] mt-1.5">
            1:1 device lock enforced per user
          </div>
        </div>

        <div className="pn-card p-5">
          <div className="text-xs text-[var(--text-muted)] uppercase tracking-wider font-semibold mb-2">
            Device Locks
          </div>
          <div className="font-mono text-xl font-medium text-[var(--text-primary)] tracking-tight">
            {boundSessionsCount}
          </div>
          <div className="text-[12.5px] text-[var(--text-secondary)] mt-1.5">
            {boundSessionsCount > 0 ? "Hardware sessions actively bound" : "No bound devices currently"}
          </div>
        </div>
      </div>

      {/* Clients & Locks Section */}
      <section className="pn-card overflow-hidden">
        <div className="p-5 border-b border-[var(--border)] flex justify-between items-center gap-4">
          <div>
            <div className="text-[15px] font-semibold text-[var(--text-primary)] tracking-tight">
              Authorized Clients &amp; Device Locks
            </div>
            <div className="text-[12.5px] text-[var(--text-secondary)] mt-0.5">
              Strict 1:1 single-device concurrent lock enforced per user.
            </div>
          </div>
          <button
            onClick={loadData}
            className="p-1.5 rounded-md border border-[var(--border)] text-[var(--text-secondary)] hover:bg-[var(--sidebar-active)] hover:text-[var(--text-primary)] transition-colors cursor-pointer inline-flex items-center justify-center"
            title="Refresh"
            aria-label="Refresh"
          >
            <RefreshCw className="w-3.5 h-3.5" />
          </button>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-left text-[13.5px]">
            <thead>
              <tr className="bg-[var(--table-head-bg)] border-b border-[var(--border)] text-[11px] uppercase tracking-wider text-[var(--text-muted)] font-semibold font-mono">
                <th className="py-3 px-5">User / Identity</th>
                <th className="py-3 px-5">Virtual IP</th>
                <th className="py-3 px-5">Subnet</th>
                <th className="py-3 px-5">Device Lock</th>
                <th className="py-3 px-5">Status</th>
                <th className="py-3 px-5 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--border)]">
              {users.length === 0 ? (
                <tr>
                  <td colSpan={6} className="py-8 text-center text-[var(--text-muted)]">
                    No client accounts registered. Add one using the top button.
                  </td>
                </tr>
              ) : (
                users.map((u, idx) => (
                  <tr key={u.id} className="hover:bg-[var(--table-hover)] transition-colors">
                    {/* User Identity */}
                    <td className="py-3.5 px-5">
                      <div className="flex items-center gap-2.5">
                        <div className="w-7 h-7 rounded-full bg-[var(--user-dot-bg)] text-[var(--user-dot-text)] flex items-center justify-center text-[11px] font-semibold font-mono">
                          {String(idx + 1).padStart(2, "0")}
                        </div>
                        <span className="font-medium text-[var(--text-primary)]">{u.username}</span>
                      </div>
                    </td>

                    {/* Virtual IP */}
                    <td className="py-3.5 px-5 font-mono text-[13px] text-[var(--text-primary)]">
                      {u.assignedIp || "Unassigned"}
                    </td>

                    {/* Subnet */}
                    <td className="py-3.5 px-5 text-[var(--text-secondary)]">
                      {u.network?.name || "Default Network"}
                    </td>

                    {/* Device Lock */}
                    <td className="py-3.5 px-5">
                      {u.session ? (
                        <div className="inline-flex items-center gap-1.5 flex-wrap">
                          <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium bg-[var(--success-soft)] text-[var(--success-text)] border border-[var(--success-border)]">
                            <span className="w-1.5 h-1.5 rounded-full bg-[var(--success)] animate-pulse"></span>
                            <Laptop className="w-3 h-3 opacity-70" />
                            {u.session.deviceName || u.session.deviceId.substring(0, 8)}
                          </span>
                          <button
                            onClick={() => clearUserSession(u)}
                            className="text-[11px] text-amber-500 hover:underline cursor-pointer ml-1 font-medium"
                          >
                            Release
                          </button>
                        </div>
                      ) : (
                        <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full text-xs font-medium bg-[var(--badge-neutral-bg)] text-[var(--badge-neutral-text)]">
                          <span className="w-1.5 h-1.5 rounded-full bg-[var(--text-muted)]"></span>
                          None
                        </span>
                      )}
                    </td>

                    {/* Account Status */}
                    <td className="py-3.5 px-5">
                      {u.disabled ? (
                        <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium bg-[var(--danger-soft)] text-[var(--danger-text)] border border-[var(--danger-border)]">
                          <span className="w-1.5 h-1.5 rounded-full bg-[var(--danger)]"></span>
                          Disabled
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-medium bg-[var(--success-soft)] text-[var(--success-text)] border border-[var(--success-border)]">
                          <span className="w-1.5 h-1.5 rounded-full bg-[var(--success)]"></span>
                          Active
                        </span>
                      )}
                    </td>

                    {/* Actions */}
                    <td className="py-3.5 px-5 text-right">
                      <div className="inline-flex items-center gap-1 justify-end">
                        <button
                          onClick={() => toggleUserStatus(u)}
                          className="pn-btn pn-btn-ghost text-xs"
                        >
                          {u.disabled ? "Enable" : "Disable"}
                        </button>
                        <button
                          onClick={() => deleteUser(u)}
                          className="pn-btn pn-btn-danger-text text-xs"
                        >
                          Delete
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </section>

      {/* Modal: Add User */}
      {showAddUser && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="w-full max-w-md pn-card p-6 sm:p-7 shadow-2xl">
            <h2 className="text-base font-semibold text-[var(--text-primary)] mb-4">
              Add VPN Client User
            </h2>
            <form onSubmit={handleCreateUser} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">
                  Username
                </label>
                <input
                  type="text"
                  required
                  value={newUsername}
                  onChange={(e) => setNewUsername(e.target.value)}
                  placeholder="e.g. vpn_user"
                  className="w-full text-sm text-[var(--text-primary)] bg-[var(--input-bg)] border border-[var(--border-strong)] rounded-lg px-3 py-2 outline-none focus:border-[var(--accent)] font-mono"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">
                  Initial Password
                </label>
                <input
                  type="password"
                  required
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  placeholder="••••••••••••"
                  className="w-full text-sm text-[var(--text-primary)] bg-[var(--input-bg)] border border-[var(--border-strong)] rounded-lg px-3 py-2 outline-none focus:border-[var(--accent)] font-mono"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">
                  Assigned Virtual IP
                </label>
                <input
                  type="text"
                  required
                  value={newAssignedIp}
                  onChange={(e) => setNewAssignedIp(e.target.value)}
                  placeholder="10.77.0.2"
                  className="w-full text-sm text-[var(--text-primary)] bg-[var(--input-bg)] border border-[var(--border-strong)] rounded-lg px-3 py-2 outline-none focus:border-[var(--accent)] font-mono"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">
                  Assign Network
                </label>
                <select
                  value={newNetworkId}
                  onChange={(e) => setNewNetworkId(e.target.value)}
                  className="w-full text-sm text-[var(--text-primary)] bg-[var(--input-bg)] border border-[var(--border-strong)] rounded-lg px-3 py-2 outline-none focus:border-[var(--accent)]"
                >
                  {networks.map((n) => (
                    <option key={n.id} value={n.id}>
                      {n.name} ({n.subnet})
                    </option>
                  ))}
                </select>
              </div>

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setShowAddUser(false)}
                  className="flex-1 pn-btn pn-btn-secondary justify-center"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="flex-1 pn-btn pn-btn-primary justify-center"
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
        <div className="fixed inset-0 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4 z-50">
          <div className="w-full max-w-md pn-card p-6 sm:p-7 shadow-2xl">
            <h2 className="text-base font-semibold text-[var(--text-primary)] mb-4">
              Create Network Segment
            </h2>
            <form onSubmit={handleCreateNetwork} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">
                  Network Name
                </label>
                <input
                  type="text"
                  required
                  value={netName}
                  onChange={(e) => setNetName(e.target.value)}
                  placeholder="e.g. Secondary VPN"
                  className="w-full text-sm text-[var(--text-primary)] bg-[var(--input-bg)] border border-[var(--border-strong)] rounded-lg px-3 py-2 outline-none focus:border-[var(--accent)]"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">
                  Subnet Range
                </label>
                <input
                  type="text"
                  required
                  value={netSubnet}
                  onChange={(e) => setNetSubnet(e.target.value)}
                  placeholder="10.77.0.0/24"
                  className="w-full text-sm text-[var(--text-primary)] bg-[var(--input-bg)] border border-[var(--border-strong)] rounded-lg px-3 py-2 outline-none focus:border-[var(--accent)] font-mono"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-[var(--text-secondary)] mb-1">
                  Description
                </label>
                <input
                  type="text"
                  value={netDesc}
                  onChange={(e) => setNetDesc(e.target.value)}
                  placeholder="Network segment description"
                  className="w-full text-sm text-[var(--text-primary)] bg-[var(--input-bg)] border border-[var(--border-strong)] rounded-lg px-3 py-2 outline-none focus:border-[var(--accent)]"
                />
              </div>

              <div className="flex gap-2.5 pt-2">
                <button
                  type="button"
                  onClick={() => setShowAddNetwork(false)}
                  className="flex-1 pn-btn pn-btn-secondary justify-center"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="flex-1 pn-btn pn-btn-primary justify-center"
                >
                  Create Network
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
