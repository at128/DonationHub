# أسئلة المناقشة المعمارية — DonationHub

هذا الدليل يصف الكود الموجود فعليًا. إضافة كيانات التبرع والدفع وMoney/ValueObject وقواعدها وعلاقاتها مستثناة بطلب صاحب المشروع؛ لا تُعرض هنا كميزات منفذة.

## 1. لماذا Clean Architecture؟
نعزل قواعد العمل عن HTTP وEF Core. Application يعتمد على Domain ويعرّف واجهات مثل IIdentityGateway، وInfrastructure ينفذها. البديل وضع كل شيء داخل Api؛ أبسط في البداية لكنه يربط الاختبارات والتغييرات بقاعدة البيانات. جواب مختصر: «الاتجاه للداخل حتى تتغير أدوات التنفيذ دون تغيير قواعد العمل».

## 2. لماذا التنظيم حسب Features؟
Features/Auth وFeatures/Categories يجمعان workflow والعقود المتعلقة بكل ميزة. البديل تقسيم الملفات إلى Services وDTOs وValidators لكل المشروع، مما يوزع التعديل على أماكن متباعدة. التنظيم الحالي خدمات مباشرة داخل كل feature؛ الانتقال إلى handler مستقل لكل use case ممكن لاحقًا دون إدخال MediatR الآن. جواب: «أبحث عن الميزة بدل البحث عن نوع الملف».

## 3. لماذا Repository للكتابة وqueries مباشرة للقراءة؟
CategoryService يحمّل entity متتبعًا، يستدعي طرقه ثم يحفظ عبر IGenericRepository وIUnitOfWork. CategoryQueries ينفذ AsNoTracking ثم Where/OrderBy ثم Select إلى DTO داخل SQL. البديل إجبار كل التقارير على repository يعيد entities كاملة؛ يقل تكرار الواجهات لكنه يزيد التحميل والتتبع. جواب: «الكتابة تحافظ على invariants؛ القراءة تطلب شكل البيانات الذي تحتاجه فقط».

## 4. لماذا Unit of Work؟
EfUnitOfWork يستخدم نفس AppDbContext المشارك في scope الطلب. إبطال refresh token وإضافة بديله يُحفظان مرة واحدة؛ فشل concurrency يعيد العملية كلها. AppUserStore يؤجل أيضًا حفظ المستخدم وربط دوره حتى حفظ UoW واحد. البديل الحفظ داخل كل repository؛ يؤدي إلى commits جزئية. جواب: «حد الحفظ هو العملية المكتملة، وليس كل سطر تعديل».

## 5. ما الفرق بين Entity وValue Object؟
Category له Id ثابت وتستمر هويته بعد تغيير الاسم. Value Object تقارنه بقيم خصائصه، مثل مبلغ وعملة، دون هوية مستقلة. Money مثال تعليمي هنا، وهو غير منفذ في النطاق الحالي. البديل decimal وحده أبسط لكنه لا يجمع قواعد المبلغ والعملات. جواب: «Entity تُعرف بهويتها، وValue Object تُعرف بقيمتها».

## 6. لماذا private setters؟
لا يمكن تعديل Category.NameEn مباشرة؛ يمر التغيير عبر Update التي تتحقق من القيم قبل تعيينها. البديل public setters مريح للربط لكنه يسمح بحالة غير صالحة. خصائص ApplicationUser المخصصة بقيت كما كانت ضمن استثناء بند Domain. جواب: «أحمي الحالة من التعديل الذي يتجاوز قواعدها».

## 7. لماذا Factory Methods؟
Category.Create يرفض الاسم الفارغ أو الطويل ويولد Id. البديل constructor عام فارغ مع object initializer يسمح بإنشاء كيان ناقص. بالنسبة إلى الحسابات، UserManager يطبق تحقق Identity وتجزئة كلمة المرور. جواب: «الإنشاء نقطة دخول تضمن شروط البداية».

## 8. لماذا invariants داخل Domain؟
Category.EnsureCanBeDeleted يستقبل hasCampaigns ويقرر السماح أو الرفض. Application يحصل على المعلومة من قاعدة البيانات؛ Domain لا يعرف EF ولا ينفذ query. البديل وضع القرار في endpoint يكرر القاعدة بين المداخل المختلفة. جواب: «التطبيق يجلب الحقائق والكيان يطبق القاعدة».

