# DM3 — Прогресс реализации

Спутник к [Документация_по_разработке_DM3.docx](Документация_по_разработке_DM3.docx) (полный план). Здесь — функциональная полнота по зонам и инвентарь страниц.

**Легенда:** ✅ построено и в тестах · 🔎 требует живого осмотра владельцем · 🟡 частично (бэкенд-зазор) · ⬜ не начато.

## Гейты
Числа тестов и объемы сида здесь не хранятся: они меняются каждой волной и устаревают быстрее документа. Что гонять до пуша и в каком порядке, знает `scripts/hooks/pre-push`, там же сказано, какие джобы воркфлоу оставлены за CI. Текстовые правила (буква "е", кавычки, разделители) держат тесты фронта, а не строка в этом документе.

## Функциональные зоны

| Зона | Статус | Заметки |
|------|--------|---------|
| Аутентификация (вход/регистрация/восстановление/сброс/подтверждение) | ✅ | honeypot+тайминг антибот; имя меняемо через модерацию |
| Профиль + подстраницы (отзывы/рекомендации/оцененные/загруженное/редактирование) | ✅ | H1 "Профиль:"; вкладки about/games/blogs/topics/achievements в странице; отзывы/загруженное — отдельные страницы (по доку) |
| Награды/Достижения | ✅ | награды timeless + серии; достижения — цепочки по метрикам; "Лит #22" бейдж |
| Сообщество / Опросы / Статистика сайта | ✅ | статистика: 8 топ-десяток (игроки/игры/блоги × оценки/активность/объем), период в URL (?year&month / ?period=all), пикер месяца/года с 2007 (12-летние блоки), серверные positive-only ранги с делеными местами при ничьих, кэш периодов (закрытые — сессия/24ч) |
| Игровая зона (инфо/комнаты/чат-комнаты/персонажи/схема атрибутов/посты/отзывы/заметки/настройки/премодерация/статусы) | ✅ | схема 6 типов; персонаж 4 статуса+3 флага; комнаты 2 типа + архив; статусы Draft/Active/Closed+ClosedReason |
| Блог-зона (инфо/рубрики/публикации/обсуждение/заметки/настройки/премодерация/статусы) | ✅ | зеркало игровой; publicId, счетчики+реордер рубрик, инлайн-редактирование, гейт наставника — закрыты |
| Форум (индекс/раздел/тема/создание/редактирование) | ✅ | навигация полос " \| " унифицирована с профилем |
| Глобальный чат + эвенты | ✅ / 🔎 | эвент-баннер — на осмотр владельцу (CHAT-1/5/13) |
| Личные сообщения / Уведомления / Подписки / Блокнот | ✅ | |
| Обращения (Поддержка/Жалобы/Мои обращения) | ✅ / 🟡 | единая модель Ticket (7 подтипов, 4 статуса, тред, гостевой); зазор: гостевой email опционален, серверных фильтров у /mine нет |
| Модерация (страницы + панель) | ✅ / 🔎 | контекстный сайдбар; 6 модалок → Lightbox (вид изменился — на осмотр); варны 0-6+автобан; баны SeniorMod+ + история |
| Наставничество (панель + премодерация) | ✅ | панель наставника; премодерация игр и блогов (Mentor+) |
| Боты/уведомления | ✅ / 🟡 | единый webhook-путь `/v1/webhooks/{type}/{secret}`; Discord inbound-контракт — на подтверждение |
| Правовые / О проекте / Правила / Помочь проекту | ✅ | |
| Страницы ошибок | ✅ | по указанию владельца не трогались (кроме дефолтной картинки) |
| Мобильная версия | ✅ / 🔎 | бургер + левый off-canvas drawer (вариант A): разделы сайта + контекстное меню игры/блога/модерации, правый сайдбар под контентом, токены брейкпоинтов; проверено на 375px; финальный визуальный осмотр за владельцем |

## Бэкенд-зазоры — ЗАКРЫТЫ (волна фиксов 2026-07-14)
- ✅ Блог отдает `publicId` (ссылки канонические, 5-буквенные как у игр).
- ✅ Рубрики: счетчики (N/A) + эндпоинты переименования (PATCH) и реордера (PUT rubrics/order).
- ✅ Raw-source для инлайн-редактирования описания блога/инфо игры (author_edit envelope).
- ✅ Наставник блога в DTO + гейт заметок (owner+assistant+mentor).
- ✅ Статистика: период "все время" (year=0) + 8 топ-десяток (вкл. блоговые) + publicId игр/блогов в топах; позже (2026-07-17): positive-фильтр и competition-ранги на сервере, валидация year/month (400), удалены мертвые reports/compare/legacy-stats эндпоинты.
- ✅ `tickets/mine` серверные фильтры (status+subtype); гостевой email обязателен + трекинг-эндпоинт `/v1/tickets/track` (токен в заголовке).
- ✅ `GetUpload` → Moderator+.

