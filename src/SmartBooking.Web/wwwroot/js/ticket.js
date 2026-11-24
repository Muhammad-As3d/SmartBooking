// التذكرة الحية: بتسمع لأي تغيير في طابور الخدمة وتحدّث "قدامك كام" و"الوقت المتوقع" لحظياً.
(() => {
  "use strict";
  const root = document.querySelector("[data-ticket]");
  if (!root) return;

  const token = root.dataset.ticket;
  const csrf = root.querySelector('input[name="__RequestVerificationToken"]')?.value;
  const $ = (s) => root.querySelector(s);
  const connEl = $("[data-conn]");
  let status = root.dataset.status;
  let live = false;

  const FINAL = ["Completed", "NoShow", "Cancelled"];

  async function refresh() {
    try {
      const r = await fetch(`/t/${token}/status`, { cache: "no-store", headers: { Accept: "application/json" } });
      if (!r.ok) return;
      render(await r.json());
    } catch {
      /* الشبكة هتتعاد مع أول حدث */
    }
  }

  function render(d) {
    if (d.status !== status && FINAL.includes(d.status)) {
      location.reload();
      return;
    }
    const becameCalled = d.status === "Called" && status !== "Called";
    status = d.status;

    $("[data-status-text]").textContent = d.statusLabel;
    const tag = $("[data-status-label]");
    tag.classList.toggle("amber", d.status === "Called" || d.status === "InService");
    $("[data-card]").classList.toggle("called", d.status === "Called");

    const waiting = d.status === "Waiting";
    $("[data-waiting]").classList.toggle("hide", !waiting);
    if (waiting) {
      $("[data-ahead]").textContent = d.ahead;
      $("[data-eta]").textContent = Math.max(0, Math.ceil(d.etaSeconds / 60));
    }

    const counter = $("[data-counter]");
    counter.classList.toggle("hide", waiting || !d.counterName);
    if (d.counterName) $("[data-counter-name]").textContent = d.counterName;

    if (becameCalled) alertUser("حان دورك الآن", `اتفضل على ${d.counterName ?? "الشباك"}`);
  }

  function alertUser(title, body) {
    navigator.vibrate?.([300, 150, 300, 150, 300]);
    window.dori.beep(3);
    const box = $("[data-alert-box]");
    if (box) {
      box.querySelector("b").textContent = title;
      box.querySelector("[data-push-hint]").textContent = body;
    }
    if (document.hidden && "Notification" in window && Notification.permission === "granted") {
      navigator.serviceWorker?.getRegistration().then((reg) =>
        reg?.showNotification(title, { body, tag: token, lang: "ar", dir: "rtl", icon: "/img/favicon.svg" })
      );
    }
  }

  window.dori.connect({
    join: (conn) => conn.invoke("WatchTicket", token),
    handlers: {
      // jitter بسيط عشان مش كل التذاكر تسأل السيرفر في نفس اللحظة
      queueChanged: window.dori.debounce(refresh, 300 + Math.random() * 700),
      ticketCalled: () => refresh(),
      alert: (a) => alertUser(a.title, a.body),
    },
    onState: (on, reconnected) => {
      live = on;
      window.dori.setConn(connEl, on);
      if (reconnected) refresh();
    },
  });

  // احتياطي لو الـ WebSocket مقطوع
  setInterval(() => {
    if (!live && !document.hidden) refresh();
  }, 30000);
  document.addEventListener("visibilitychange", () => {
    if (!document.hidden) refresh();
  });

  $("[data-copy-link]")?.addEventListener("click", async (e) => {
    try {
      await navigator.clipboard.writeText(location.href);
      e.currentTarget.textContent = "تم النسخ ✓";
    } catch {
      prompt("انسخ الرابط:", location.href);
    }
  });

  // ===== Web Push =====
  const pushBtn = $("[data-enable-push]");
  const vapid = root.dataset.vapid;
  const supported = "serviceWorker" in navigator && "PushManager" in window && "Notification" in window;

  if (pushBtn && (!supported || root.dataset.push !== "1")) pushBtn.classList.add("hide");

  pushBtn?.addEventListener("click", async () => {
    pushBtn.disabled = true;
    try {
      const permission = await Notification.requestPermission();
      if (permission !== "granted") {
        pushBtn.textContent = "الإشعارات مقفولة من المتصفح";
        return;
      }
      const reg = await navigator.serviceWorker.register("/sw.js");
      await navigator.serviceWorker.ready;
      const sub =
        (await reg.pushManager.getSubscription()) ||
        (await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: toBytes(vapid) }));
      const json = sub.toJSON();
      const r = await fetch(`/t/${token}/push`, {
        method: "POST",
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": csrf },
        body: JSON.stringify({ endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth }),
      });
      if (!r.ok) throw new Error();
      pushBtn.classList.add("hide");
      $("[data-push-hint]").textContent = "تمام، هيوصلك إشعار حتى لو قفلت الصفحة.";
    } catch {
      pushBtn.disabled = false;
      pushBtn.textContent = "جرّب تاني";
    }
  });

  function toBytes(base64) {
    const pad = "=".repeat((4 - (base64.length % 4)) % 4);
    const raw = atob((base64 + pad).replace(/-/g, "+").replace(/_/g, "/"));
    return Uint8Array.from(raw, (c) => c.charCodeAt(0));
  }
})();