## 9. لماذا تمرير الوقت أو حقن TimeProvider؟
AuthService وJwtTokenService وAuditingInterceptor يستخدمون الساعة المحقونة. اختبارات AuthTests تثبت الوقت وتختبر انتهاء refresh عند الحد نفسه. Campaign.Create يستقبل nowUtc أصلًا. البديل قراءة الساعة الفعلية داخل كل طريقة يجعل الاختبار تابعًا لوقت التنفيذ. جواب: «الوقت مدخل يمكن التحكم فيه واختباره».

## 10. لماذا EF Fluent API؟
CampaignConfiguration يضبط decimal(18,2)، RowVersion، الفلتر وعلاقة Category مع Restrict. تبقى هذه التفاصيل خارج Domain. البديل EF Data Annotations يربط الكيانات بأداة التخزين ويقلل مرونة الإعداد. Data Annotations الموجودة على Options مخصصة للتحقق من الإعدادات وليست mapping. جواب: «قواعد التخزين في Infrastructure وقواعد العمل في Domain».

## 11. لماذا Soft Delete؟
AuditingInterceptor يحول Deleted إلى Modified ويسجل IsDeleted وDeletedAtUtc، ويحافظ على CreatedAt ويحدث UpdatedAt. HasQueryFilter يخفي الحملات المحذوفة، بينما فحص حذف التصنيف يتجاوز الفلتر حتى يرى المراجع القديمة. البديل hard delete أبسط لكنه يفقد التاريخ وقد يكسر المراجع. جواب: «أخفي السجل مع الاحتفاظ بعلاقاته وتاريخه».

## 12. لماذا RowVersion؟
Campaign.RowVersion مهيأ بـ IsRowVersion، فيضيف EF القيمة القديمة إلى شرط UPDATE. إذا تغير السجل فلا يؤثر التحديث على أي صف ويظهر concurrency conflict. البديل آخر كتابة تفوز قد يفقد تحديثًا سابقًا، أو locking يزيد الانتظار. Campaign endpoints ومعالجة تعارضاتها ليست ضمن هذا التغيير. جواب: «أكتشف أن البيانات تغيرت بدل الكتابة فوق تغيير شخص آخر».

## 13. لماذا JWT وRefresh Tokens؟
Access token عمره الافتراضي 15 دقيقة ويحمل sub والبريد والأدوار؛ refresh عمره 7 أيام ويُراجع من قاعدة البيانات. البديل جلسة خادم مركزية أسهل في الإلغاء الفوري لكنها تحتاج lookup أو مخزن جلسات. Logout يلغي refresh؛ access token الصادر سابقًا يبقى صالحًا حتى انتهاء عمره ما لم تضف سياسة إلغاء أخرى. جواب: «وصول قصير العمر مع تجديد يمكن إبطاله من الخادم».

## 14. لماذا نحفظ Hash للـRefresh Token؟
JwtTokenService يولد 32 بايت عشوائية ويحفظ SHA-256 فقط. تسريب جدول التوكنات لا يعطي المهاجم النص الذي يرسله العميل. البديل plaintext يجعل القيمة المسروقة قابلة للاستخدام مباشرة. كلمات المرور يعالجها PasswordHasher الخاص بـIdentity وليست SHA-256 البسيطة. جواب: «أحتاج مقارنة التوكن، ولا أحتاج استرجاع أصله».

## 15. لماذا Rotation؟
AuthService.RefreshAsync يبطل القديم، يربطه بـReplacedByTokenId ويضيف بديلًا. RevokedAt concurrency token يمنع نجاح طلبين على اللقطة القديمة، وSaveChanges واحد يمنع بقاء بديل لطلب خاسر. البديل refresh ثابت أسهل لكنه يطيل فرصة استخدام نسخة مسروقة. إلغاء العائلة كاملة عند replay إضافة أخرى غير منفذة. جواب: «كل تجديد يستهلك التوكن مرة واحدة».

