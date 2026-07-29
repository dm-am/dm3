# API Design — DM3

> **User Story:** "Какие принципы API? Какие форматы ответов?"

> **SSOT:** Полный список endpoint'ов — в Swagger: http://localhost:5000/

---

## Базовая информация

| Параметр | Значение |
|----------|----------|
| Base URL | `https://api.dm.am` (prod), `http://localhost:5000` (dev) |
| API Version | v1 |
| Формат | JSON, UTF-8 |
| Аутентификация | HttpOnly Cookie (BFF Pattern) |

---

## Философия

> **Подход:** Pragmatic REST (Stripe, GitHub)

1. **Понятность > Догма** — читаемые URL важнее REST-пуризма
2. **Консистентность** — одинаковые паттерны везде
3. **Предсказуемость** — ожидаемое поведение
4. **Простота** — минимум сложности

---

## Три типа endpoints

| Тип | HTTP | Глаголы в URL | Пример |
|-----|------|---------------|--------|
| **Resource** | GET/POST/PATCH/DELETE | Нет | `/users/{id}` |
| **Query** | GET | Допустимы | `/account/check-email` |
| **Action** | POST | Допустимы | `/account/login`, `/games/{id}/join` |

---

## Форматы ответов

| Тип ответа | Формат |
|------------|--------|
| Одиночный ресурс | `Envelope<T>` с полем `resource` |
| Коллекция | `ListEnvelope<T>` с `paging` |
| Курсорная пагинация | `CursorEnvelope<T>` с `cursor` |
| Ошибка | RFC 7807 `ProblemDetails` |

```json
// Одиночный ресурс — Envelope
{ "resource": { "id": "...", "username": "john" } }

// Коллекция — ListEnvelope
{ "resources": [...], "paging": { "skip": 0, "take": 20, "total": 150 } }

// Ошибка — ProblemDetails, Content-Type: application/problem+json
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Blog not found", "status": 404, "traceId": "00-..." }

// Ошибка валидации — то же плюс errors по полям
{ "title": "Validation failed", "status": 400,
  "errors": { "AcceptedRules": ["Необходимо принять правила сайта"] } }
```

> **Правило:** `Envelope<T>` — стандарт для всех одиночных ресурсов.
> Endpoint'ы, возвращающие DTO без конверта, — legacy; при доработке приводить к `Envelope<T>`.

> **Об ошибках.** Машиночитаемого кода ошибки в контракте нет: тип ошибки несет
> HTTP-статус, человекочитаемое сообщение лежит в `title`. Заводить поле `code` не
> нужно — нужно, чтобы статус был выбран правильно. Сообщение необработанного
> исключения клиенту не уходит никогда: 500 отдает постоянный заголовок и токен
> корреляции для поиска в логах.

---

## Коды ответов

| Код | Когда использовать |
|-----|-------------------|
| 200 | Успешный запрос |
| 201 | Ресурс создан (POST) |
| 204 | Успешно, без тела (DELETE, actions) |
| 400 | Невалидные параметры |
| 401 | Требуется аутентификация |
| 403 | Недостаточно прав |
| 404 | Ресурс не найден |
| 409 | Конфликт состояния |
| 410 | Токен/приглашение истекло |
| 429 | Rate limit превышен |

**DELETE** возвращает `204 No Content` без тела.
Записанное исключение: `DELETE /v1/polls/{id}/vote` возвращает `200` с обновленным
`Envelope<Poll>` — клиент сразу перерисовывает результаты опроса без повторного запроса.

**Секрет никогда не едет в пути URL.** Путь пишется дословно в журнал доступа
обратного прокси, в лог запроса и в трейс, поэтому секрет в пути раскрыт по
построению, а его ротация — это чистка логов, а не смена переменной. Место
секрета — заголовок: у интеграции со своим соглашением берется ее заголовок
(`X-Telegram-Bot-Api-Secret-Token`), у прочих — `X-Dm-Webhook-Secret`. Сравнение
— `CryptographicOperations.FixedTimeEquals`, а незаданный секрет закрывает
эндпоинт, а не открывает его.