**Остаточные (минорные):** 3 сид-блога с placeholder-publicId (`t...`, из внешнего сид-пути; работают, но неканонично — фикс реседом); Discord inbound-webhook контракт — подтвердить при подключении реального бота.

## Живой проход
Инвентарь ниже и есть маршрут прохода: открывается по порядку на поднятом стеке
после `reset+seed`. Результат конкретного прохода тут не хранится — он устаревает
к следующей волне, а найденное живет в аудите и в истории коммитов.

## Инвентарь страниц (ссылки на дев-сервер, seed-id)
**Гость/общие:** [/](http://localhost:5173/) · [/about](http://localhost:5173/about) · [/rules](http://localhost:5173/rules) · [/polls](http://localhost:5173/polls) · [/testimonials](http://localhost:5173/testimonials) · [/pulse](http://localhost:5173/pulse) · [/community](http://localhost:5173/community) · [/global-chat](http://localhost:5173/global-chat) · [/privacy](http://localhost:5173/privacy) · [/agreement](http://localhost:5173/agreement) · [/support](http://localhost:5173/support) · /support/track/:token · [/complaint](http://localhost:5173/complaint) · [/statistics](http://localhost:5173/statistics) · [/error/404](http://localhost:5173/error/404)
**Аккаунт (требует входа):** /account · /notifications · /subscriptions · /notepad · /my-tickets · /warnings
**Профиль (SolohinLex):** [/users/SolohinLex](http://localhost:5173/users/SolohinLex) · /users/SolohinLex/about · /users/SolohinLex/games · /users/SolohinLex/blogs · /users/SolohinLex/topics · /users/SolohinLex/achievements · /users/SolohinLex/received-reviews · /users/SolohinLex/given-reviews · /users/SolohinLex/received-endorsements · /users/SolohinLex/given-endorsements · /users/SolohinLex/uploads
**Игры:** [/games](http://localhost:5173/games) · /games/create · [/game/aaaaa](http://localhost:5173/game/aaaaa) · /game/aaaaa/rooms/:num · /game/aaaaa/chat-rooms/:num · /game/aaaaa/characters · /game/aaaaa/characters/create · /game/aaaaa/characters/:characterId/edit · /game/aaaaa/settings · /game/aaaaa/comments · /game/aaaaa/reviews · /game/aaaaa/post-reviews · /game/aaaaa/notes · /game/aaaaa/posts/unread · /game/aaaaa/comments/unread
**Блоги:** [/blogs](http://localhost:5173/blogs) · /blogs/create · [/blogs/e6b1e8c0-f713-4e58-93b6-d0715ed34980](http://localhost:5173/blogs/e6b1e8c0-f713-4e58-93b6-d0715ed34980) · /blogs/:id/feed · /blogs/:id/feed/create · /blogs/:id/feed/:pubId/edit · /blogs/:id/comments · /blogs/:id/settings · /blogs/:id/notes
**Форум:** [/forum](http://localhost:5173/forum) · [/forum/general](http://localhost:5173/forum/general) · [/forum/general/4](http://localhost:5173/forum/general/4) · /forum/general/4/unread · /forum-topic/:topicId
**Чат/сообщения:** [/global-chat](http://localhost:5173/global-chat) · /messenger · /messenger/c/:id · /messenger/user/:username
**Модерация (Moderator+):** /moderation · /moderation/moderators · /moderation/games · /moderation/blogs · /moderation/bans · /moderation/warnings · /moderation/rated-posts · /moderation/new-users · /moderation/violators · /moderation/support · /moderation/complaints · /moderation/tickets/:id · /moderation/uploads · /moderation/username-changes · /moderation/tags · /moderation/awards · /moderation/awards/series/:id · /moderation/award-types · /moderation/achievements · /moderation/fundraising
**Auth/служебные:** /auth/callback · /activate/:token · /confirm-email/:token · /reset-password/:token · /error/:code