## 16. لماذا IOptions؟
JwtOptions وSeedAdminOptions وDonationOptions لها تحقق على startup، وتُقرأ داخل الخدمات عبر IOptions. المفتاح Base64 يُفك قبل التحقق من طول البايتات. البديل IConfiguration داخل كل خدمة يوزع أسماء المفاتيح والتحقق والتحويل. جواب: «إعدادات typed ومتحقق منها قبل أول عملية على قاعدة البيانات».

## 17. IOptions أم Snapshot أم Monitor؟
IOptions يعطي إعدادات مستقرة مناسبة لهذه الخدمات. IOptionsSnapshot scoped ويعيد حسابها لكل scope؛ IOptionsMonitor singleton ويتابع التغيير ويدعم OnChange. البديلان يفيدان عند الحاجة لتحديث حي، لكن تغيير مفاتيح JWT يحتاج خطة لقبول التوكنات السابقة. جواب: «أختار حسب دورة حياة الإعداد والحاجة لتغييره أثناء التشغيل».

## 18. ما DI lifetimes؟
Singleton نسخة للتطبيق، مثل TimeProvider.System. Scoped نسخة لكل request/scope، مثل AuthService وDbContext وrepositories. Transient نسخة كل resolve، مناسب لمكونات خفيفة لا تشارك حالة. لا نخزن خدمة Scoped داخل Singleton. جواب: «العمر يحدد من يشارك الحالة ومتى تُحرر الموارد».

## 19. لماذا DbContext Scoped؟
العملية تحتاج change tracker واحدًا حتى تُحفظ التعديلات معًا. Singleton يشارك حالة غير آمنة للتزامن بين الطلبات؛ Transient عشوائي قد يفصل repositories عن UoW. الاختبارات تستخدم contexts منفصلة لمحاكاة متنافسين. جواب: «سياق واحد للعملية، وسياق مستقل لكل طلب».

## 20. لماذا لا نستخدم Task.Run مع EF؟
ToListAsync وSaveChangesAsync ينفذان I/O غير متزامن أصلًا. Task.Run يضيف scheduling وقد يشغل نفس DbContext على threads متوازية رغم أنه غير thread-safe. البديل الصحيح await المباشر وتمرير CancellationToken. جواب: «async للـI/O لا يحتاج تغليفه بعمل على thread آخر».

## 21. كيف نتعامل مع concurrency؟
أسماء التصنيفات والبريد محمية بقيود unique، وسباق التكرار يرجع 409. FK يحمي حذف التصنيف، بما فيه السباق بعد فحص وجود حملات. Refresh يستخدم optimistic concurrency ويفشل الطلب الخاسر بـ401 دون تجاهل بقية أخطاء قاعدة البيانات. البديل Serializable لكل عملية يزيد locking واحتمال deadlocks. جواب: «الفحص يعطي رسالة واضحة وقيد قاعدة البيانات يحمي السباق».

## 22. كيف تعمل Roles والصلاحيات؟
IdentitySeeder ينشئ Admin وCampaignManager وDonor، والتسجيل العام يختار Donor داخل Application. endpoints الإدارية تستخدم RequireRole("Admin")؛ عدم وجود JWT يعطي 401، والدور غير المسموح 403. Logout يطابق UserId مع مالك التوكن. البديل التحقق في العميل لا يحمي الخادم. جواب: «الخادم يقرر الصلاحية والمالك من هوية مصادق عليها».

## 23. ما أهم المفاضلات؟
نستخدم feature services مباشرة مع interfaces بدل إطار CQRS إضافي؛ يقل عدد الأدوات مع بقاء الطريق مفتوحًا للتقسيم إلى handlers. AppUserStore يضيف تعديلًا محدودًا لدعم حفظ المستخدم ودوره معًا؛ البديل ترك حفظ Identity الافتراضي داخل transaction مع عدة SaveChanges. اختبارات PersistenceTests تثبت عدد مرات الحفظ وذرية العملية. الكيانات وValue Objects المستثناة تظل نواقص معلنة ولا نعطي درجة مكتملة بناءً على نجاح الاختبارات فقط. جواب: «أبرر الإضافة بمشكلة فعلية واختبار يثبت سلوكها، وأعلن حدود النطاق».

مرجع تنفيذ Identity المستخدم: [UserStore في ASP.NET Core 10.0.14](https://github.com/dotnet/aspnetcore/blob/v10.0.14/src/Identity/EntityFrameworkCore/src/UserStore.cs).