**`CancellationToken` принимают только GET-экшены.** Мутация выполняется до
конца: многошаговые записи в слое хранения неатомарны (транзакция на весь слой
заводится в трех местах), поэтому отмена на середине оставляет половину записи
без отката и без следа — игру с временным публичным идентификатором, который
никто не пересчитает, или погашенное приглашение, оставшееся действительным.
Правило держит тест `MutatingActionsShould`. Разрыв соединения клиентом не
ошибка: `ErrorHandlingMiddleware` отвечает на него отдельной веткой, а не
записью уровня Critical.

**Заголовок `Location` у 201** указывает на созданный ресурс и ни на что другое.
Если у подресурса нет собственного адреса (нет `GET .../{childId}`), заголовок не
ставится вовсе — `201` без `Location` допустим, а `Location`, ведущий на коллекцию,
лжет: клиент, перешедший по нему, получит весь список вместо созданной записи.
Выбор между «завести адрес элемента» и «не ставить заголовок» делается по тому,
нужен ли этот адрес кому-то кроме заголовка.

---

## Pagination, Filtering & Sorting

### Принцип: Один endpoint — много сценариев

> **Правило:** Вместо специализированных endpoints (`/popular`, `/active`, `/owned`)
> используй **один list endpoint** с параметрами фильтрации и сортировки.

**Преимущества:**
- Меньше endpoints = проще API
- Гибкость для клиента
- Единая логика авторизации
- Кэширование по URL

### Pagination

| Тип | Параметры | Использование |
|-----|-----------|---------------|
| Offset | `?skip=20&take=10` | Списки с известным total |
| Cursor | `?cursor=abc&limit=50` | Бесконечный скролл |

### Filtering

```
GET /v1/games?statuses=Active,Draft&authorUsernames=ivan&requiredTags=42
GET /v1/posts?roomId=abc&authorId=xyz
GET /v1/users?role=moderator&isOnline=true
```

**Паттерны:**
- Enum фильтры: `?statuses=Active,Draft` (массив через запятую или повтор параметра)
- Boolean: `?isOpen=true`
- ID reference: `?authorId=abc`
- Числовые: `?minRating=5&maxPlayers=10`

### Sorting

```
GET /v1/games?sort=subscribersCount:desc     # Popular = по подписчикам
GET /v1/games?sort=lastPostUtc:desc          # Active = по последнему посту
GET /v1/games?sort=createdUtc:desc           # Newest
GET /v1/posts?sort=rating:desc&take=1        # Best post
```

**Формат:** `?sort=field:asc|desc` (default: `desc`)

### Примеры замены специальных endpoints

| Было (отдельный endpoint) | Стало (query parameters) |
|---------------------------|-------------------------|
| `GET /games/popular` | `GET /games?sortBy=popularity&take=10` |
| `GET /games/owned` | `GET /games?participating=true` |
| `GET /blogs/popular` | `GET /blogs?sortBy=popularity&take=10` |
| `GET /blogs/owned` | `GET /blogs?participating=true` |
| `GET /posts/best` | `GET /posts?sortBy=rating&take=1` |

> **`me` keyword:** Для текущего пользователя можно использовать `authorId=me`
> или отдельный флаг `?participating=true`.

### Когда отдельный endpoint оправдан

1. **Actions** (изменяют состояние): `/games/{id}/join`, `/posts/{id}/like`
2. **Агрегации** (сложные вычисления): `/statistics/overview`
3. **Специфичные форматы**: `/export/csv`

### Записанные исключения (семантические шорткаты)

Специализированные GET, которые осознанно оставлены вместо параметров фильтрации:

- `GET /v1/users/by-role/{role}`
- `GET /v1/users/{username}/best-publication`
- `GET /v1/users/{username}/best-topic`
- `GET /v1/users/{username}/bans/active`

