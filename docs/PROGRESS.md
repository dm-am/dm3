# DM3 — Прогресс реализации

Спутник к [Документация_по_разработке_DM3.docx](Документация_по_разработке_DM3.docx) (полный план). Здесь — функциональная полнота по зонам и инвентарь страниц.

**Легенда:** ✅ построено и в тестах · 🔎 требует живого осмотра владельцем · 🟡 частично (бэкенд-зазор) · ⬜ не начато.

## Сводка тестов (после reset+seed 2026-07-14)
Backend: `dotnet build DM.sln` 0/0; `dotnet test` — **14 проектов, 1697 тестов + DM.Web.API 54 = зеленые**. Frontend: `vue-tsc` чист, `eslint` 0 ошибок, `vitest` **798/798**. Сид: 22 юзера, 69 игр, 9326 постов, 306 персонажей, 28 топиков, 18 блогов, 45 публикаций, 19 опросов, 116 отзывов. диакритическая е в src/docs — 0; елочки — только девиз в About.

## Функциональные зоны

| Зона | Статус | Заметки |
|------|--------|---------|
| Аутентификация (вход/регистрация/восстановление/сброс/подтверждение) | ✅ | honeypot+тайминг антибот; имя меняемо через модерацию |
| Профиль + подстраницы (отзывы/рекомендации/оцененные/загруженное/редактирование) | ✅ | H1 "Профиль:"; вкладки about/games/blogs/topics/achievements в странице; отзывы/загруженное — отдельные страницы (по доку) |
| Награды/Достижения | ✅ | награды timeless + серии; достижения — цепочки по метрикам; "Лит #22" бейдж |
| Сообщество / Опросы / Статистика сайта | ✅ | статистика: 8 топ-десяток (игроки/игры/блоги × оценки/активность/объем), период в URL (?year&month / ?period=all), пикер месяца/года с 2007 (12-летние блоки), серверные positive-only ранги с делеными местами при ничьих, кэш периодов (закрытые — сессия/24ч) |
| Игровая зона (инфо/комнаты/чат-комнаты/персонажи/схема атрибутов/посты/отзывы/заметки/настройки/премодерация/статусы) | ✅ | схема 6 типов; персонаж 4 статуса+3 флага; комнаты 2 типа + архив; статусы Draft/Active/Closed+ClosedReason |
| Блог-зона (инфо/рубрики/публикации/обсуждение/заметки/настройки/премодерация/статусы) | ✅ | зеркало игровой; publicId, счетчики+реордер рубрик, инлайн-редактирование, ментор-гейт — закрыты |
| Форум (индекс/раздел/тема/создание/редактирование) | ✅ | навигация полос " \| " унифицирована с профилем |
| Глобальный чат + эвенты | ✅ / 🔎 | эвент-баннер — на осмотр владельцу (CHAT-1/5/13) |
| Личные сообщения / Уведомления / Подписки / Блокнот | ✅ | |
| Обращения (Поддержка/Жалобы/Мои обращения) | ✅ / 🟡 | единая модель Ticket (7 подтипов, 4 статуса, тред, гостевой); зазор: гостевой email опционален, серверных фильтров у /mine нет |
| Модерация (11 страниц + панель) | ✅ / 🔎 | контекстный сайдбар; 6 модалок → Lightbox (вид изменился — на осмотр); варны 0-6+автобан; баны SeniorMod+ + история |
| Наставничество (панель + премодерация) | ✅ | панель наставника; премодерация игр и блогов (Mentor+) |
| Боты/уведомления | ✅ / 🟡 | единый webhook-путь `/v1/webhooks/{type}/{secret}`; Discord inbound-контракт — на подтверждение |
| Правовые / О проекте / Правила / Помочь проекту | ✅ | |
| Страницы ошибок | ✅ | по указанию владельца не трогались (кроме дефолтной картинки) |
| Мобильная версия | ✅ / 🔎 | бургер + левый off-canvas drawer (вариант A): разделы сайта + контекстное меню игры/блога/модерации, правый сайдбар под контентом, токены брейкпоинтов; проверено на 375px; финальный визуальный осмотр за владельцем |

