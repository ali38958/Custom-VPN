"use client";

import { useState } from "react";
import Link from "next/link";
import { KeyRound, Mail, ArrowLeft, Loader2, CheckCircle2, AlertCircle } from "lucide-react";

export default function RequestResetPage() {
  const [email, setEmail] = useState("");
  const [loading, setLoading] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);

    try {
      const res = await fetch("/api/auth/reset-password/request", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
      });

      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.error || "Failed to send reset link");
      }

      setSubmitted(true);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Something went wrong");
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
            <KeyRound className="w-5 h-5" />
          </div>
          <h1 className="text-[22px] font-semibold tracking-tight text-[var(--text-primary)]">
            Reset Password
          </h1>
          <p className="text-[13.5px] text-[var(--text-secondary)] mt-1.5">
            Send a recovery link to the registered admin email.
          </p>
        </div>

        {/* Card */}
        <div className="pn-card p-6 sm:p-7 shadow-lg">
          {submitted ? (
            <div className="text-center py-2">
              <div className="w-11 h-11 bg-[var(--success-soft)] border border-[var(--success-border)] rounded-full flex items-center justify-center mx-auto mb-3.5 text-[var(--success-text)]">
                <CheckCircle2 className="w-5 h-5" />
              </div>
              <h3 className="text-base font-semibold text-[var(--text-primary)] mb-1.5">
                Check Your Inbox
              </h3>
              <p className="text-xs text-[var(--text-secondary)] mb-6 leading-relaxed">
                If the email matches the registered administrator address, a reset link has been dispatched.
              </p>
              <Link
                href="/login"
                className="pn-btn pn-btn-primary w-full justify-center"
              >
                <ArrowLeft className="w-4 h-4" />
                <span>Return to Sign In</span>
              </Link>
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
                  Admin Registered Email
                </label>
                <div className="relative flex items-center">
                  <span className="absolute left-3 text-[var(--text-muted)] pointer-events-none">
                    <Mail className="w-4 h-4" />
                  </span>
                  <input
                    type="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="admin@example.com"
                    className="w-full text-sm text-[var(--text-primary)] bg-[var(--input-bg)] border border-[var(--border-strong)] rounded-lg pl-9 pr-3.5 py-2.5 outline-none focus:border-[var(--accent)] font-mono"
                  />
                </div>
              </div>

              <button
                type="submit"
                disabled={loading}
                className="w-full mt-2 font-medium text-sm py-2.5 px-3.5 rounded-lg border-none bg-[var(--accent)] hover:bg-[var(--accent-hover)] text-white cursor-pointer inline-flex items-center justify-center gap-2 transition-colors disabled:opacity-50 shadow-sm"
              >
                {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : "Send Reset Email"}
              </button>

              <div className="pt-3 text-center">
                <Link
                  href="/login"
                  className="inline-flex items-center gap-1.5 text-xs text-[var(--text-secondary)] hover:text-[var(--text-primary)] transition-colors"
                >
                  <ArrowLeft className="w-3.5 h-3.5" />
                  <span>Back to Sign In</span>
                </Link>
              </div>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