Это устойчивые продуктовые понятия ("лучшая публикация", "активный бан"), а не комбинации
фильтров; отдельный endpoint здесь читается лучше, чем набор query-параметров.

---

## Rate Limiting

| Эндпоинты | Лимит |
|-----------|-------|
| Глобальный | 100 req/min на IP |
| Аутентификация | 5 req/min на IP |
| Username check | 20 req/min на IP |
| Email check | 10 req/min на IP |

**При превышении:** `429 Too Many Requests` + заголовок `Retry-After`

---

## Кеширование ответов

`Cache-Control: public` разрешен только для ответа, одинакового для всех: анонимного,
любого пользователя и любого значения `X-Dm-Audience`. Достаточно одного персонального
поля (счетчик непрочитанного, признак "мое"), чтобы ответ стал `private` — серверный
кеш и любой прокси ключуются по методу и пути, без `Vary`, и отдадут данные первого
запросившего всем остальным.

**Правило:** личное — `private`; общее и кешируемое — `public` плюс явный `Vary` на
каждый заголовок, от которого ответ зависит.

---

## Health Endpoints

| Endpoint | Назначение |
|----------|-----------|
| `GET /_health` | Liveness (Docker) |
| `GET /_ready` | Readiness (DB checks) |
| `GET /_health/detail` | Детальная информация |
| `GET /metrics` | Prometheus metrics |

---

## Realtime (SignalR)

| Параметр | Значение |
|----------|----------|
| Endpoint | `/whatsup` |
| Протокол | WebSocket |
| Аутентификация | HttpOnly cookie сессии (та же, что у обычных запросов) |

**Особенности:**
- Receive-only (клиент не вызывает методы)
- Connection tracking — in-memory
- Адресация по connection ID (без групп)
- Токен в query-строке не принимается: URL попадает в логи прокси, а это
  обесценивает HttpOnly-куку, ради которой существует BFF

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/whatsup", { withCredentials: true })
  .build();

connection.on("Send", (notification) => {
  console.log(notification.eventType, notification.payload);
});
```

---

## Rendering audience header (`X-Dm-Audience`)

Поля ответов, содержащие пользовательский BBCode, рендерятся сервером под запрошенный **audience**. Клиент передает желаемое намерение в HTTP-заголовке `X-Dm-Audience`; при отсутствии заголовка используется `display`.

| Значение | Назначение |
|---|---|
| `display` | Обычное чтение. Privacy-теги фильтруются по правам текущего пользователя. Default. |
| `author_edit` | Автор загружает свой контент в редактор. Сервер отдает HTML с `data-bb-*` round-trip атрибутами. Endpoint-уровень обязан подтвердить авторство. |
| `plain_text` | Plain-text пайплайны. Privacy-теги вырезаются безусловно. |
| `embed_safe` | Link preview / cross-post embed. NSFW-safe tag set, privacy-теги вырезаются. |

Неизвестное значение заголовка трактуется как `display`. Контракт audience, правила видимости тегов, модель surface и permission-бакеты кэша — в [BBCODE_RENDERING.md](../architecture/BBCODE_RENDERING.md).

---

## Swagger Groups

API организован в 9 групп по доменам:

| Группа | Описание |
|--------|----------|
| Account | Регистрация, вход, восстановление |
| Personal | Профиль и настройки текущего пользователя |
| Messaging | Личные сообщения и чат |
| Community | Профили, отзывы, опросы |
| Game | Игры и все связанное |
| Blog | Блоги и публикации |
| Forum | Форумы и обсуждения |
| Moderation | Инструменты модерации |
| General | Зеркала, поиск, загрузки |

**Детали:** см. Swagger UI

---

## Ссылки

- [CODE_STYLE.md](./CODE_STYLE.md) — стандарты кода
- [BBCODE_RENDERING.md](../architecture/BBCODE_RENDERING.md) — контракт рендеринга BBCode
- [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) — как работает вход
- [AUTHORIZATION.md](../architecture/AUTHORIZATION.md) — роли и права
