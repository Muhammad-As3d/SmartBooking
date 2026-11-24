// صفحة الحجز: تحديث الوقت المتوقع لكل خدمة كل 30 ثانية من غير ما الاختيار يضيع.
(() => {
  "use strict";
  const list = document.querySelector("[data-live-services]");
  if (!list) return;

  const form = document.querySelector("[data-booking]");
  form?.addEventListener("submit", () => {
    const btn = form.querySelector("button[type=submit]");
    btn.disabled = true;
  });

  async function refresh() {
    if (document.hidden) return;
    try {
      const r = await fetch(location.pathname, { cache: "no-store" });
      if (!r.ok) return;
      const doc = new DOMParser().parseFromString(await r.text(), "text/html");
      doc.querySelectorAll("[data-eta]").forEach((fresh) => {
        const el = list.querySelector(`[data-eta="${fresh.dataset.eta}"]`);
        if (el) el.replaceChildren(...fresh.cloneNode(true).childNodes);
        const meta = fresh.closest(".box")?.querySelector(".meta");
        const target = el?.closest(".box")?.querySelector(".meta");
        if (meta && target) target.textContent = meta.textContent;
      });
    } catch {
      /* تجاهل */
    }
  }
  setInterval(refresh, 30000);
})();
