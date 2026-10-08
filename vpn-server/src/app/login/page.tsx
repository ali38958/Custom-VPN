"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { Lock, User, ArrowRight, AlertCircle, Loader2, Eye, EyeOff } from "lucide-react";

export default function LoginPage() {
  const router = useRouter();
  const [username, setUsername] = useState("Knight");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);

    try {
      const res = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username, password }),
      });

      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.error || "Login failed");
      }

      window.location.href = "/admin";
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Invalid credentials");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex-1 flex items-center justify-center p-6 sm:p-12">
      <div className="w-full max-w-[400px]">
        {/* Header */}
        <div className="text-center mb-7">
          <div className="w-11 h-11 rounded-xl bg-[var(--accent-soft)] text-[var(--accent)] flex items-center justify-center mx-auto mb-4 border border-[var(--accent)]/20 shadow-sm">
            <Lock className="w-5 h-5" />
          </div>
          <h1 className="text-[22px] font-semibold tracking-tight text-[var(--text-primary)]">
            Sign in to Admin Console
          </h1>
          <p className="text-[13.5px] text-[var(--text-secondary)] mt-1.5">
            Restricted access. Authorized administrators only.
          </p>
        </div>

        {/* Card */}
        <div className="pn-card p-6 sm:p-7 shadow-lg">
          {error && (
            <div className="mb-5 p-3.5 rounded-lg bg-[var(--danger-soft)] border border-[var(--danger-border)] text-[var(--danger-text)] text-xs flex items-start gap-2.5">
              <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            {/* Username */}
            <div>
              <label
                htmlFor="username"
                className="block text-[13px] font-medium text-[var(--text-primary)] mb-1.5"
              >
                Username
              </label>
              <div className="relative flex items-center">
                <span className="absolute left-3 text-[var(--text-muted)] pointer-events-none">
                  <User className="w-4 h-4" />
                </span>
                <input
                  id="username"
                  type="text"
                  required
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  placeholder="Knight"
                  className="w-full text-sm text-[var(--text-primary)] bg-[var(--input-bg)] border border-[var(--border-strong)] rounded-lg pl-9 pr-3.5 py-2.5 outline-none focus:border-[var(--accent)] focus:ring-2 focus:ring-[var(--accent)]/15 transition-all font-mono"
                />
              </div>
            </div>

            {/* Password */}
            <div>
              <div className="flex justify-between items-center mb-1.5">
                <label
                  htmlFor="password"
                  className="text-[13px] font-medium text-[var(--text-primary)]"
                >
                  Password
                </label>
                <Link
                  href="/reset-password"
                  className="text-[12.5px] text-[var(--accent)] hover:underline font-medium"
                >
                  Forgot password?
                </Link>
              </div>
              <div className="relative flex items-center">
                <span className="absolute left-3 text-[var(--text-muted)] pointer-events-none">
                  <Lock className="w-4 h-4" />
                </span>
                <input
                  id="password"
                  type={showPassword ? "text" : "password"}
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••••••"
                  className="w-full text-sm text-[var(--text-primary)] bg-[var(--input-bg)] border border-[var(--border-strong)] rounded-lg pl-9 pr-10 py-2.5 outline-none focus:border-[var(--accent)] focus:ring-2 focus:ring-[var(--accent)]/15 transition-all font-mono"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute right-2.5 p-1 rounded text-[var(--text-muted)] hover:text-[var(--text-primary)] transition-colors cursor-pointer"
                  title={showPassword ? "Hide password" : "Show password"}
                >
                  {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            {/* Submit */}
            <button
              type="submit"
              disabled={loading}
              className="w-full mt-2 font-medium text-sm py-2.5 px-3.5 rounded-lg border-none bg-[var(--accent)] hover:bg-[var(--accent-hover)] text-white cursor-pointer inline-flex items-center justify-center gap-2 transition-colors disabled:opacity-50 shadow-sm"
            >
              {loading ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <>
                  <span>Sign in</span>
                  <ArrowRight className="w-4 h-4" />
                </>
              )}
            </button>
          </form>

          {/* Card Footer */}
          <div className="flex items-center justify-center gap-2 mt-5 pt-5 border-t border-[var(--border)] text-[12.5px] text-[var(--text-secondary)]">
            <span className="w-2 h-2 rounded-full bg-[var(--success)] shadow-[0_0_0_3px_var(--success-soft)]"></span>
            <span>WireGuard virtual router operational</span>
          </div>
        </div>
      </div>
    </div>
  );
}
