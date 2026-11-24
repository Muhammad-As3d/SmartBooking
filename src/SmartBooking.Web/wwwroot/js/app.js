// لوحة التحكم: أي تغيير في الطابور (حجز جديد، نداء، إنهاء) بيحدّث الجزء الحي من الصفحة من غير reload.
(() => {
  "use strict";
  const target = document.querySelector("[data-live-src]");
  const connEl = document.querySelector("[data-conn]");
  if (!target || !window.signalR) {
    connEl?.classList.add("hide");
    return;
  }

  const src = target.dataset.liveSrc;
  let busy = false;

  async function refresh() {
    if (busy) return;
    busy = true;
    try {
      const r = await fetch(src, { cache: "no-store", headers: { "X-Requested-With": "fetch" } });
      if (r.ok) target.innerHTML = await r.text();
      else if (r.status === 401) location.reload();
    } catch {
      /* تجاهل */
    } finally {
      busy = false;
    }
  }

  window.dori.connect({
    join: (conn) => conn.invoke("WatchDashboard"),
    handlers: {
      queueChanged: window.dori.debounce(refresh, 300),
      ticketCalled: window.dori.debounce(refresh, 300),
    },
    onState: (on, reconnected) => {
      window.dori.setConn(connEl, on);
      if (reconnected) refresh();
    },
  });

  document.addEventListener("visibilitychange", () => {
    if (!document.hidden) refresh();
  });
})();
