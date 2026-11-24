// بيتحمّل في الـ <head> قبل الرسم عشان الصفحة ما "تومضش" بالوضع الغلط.
// الاختيار بيتحفظ على الجهاز، ولو مفيش اختيار بنمشي على إعداد الجهاز نفسه.
(() => {
  "use strict";
  const KEY = "dori-theme";
  const root = document.documentElement;
  const media = window.matchMedia("(prefers-color-scheme: dark)");

  const stored = () => {
    try { return localStorage.getItem(KEY); } catch { return null; }
  };
  const apply = (theme) => {
    root.dataset.theme = theme;
    document.querySelector('meta[name="theme-color"]')?.setAttribute("content", theme === "dark" ? "#0e1517" : "#f5f2ec");
  };

  apply(stored() || (media.matches ? "dark" : "light"));
  media.addEventListener?.("change", (e) => {
    if (!stored()) apply(e.matches ? "dark" : "light");
  });

  document.addEventListener("click", (e) => {
    const btn = e.target.closest("[data-theme-toggle]");
    if (!btn) return;
    const next = root.dataset.theme === "dark" ? "light" : "dark";
    root.classList.add("theming");
    apply(next);
    try { localStorage.setItem(KEY, next); } catch { /* وضع التصفح الخفي */ }
    setTimeout(() => root.classList.remove("theming"), 500);
  });

  document.addEventListener("DOMContentLoaded", () => apply(root.dataset.theme));
})();
