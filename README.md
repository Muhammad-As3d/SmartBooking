# دوري (SmartBooking)

منصة SaaS لإدارة الطوابير والحجز اللحظي للعيادات والمختبرات ومكاتب الخدمات.
ASP.NET Core 10 MVC + EF Core 10 (SQL Server) + SignalR (WebSockets) + Web Push.

## التشغيل

```bash
# 1) إنشاء قاعدة البيانات (الـ migration جاهز ومش بيتنفذ تلقائياً)
dotnet tool restore
dotnet ef database update --project src/SmartBooking.Infrastructure --startup-project src/SmartBooking.Web

# 2) التشغيل
dotnet run --project src/SmartBooking.Web
```

بديل للخطوة 1: نفّذ `db/SmartBooking.sql` (سكربت idempotent) على SQL Server.

الـ connection string في `src/SmartBooking.Web/appsettings.json` على `Server=.` (Windows Authentication).

### مدير المنصة (أنت كصاحب الـ SaaS)
1. حط بريدك في `Platform:AdminEmails` في `appsettings.json`.
2. افتح `/account/setup` مرة واحدة واعمل الحساب. بعدها لوحة الإدارة على `/admin`.

### إشعارات الجوال (Web Push)
لو `WebPush:PublicKey/PrivateKey` فاضيين، بيتولدوا مرة واحدة ويتحفظوا في `App_Data/vapid.json`.
في الإنتاج انقلهم للإعدادات (User Secrets / متغيرات البيئة) وغيّر `WebPush:Subject` لبريدك.
الإشعارات محتاجة HTTPS (ماعدا localhost).

## الصفحات

| الرابط | الوصف |
|---|---|
| `/` | الصفحة التسويقية والباقات |
| `/account/register` | تسجيل منشأة جديدة (14 يوم تجربة) |
| `/q/{slug}` | صفحة الحجز اللي الـ QR بيفتحها |
| `/t/{token}` | التذكرة الحية في جوال العميل |
| `/d/{slug}` | شاشة الصالة (نداء بالصوت) |
| `/app` | لوحة تحكم المنشأة |
| `/app/queue/{counterId}` | شاشة الشباك / الدكتور |
| `/admin` | لوحة مدير المنصة |

## المعمارية

```
src/
  SmartBooking.Core            الكيانات + قواعد العمل (حساب وقت الانتظار، الاشتراكات) بدون أي dependency
  SmartBooking.Infrastructure  EF Core، الخدمات، التنبيه الذكي (BackgroundService)، Web Push
  SmartBooking.Web             MVC + SignalR Hub + الواجهة
```

### منع تضارب الحجوزات
- **رقم التذكرة**: `MERGE ... WITH (HOLDLOCK)` على جدول `DailySequences` جوه نفس الـ transaction، فعميلين في نفس الملّي ثانية ياخدوا رقمين مختلفين.
- **شبكة أمان**: unique index على `(ServiceId, ServiceDate, Number)`.
- **نفس الجوال مرتين**: filtered unique index `UX_Tickets_ActivePhone`، والتعارض بيرجّع التذكرة الموجودة.
- **شباكين ضغطوا "التالي" مع بعض**: `UPDLOCK, READPAST, ROWLOCK`، كل شباك بياخد عميل مختلف من غير ما حد يستنى التاني.

### الأداء
- كل الاستعلامات **Projections** (`Select` لـ DTO/record) و `NoTracking` افتراضياً.
- أكتر استعلام بيتنفذ (حالة التذكرة) **compiled query**.
- الكتابة بـ `ExecuteUpdate/ExecuteDelete` من غير تحميل الكيانات.
- بيانات المنشأة متكاشة 30 ثانية (`TenantDirectory`).
- فهارس مخصوصة لكل استعلام ساخن (منها filtered indexes للتنبيهات والطابور).
- Output cache للصفحة الرئيسية، Brotli، static files مع cache-busting.
- التحديث اللحظي بـ SignalR groups، والعميل بيطلب حالته بـ jitter عشان ما يحصلش ضغط لحظي.

### الأمان
- عزل كامل بين المنشآت: `TenantId` من الـ cookie الموقّعة فقط + global query filters.
- Identity مع lockout، Antiforgery على كل POST، Rate limiting على الحجز والدخول.
- CSP صارمة (مفيش inline scripts ولا inline styles)، HSTS، X-Frame-Options، Referrer-Policy.
- رابط التذكرة رمز عشوائي 128-bit غير قابل للتخمين.

### الاشتراكات
- 3 باقات (seed في الـ migration) بحدود: خدمات، نقاط خدمة، مستخدمين، تذاكر شهرية، شاشة عرض.
- الفاتورة بتتعمل من لوحة العميل، ومدير المنصة بيأكد الدفع من `/admin` فيتمد الاشتراك تلقائياً.
- لربط بوابة دفع (Paymob / Fawry / Moyasar / Stripe): نادِ `BillingService.MarkInvoicePaidAsync` من الـ webhook بتاعها.
