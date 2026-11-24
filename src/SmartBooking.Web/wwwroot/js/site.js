// أدوات عامة لكل الصفحات: ساعة الـ HUD، تأكيد قبل الإرسال، إخفاء الرسائل، اقتراح رابط المنشأة.
(() => {
  "use strict";

  // ساعة TC 00:00:00 زي الفيديو
  const clocks = document.querySelectorAll("[data-clock]");
  if (clocks.length) {
    const pad = (n) => String(n).padStart(2, "0");
    const tick = () => {
      const d = new Date();
      const text = `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
      clocks.forEach((c) => (c.textContent = text));
    };
    tick();
    setInterval(tick, 1000);
  }

  // ديمو الصفحة الرئيسية: متزامن مع حركة الطابور (3.2 ثانية)، اللي قدامك بيقلّوا لحد ما ييجي دورك
  const demoAhead = document.querySelector("[data-demo-ahead]");
  if (demoAhead && !window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    const eta = document.querySelector("[data-demo-eta]");
    const code = document.querySelector("[data-demo-code]");
    const card = code?.closest(".ticket-card");
    let ahead = 3;
    let ticket = 14;
    setInterval(() => {
      ahead -= 1;
      if (ahead < 0) {
        ahead = 3;
        ticket += 1;
        code.textContent = `#A-${ticket}`;
        code.classList.remove("tick");
        void code.offsetWidth;
        code.classList.add("tick");
      }
      demoAhead.textContent = ahead;
      eta.textContent = ahead === 0 ? "0" : String(ahead * 7 + 1);
      card?.classList.toggle("called", ahead === 0);
    }, 3200);
  }

  document.addEventListener("submit", (e) => {
    const form = e.target.closest("[data-confirm]");
    if (form && !window.confirm(form.dataset.confirm)) e.preventDefault();
  });

  document.querySelectorAll("[data-autohide]").forEach((el) => {
    setTimeout(() => el.classList.add("hide"), 6000);
  });

  // قائمة الموبايل في لوحة التحكم
  const side = document.querySelector("[data-side]");
  document.querySelectorAll("[data-menu]").forEach((btn) =>
    btn.addEventListener("click", () => side?.classList.toggle("open"))
  );

  // رابط المنشأة: تنظيف أثناء الكتابة + فحص التوفر
  const slug = document.querySelector("[data-slug]");
  const status = document.querySelector("[data-slug-status]");
  if (slug && status) {
    const original = status.textContent;
    let timer;
    slug.addEventListener("input", () => {
      const clean = slug.value.toLowerCase().replace(/[^a-z0-9-]/g, "-").replace(/-+/g, "-").replace(/^-/, "");
      if (clean !== slug.value) slug.value = clean;
      clearTimeout(timer);
      if (clean.length < 3) {
        status.textContent = original;
        status.classList.remove("amber");
        return;
      }
      timer = setTimeout(async () => {
        try {
          const r = await fetch(`/account/slug-available?slug=${encodeURIComponent(clean)}`);
          const { available } = await r.json();
          status.textContent = available ? "الرابط متاح ✓" : "الرابط ده مش متاح، جرّب غيره.";
          status.classList.toggle("amber", available);
        } catch {
          /* تجاهل: التحقق النهائي على السيرفر */
        }
      }, 350);
    });
  }
})();

// أدوات مشتركة لصفحات الـ realtime
window.dori = {
  debounce(fn, wait) {
    let t;
    return (...args) => {
      clearTimeout(t);
      t = setTimeout(() => fn(...args), wait);
    };
  },

  // اتصال SignalR بيعيد المحاولة لوحده، ولما يرجع بيعمل join تاني للمجموعة
  async connect({ join, handlers, onState }) {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/queue")
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
      .build();

    Object.entries(handlers).forEach(([name, fn]) => conn.on(name, fn));
    conn.onreconnecting(() => onState?.(false));
    conn.onreconnected(async () => {
      await join(conn);
      onState?.(true, true);
    });
    conn.onclose(() => {
      onState?.(false);
      setTimeout(start, 5000);
    });

    async function start() {
      try {
        await conn.start();
        await join(conn);
        onState?.(true);
      } catch {
        onState?.(false);
        setTimeout(start, 4000);
      }
    }
    await start();
    return conn;
  },

  setConn(el, on) {
    if (!el) return;
    el.classList.toggle("on", on);
    el.lastChild.textContent = on ? " LIVE" : " RECONNECTING";
    el.querySelector(".dot")?.classList.toggle("live", on);
  },

  beep(times = 2) {
    try {
      const ctx = new (window.AudioContext || window.webkitAudioContext)();
      for (let i = 0; i < times; i++) {
        const o = ctx.createOscillator();
        const g = ctx.createGain();
        o.type = "sine";
        o.frequency.value = i % 2 ? 660 : 880;
        const t0 = ctx.currentTime + i * 0.35;
        g.gain.setValueAtTime(0.0001, t0);
        g.gain.exponentialRampToValueAtTime(0.25, t0 + 0.02);
        g.gain.exponentialRampToValueAtTime(0.0001, t0 + 0.3);
        o.connect(g).connect(ctx.destination);
        o.start(t0);
        o.stop(t0 + 0.32);
      }
    } catch {
      /* المتصفح ممكن يمنع الصوت قبل أي تفاعل */
    }
  },
};
