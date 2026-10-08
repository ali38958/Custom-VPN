"use client";

import { useEffect, useState } from "react";
import { Sun, Moon } from "lucide-react";

export default function ThemeToggle() {
  const [theme, setTheme] = useState<"dark" | "light">("dark");

  useEffect(() => {
    const saved = localStorage.getItem("pn-theme") as "dark" | "light" | null;
    if (saved === "light") {
      setTheme("light");
      document.documentElement.setAttribute("data-theme", "light");
    } else {
      setTheme("dark");
      document.documentElement.setAttribute("data-theme", "dark");
    }
  }, []);

  const toggleTheme = () => {
    const next = theme === "dark" ? "light" : "dark";
    setTheme(next);
    document.documentElement.setAttribute("data-theme", next);
    localStorage.setItem("pn-theme", next);
  };

  return (
    <button
      onClick={toggleTheme}
      type="button"
      className="p-2 rounded-lg border border-[var(--border)] bg-[var(--card-bg)] text-[var(--text-secondary)] hover:text-[var(--text-primary)] hover:bg-[var(--sidebar-active)] transition-colors inline-flex items-center justify-center cursor-pointer"
      title={`Switch to ${theme === "dark" ? "Bright" : "Dark"} theme`}
      aria-label="Toggle theme"
    >
      {theme === "dark" ? (
        <Sun className="w-4 h-4 text-amber-400" />
      ) : (
        <Moon className="w-4 h-4 text-indigo-600" />
      )}
    </button>
  );
}