## Бэкенд-зазоры — ЗАКРЫТЫ (волна фиксов 2026-07-14)
- ✅ Блог отдает `publicId` (ссылки канонические, 5-буквенные как у игр).
- ✅ Рубрики: счетчики (N/A) + эндпоинты переименования (PATCH) и реордера (PUT rubrics/order).
- ✅ Raw-source для инлайн-редактирования описания блога/инфо игры (author_edit envelope).
- ✅ Ментор блога в DTO + гейт заметок (owner+assistant+mentor).
- ✅ Статистика: период "все время" (year=0) + 8 топ-десяток (вкл. блоговые) + publicId игр/блогов в топах; позже (2026-07-17): positive-фильтр и competition-ранги на сервере, валидация year/month (400), удалены мертвые reports/compare/legacy-stats эндпоинты.
- ✅ `tickets/mine` серверные фильтры (status+subtype); гостевой email обязателен + трекинг-эндпоинт `/v1/tickets/track` (токен в заголовке).
- ✅ `GetUpload` → Moderator+.

**Остаточные (минорные):** 3 сид-блога с placeholder-publicId (`t...`, из внешнего сид-пути; работают, но неканонично — фикс реседом); Discord inbound-webhook контракт — подтвердить при подключении реального бота.

## Верификация (reset+seed 2026-07-14, порт 5174 / API 5000)
Живой проход: **53/53 PASS**. Все страницы рендерятся, все 14 авторизованных эндпоинтов гейтятся (401/403), гостевой intake 400. Один регресс найден и починен: `/game/{id}/details` OOM (декартово произведение проекции GameDetails) → `AsSplitQuery` на 4 запросах, теперь 200 за ~0.4с. Флаг: SignalR-хаб `/whatsup` дает negotiate-404 на :5174 (реалтайм не подключается — проверить apiHost/прокси/гость; не сегодняшний регресс).

## Инвентарь страниц (кликабельные, seed-id)
**Гость/общие:** [/](http://localhost:5174/) · [/about](http://localhost:5174/about) · [/rules](http://localhost:5174/rules) · [/polls](http://localhost:5174/polls) · [/testimonials](http://localhost:5174/testimonials) · [/pulse](http://localhost:5174/pulse) · [/community](http://localhost:5174/community) · [/global-chat](http://localhost:5174/global-chat) · [/privacy](http://localhost:5174/privacy) · [/agreement](http://localhost:5174/agreement) · [/support](http://localhost:5174/support) · [/complaint](http://localhost:5174/complaint) · [/statistics](http://localhost:5174/statistics) · [/error/404](http://localhost:5174/error/404)
**Аккаунт (требует входа):** /account · /notifications · /subscriptions · /notepad · /my-tickets · /warnings
**Профиль (SolohinLex):** [/users/SolohinLex](http://localhost:5174/users/SolohinLex) (+вкладки about/games/blogs/topics/achievements) · /received-reviews · /given-reviews · /received-endorsements · /given-endorsements · /uploads
**Игры:** [/games](http://localhost:5174/games) · /games/create · [/game/aaaaa](http://localhost:5174/game/aaaaa) (+/rooms /rooms/:num /chat-rooms/:num /characters /characters/create /characters/:id/edit /settings /comments /reviews /post-reviews /notes)
**Блоги:** [/blogs](http://localhost:5174/blogs) · /blogs/create · [/blogs/e6b1e8c0-…](http://localhost:5174/blogs/e6b1e8c0-f713-4e58-93b6-d0715ed34980) (+/feed /feed/create /feed/:pubId/edit /comments /settings /notes)
**Форум:** [/forum](http://localhost:5174/forum) · [/forum/general](http://localhost:5174/forum/general) · [/forum/general/4](http://localhost:5174/forum/general/4)
**Чат/сообщения:** [/global-chat](http://localhost:5174/global-chat) · /messenger · /messenger/c/:id · /messenger/user/:username
**Модерация (Moderator+):** /moderation · /moderation/moderators · /moderation/games · /moderation/blogs · /moderation/bans · /moderation/warnings · /moderation/rated-posts · /moderation/new-users · /moderation/violators · /moderation/support · /moderation/complaints · /moderation/tickets/:id · /moderation/uploads · /moderation/username-changes · /moderation/tags · /moderation/awards · /moderation/award-types · /moderation/achievements · /moderation/fundraising
**Auth/служебные:** /auth/callback · /activate/:token · /confirm-email/:token · /reset-password/:token · /error/:code
