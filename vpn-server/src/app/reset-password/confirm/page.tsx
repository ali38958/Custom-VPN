"use client";

import { useState, Suspense } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import Link from "next/link";
import { Lock, ArrowRight, Loader2, CheckCircle2, AlertCircle } from "lucide-react";

function ConfirmResetContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams.get("token");

  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (password !== confirmPassword) {
      setError("Passwords do not match");
      return;
    }

    if (password.length < 8) {
      setError("Password must be at least 8 characters");
      return;
    }

    setLoading(true);

    try {
      const res = await fetch("/api/auth/reset-password/confirm", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token, password }),
      });

      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.error || "Failed to reset password");
      }

      setSuccess(true);
      setTimeout(() => {
        router.push("/login");
      }, 2500);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Something went wrong");
    } finally {
      setLoading(false);
    }
  };

  if (!token) {
    return (
      <div className="w-full max-w-[400px] text-center p-6 pn-card shadow-lg">
        <AlertCircle className="w-10 h-10 text-[var(--danger)] mx-auto mb-3" />
        <h2 className="text-base font-semibold text-[var(--text-primary)]">Invalid Link</h2>
        <p className="text-xs text-[var(--text-secondary)] mt-1 mb-4">No reset token provided or the link is broken.</p>
        <Link href="/login" className="pn-btn pn-btn-primary justify-center w-full">
          Return to Sign In
        </Link>
      </div>
    );
  }

  return (
    <div className="w-full max-w-[400px]">
      <div className="text-center mb-7">
        <h1 className="text-[22px] font-semibold tracking-tight text-[var(--text-primary)]">
          Create New Password
        </h1>
        <p className="text-[13.5px] text-[var(--text-secondary)] mt-1.5">
          Set a fresh security key for Knight
        </p>
      </div>

      <div className="pn-card p-6 sm:p-7 shadow-lg">
        {success ? (
          <div className="text-center py-2">
            <CheckCircle2 className="w-11 h-11 text-[var(--success)] mx-auto mb-3" />
            <h3 className="text-base font-semibold text-[var(--text-primary)]">Password Changed</h3>
            <p className="text-xs text-[var(--text-secondary)] mt-1.5 mb-2">Redirecting to login with your new credentials...</p>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            {error && (
              <div className="p-3.5 rounded-lg bg-[var(--danger-soft)] border border-[var(--danger-border)] text-[var(--danger-text)] text-xs flex items-start gap-2.5">
                <AlertCircle className="w-4 h-4 text-[var(--danger)] shrink-0 mt-0.5" />
                <span>{error}</span>
              </div>
            )}

            <div>
              <label className="block text-[13px] font-medium text-[var(--text-primary)] mb-1.5">
                New Password
              </label>
              <div className="relative flex items-center">
                <span className="absolute left-3 text-[var(--text-muted)] pointer-events-none">
                  <Lock className="w-4 h-4" />
                </span>
                <input
                  type="password"
                  required
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••••••"
                  className="w-full text-sm text-[var(--text-primary)] bg-[var(--input-bg)] border border-[var(--border-strong)] rounded-lg pl-9 pr-3.5 py-2.5 outline-none focus:border-[var(--accent)] font-mono"
                />
              </div>
            </div>

            <div>
              <label className="block text-[13px] font-medium text-[var(--text-primary)] mb-1.5">
                Confirm New Password
              </label>
              <div className="relative flex items-center">
                <span className="absolute left-3 text-[var(--text-muted)] pointer-events-none">
                  <Lock className="w-4 h-4" />
                </span>
                <input
                  type="password"
                  required
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder="••••••••••••"
                  className="w-full text-sm text-[var(--text-primary)] bg-[var(--input-bg)] border border-[var(--border-strong)] rounded-lg pl-9 pr-3.5 py-2.5 outline-none focus:border-[var(--accent)] font-mono"
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full mt-2 font-medium text-sm py-2.5 px-3.5 rounded-lg border-none bg-[var(--accent)] hover:bg-[var(--accent-hover)] text-white cursor-pointer inline-flex items-center justify-center gap-2 transition-colors disabled:opacity-50 shadow-sm"
            >
              {loading ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <>
                  <span>Save Password</span>
                  <ArrowRight className="w-4 h-4" />
                </>
              )}
            </button>
          </form>
        )}
      </div>
    </div>
  );
}

export default function ConfirmResetPage() {
  return (
    <div className="flex-1 flex items-center justify-center p-6 sm:p-12">
      <Suspense fallback={<div className="text-[var(--text-muted)] text-sm">Loading...</div>}>
        <ConfirmResetContent />
      </Suspense>
    </div>
  );
}
