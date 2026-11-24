// Service worker: بيستقبل إشعارات Web Push للتذكرة حتى لو الصفحة مقفولة.
self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", (e) => e.waitUntil(self.clients.claim()));

self.addEventListener("push", (event) => {
  let data = {};
  try {
    data = event.data ? event.data.json() : {};
  } catch {
    data = { title: "دوري", body: event.data?.text() ?? "" };
  }
  event.waitUntil(
    self.registration.showNotification(data.title || "دوري", {
      body: data.body || "",
      tag: data.tag,
      renotify: true,
      requireInteraction: true,
      lang: "ar",
      dir: "rtl",
      icon: "/img/favicon.svg",
      vibrate: [300, 150, 300],
      data: { url: data.url || "/" },
    })
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const url = new URL(event.notification.data?.url || "/", self.location.origin).href;
  event.waitUntil(
    self.clients.matchAll({ type: "window", includeUncontrolled: true }).then((list) => {
      const open = list.find((c) => c.url === url);
      return open ? open.focus() : self.clients.openWindow(url);
    })
  );
});
