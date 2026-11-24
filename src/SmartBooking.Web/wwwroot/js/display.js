// شاشة الصالة: أول ما موظف ينادي رقم، الرقم يظهر كبير + صوت + نطق بالعربي.
(() => {
  "use strict";
  const root = document.querySelector("[data-display]");
  if (!root) return;

  const slug = root.dataset.display;
  const board = root.querySelector("[data-board]");
  const connEl = root.querySelector("[data-conn]");
  const soundBtn = root.querySelector("[data-sound]");
  let soundOn = false;
  let live = false;

  async function refresh() {
    try {
      const r = await fetch(`/d/${slug}/state`, { cache: "no-store" });
      if (!r.ok) return;
      const before = board.querySelector("[data-now]")?.dataset.now;
      board.innerHTML = await r.text();
      const now = board.querySelector("[data-now]");
      if (now && now.dataset.now && now.dataset.now !== before) now.classList.add("flash-in");
    } catch {
      /* تجاهل */
    }
  }

  function announce(code, counter) {
    if (!soundOn) return;
    window.dori.beep(2);
    if (!("speechSynthesis" in window)) return;
    // "A-014" تتنطق "A 14"
    const spoken = code.replace("-", " ").replace(/\b0+(\d)/, "$1");
    const u = new SpeechSynthesisUtterance(`رقم ${spoken}، إلى ${counter}`);
    u.lang = "ar-SA";
    u.rate = 0.9;
    setTimeout(() => speechSynthesis.speak(u), 800);
  }

  soundBtn?.addEventListener("click", () => {
    soundOn = !soundOn;
    soundBtn.classList.toggle("primary", soundOn);
    if (soundOn) window.dori.beep(1);
  });

  window.dori.connect({
    join: (conn) => conn.invoke("WatchDisplay", slug),
    handlers: {
      queueChanged: window.dori.debounce(refresh, 250),
      ticketCalled: (t) => {
        refresh();
        announce(t.code, t.counter);
      },
    },
    onState: (on, reconnected) => {
      live = on;
      window.dori.setConn(connEl, on);
      if (reconnected) refresh();
    },
  });

  setInterval(() => {
    if (!live) refresh();
  }, 20000);

  // الشاشة شغالة طول اليوم: نمنع إنها تطفي لو المتصفح بيدعم ده
  navigator.wakeLock?.request("screen").catch(() => {});
})();
