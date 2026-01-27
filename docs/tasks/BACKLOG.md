# Бэклог задач DM3

> **Метаданные:**
> - Документ: BACKLOG.md
> - Версия: 1.0
> - Дата создания: 2026-01-27
> - Последнее обновление: 2026-01-27

---

## Содержание

- [Приоритизированный бэклог](#приоритизированный-бэклог)
- [Качество кода](#качество-кода)
- [Тестирование](#тестирование)
- [Производительность](#производительность)
- [Frontend доработки](#frontend-доработки)
- [API консистентность](#api-консистентность)
- [DevOps и инфраструктура](#devops-и-инфраструктура)
- [Технический долг](#технический-долг)
- [Nice-to-have функции](#nice-to-have-функции)
- [Удалено из плана](#удалено-из-плана)

---

## Приоритизированный бэклог

### P0: Критический приоритет

> **Требуется выполнить до релиза MVP**

| ID | Задача | Затраты | Уверенность |
|----|--------|---------|-------------|
| ~~P0.1~~ | ~~OpenIddict Development Certificates~~ | ~~2 часа~~ | ✅ ВЫПОЛНЕНО |
| ~~P0.2~~ | ~~Outbox Pattern Implementation~~ | ~~1 день~~ | ✅ ВЫПОЛНЕНО |
| ~~P0.3~~ | ~~Consumer Error Handling~~ | ~~3 часа~~ | ✅ ВЫПОЛНЕНО |

---

### P1: Высокий приоритет

> **Желательно выполнить до релиза MVP**

| ID | Задача | Затраты | Уверенность |
|----|--------|---------|-------------|
| P1.0 | API Response Code Inconsistencies | 1 день | 90% |
| ~~P1.1~~ | ~~PBKDF2 Iterations (100k→600k)~~ | ~~1 час~~ | ✅ ВЫПОЛНЕНО |
| ~~P1.2~~ | ~~API Response Codes (3 endpoints)~~ | ~~2 часа~~ | ✅ ВЫПОЛНЕНО |
| P1.3 | ConfigureAwait(false) во всех await | 1 день | 100% |
| ~~P1.4~~ | ~~Удалить BBCodeEditor-REFACTORED.vue~~ | ~~1 час~~ | ✅ ВЫПОЛНЕНО |
| P1.5 | Рефакторинг BbParserWrapper (376 строк) | 2 дня | 85% |

---

### P2: Средний приоритет

> **Выполнить после MVP, но до v1.5**

| ID | Задача | Затраты | Уверенность |
|----|--------|---------|-------------|
| P2.0 | Hexagonal Architecture Violations | 2 дня | 60% |
| P2.1 | Hardcoded Crypto Keys | 3 часа | 60% |
| P2.2 | Missing Events (NewUser, NewMessage, etc.) | 1 день | 80% |
| ~~P2.3~~ | ~~Tests for Mail/Notifications/Search~~ | ~~2 недели~~ | ✅ ЧАСТИЧНО (Notifications: 28, Uploading: 41, MessageQueuing: 13) |
| P2.4 | Удалить NotImplementedException endpoints | 1 день | 100% |

---

### P3: Низкий приоритет

> **Выполнить в v1.5-v2.0**

| ID | Задача | Затраты | Уверенность |
|----|--------|---------|-------------|
| P3.1 | DbContext в ExternalAuthController | 1 день | 60% |
| P3.2 | Стандартные файлы (CHANGELOG, CONTRIBUTING, LICENSE) | 4 часа | 70% |
| P3.3 | Добавить DB индексы (Games.Status, etc.) | 1 день | 90% |
| P3.4 | Расширить кэширование (User profiles, etc.) | 3 дня | 80% |
| P3.5 | Удалить console.log из production кода | 1 час | 100% |

---

### P4: Опциональные задачи

> **Nice-to-have, выполнить при наличии времени**

| ID | Задача | Затраты | Условие |
|----|--------|---------|---------|
| P4.1 | Унифицировать HTTP статусы (404 vs 410) | 1 день | После API стабилизации |
| P4.2 | Стандартизировать URL структуру | 1 неделя | После v1.5 |
| P4.3 | Google/VK OAuth | 1 неделя | После стабилизации Discord |
| P4.4 | Umami аналитика | 2 дня | После production deploy |
| P4.5 | k3s/Helm deployment | 1 неделя | При увеличении нагрузки |

---

## Качество кода

### Рефакторинг и чистый код

| Задача | Файл/Область | Усилия | Приоритет | Описание |
|--------|--------------|--------|-----------|----------|
| ConfigureAwait(false) | Все async методы | 1 день | P1 | Добавить во все await для performance |
| Удалить дубликат файла | BBCodeEditor-REFACTORED.vue | 1 час | P1 | Оставить только BBCodeEditor.vue |
| Рефакторинг BbParserWrapper | BbParserWrapper.cs (376 строк) | 2 дня | P1 | Разбить на pipeline stages |
| Удалить console.log | Frontend | 1 час | P4 | Найти и удалить все debug логи |
| Honeypot Validation Service | AccountController, LoginController | 4 часа | P2 | Вынести бизнес-логику в service layer |
| DbContext abstraction | ExternalAuthController.cs | 1 день | P3 | Создать IDiscordIntegrationService |
| Cache headers refactoring | Multiple controllers | 2 часа | P2 | Action filters вместо дублирования |

---

### Hardcoded значения

| Задача | Файл | Риск | Приоритет | Решение |
|--------|------|------|-----------|---------|
| Hardcoded Crypto Keys | TripleDesSymmetricCryptoService.cs | Средний | P2 | Добавить логирование при использовании fallback, запланировать удаление |
| Magic numbers | BbParserWrapper.cs | Низкий | P3 | Вынести в константы |
| Hardcoded URLs | Frontend config | Низкий | P3 | Использовать environment variables |

---

## Тестирование

### Покрытие тестами

**Текущее состояние (обновлено 2026-01-27):**

| Module | Покрытие | Файлов с тестами | Всего файлов | Статус |
|--------|----------|------------------|--------------|--------|
| Backend Unit | ~25% | 75+ | ~300 | 🟡 В работе |
| Backend Integration | ~45% | 12 | 24 | 🟢 Хорошо |
| Frontend Unit | ~15% | 23 | 69 | 🟡 В работе |
| E2E тесты | ~25% | 15 | N/A | 🟡 В работе |

**Новые тесты (2026-01-27):**
- DM.Services.Uploading.Tests: **41 тест**
- DM.Services.Notifications.Tests: **28 тестов**
- DM.Services.MessageQueuing.Tests: **13 тестов**
- **Итого добавлено: 82 теста**

---

### Фаза 1: API Layer (2 недели)

**Приоритет:** P2
**Срок:** 2026-02-10

| Задача | Усилия | Тесты | Описание |
|--------|--------|-------|----------|
| Создать IntegrationTests проект | 1 день | 0 | WebApplicationFactory setup |
| API Controllers тесты | 1 неделя | 100+ | Покрыть все 24 контроллера |
| DataAccess тесты | 3 дня | 50+ | InMemory или TestContainers |
| Authentication flow тесты | 2 дня | 20+ | Login, Register, OAuth |
| File upload тесты | 1 день | 10+ | Unit + Integration |

**Создать проекты:**
```
test/
├── DM.Web.API.IntegrationTests/
│   ├── Controllers/
│   │   ├── AccountControllerTests.cs
│   │   ├── GameControllerTests.cs
│   │   └── ... (24 контроллера)
│   ├── Infrastructure/
│   │   ├── WebApplicationFactoryFixture.cs
│   │   └── TestDatabaseFixture.cs
│   └── appsettings.Test.json
└── DM.Services.DataAccess.Tests/
    ├── Repositories/
    └── Migrations/
```

---

### Фаза 2: E2E тесты (2 недели)

**Приоритет:** P2
**Срок:** 2026-02-24

| Задача | Усилия | Тесты | Описание |
|--------|--------|-------|----------|
| Cypress setup | 1 день | 0 | Конфигурация + fixtures |
| Authentication E2E | 2 дня | 10 | Login, Logout, Register, OAuth |
| Forum E2E | 3 дня | 15 | Topics, Comments, Likes |
| Gaming E2E | 3 дня | 15 | Games, Characters, Rooms |
| Messaging E2E | 2 дня | 10 | Conversations, Chat |
| Visual regression | 2 дня | N/A | Percy или BackstopJS |

**Структура E2E тестов:**
```
e2e/
├── cypress/
│   ├── e2e/
│   │   ├── auth/
│   │   ├── forum/
│   │   ├── gaming/
│   │   └── messaging/
│   ├── fixtures/
│   ├── support/
│   └── cypress.config.ts
└── package.json
```

---

### Фаза 3: Frontend тесты (1 неделя)

**Приоритет:** P2
**Срок:** 2026-03-03

| Задача | Усилия | Тесты | Описание |
|--------|--------|-------|----------|
| Component тесты | 3 дня | 50+ | Vitest + Vue Test Utils |
| Store тесты | 1 день | 20+ | Pinia stores |
| Composables тесты | 1 день | 15+ | useApiResource, useFetchData |
| BBCodeEditor тесты | 2 дня | 30+ | Tiptap extensions |

---

### Недостающие тесты для модулей

| Проект | Приоритет | Усилия | Описание |
|--------|-----------|--------|----------|
| DM.Services.Mail.Sender.Tests | P2 | 2 дня | Unit тесты с моками SMTP |
| ~~DM.Services.Notifications.Tests~~ | ~~P2~~ | ~~2 дня~~ | ✅ **28 тестов добавлено** |
| ~~DM.Services.Uploading.Tests~~ | ~~P2~~ | ~~2 дня~~ | ✅ **41 тест добавлен** |
| DM.Services.Search.Tests | P2 | 2 дня | Unit тесты + MongoDB moq |
| ~~DM.Services.MessageQueuing.Tests~~ | ~~P2~~ | ~~1 день~~ | ✅ **13 тестов добавлено** |

---

## Производительность

### Индексы БД

**Приоритет:** P3
**Усилия:** 1 день

| Таблица | Поле | Тип | Обоснование |
|---------|------|-----|-------------|
| Games | Status | Index | Фильтрация по статусу (Active, Finished, etc.) |
| Games | CreateDate | Index | Сортировка по дате создания |
| ForumTopics | CreateDate | Index | Сортировка по дате |
| ForumTopics | LastMessageDate | Index | Сортировка по активности |
| Posts | RoomId | Index | Выборка постов комнаты |
| Posts | CreateDate | Index | Сортировка по дате |
| Comments | EntityId + CommentableType | Composite | Выборка комментариев сущности |
| Comments | CreateDate | Index | Сортировка по дате |
| Likes | EntityId + UserId | Composite + Unique | Проверка существования лайка |
| Messages | ConversationId | Index | Выборка сообщений беседы |
| ChatMessages | CreateDate | Index | Сортировка чата |

**SQL миграция:**
```sql
CREATE INDEX IX_Games_Status ON Games(Status);
CREATE INDEX IX_Games_CreateDate ON Games(CreateDate DESC);
CREATE INDEX IX_ForumTopics_CreateDate ON ForumTopics(CreateDate DESC);
CREATE INDEX IX_ForumTopics_LastMessageDate ON ForumTopics(LastMessageDate DESC);
-- ... и т.д.
```

---

### Кэширование

**Приоритет:** P3
**Усилия:** 3 дня

| Данные | TTL | Invalidation | Обоснование |
|--------|-----|--------------|-------------|
| User profiles | 5 min | При обновлении профиля | Часто читаются, редко меняются |
| Forum moderators | 1 hour | При добавлении/удалении модератора | Почти статичны |
| User ratings | 10 min | При изменении лайков | Пересчитываются не часто |
| Game schemas | 1 hour | При обновлении схемы | Редко меняются |
| Forum list | 5 min | При создании/удалении форума | Почти статичны |

**Реализация:**
```csharp
// IMemoryCache уже используется
// Расширить для дополнительных сущностей
public class UserProfileCache
{
    private readonly IMemoryCache _cache;

    public async Task<UserDetails> GetOrCreate(Guid userId)
    {
        return await _cache.GetOrCreateAsync(
            $"user:{userId}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _repository.GetUserDetails(userId);
            });
    }
}
```

---

### Оптимизация запросов

**Приоритет:** P3
**Усилия:** 2 дня

| Область | Проблема | Решение |
|---------|----------|---------|
| N+1 queries | Загрузка связанных сущностей | EF Core Include/ThenInclude |
| Large payloads | Передача всех полей | DTO с Select для минимизации данных |
| Pagination | Отсутствие в некоторых API | Добавить PagingQuery везде |
| Eager loading | Загрузка ненужных данных | Lazy loading где уместно |

---

## Frontend доработки

### Недостающие страницы

| Страница | Компонент | Статус | Сложность | Усилия |
|----------|-----------|--------|-----------|--------|
| `/game/:id/characters` | CharactersPage.vue | TheLoader | Средняя | 2 дня |
| `/notifications` | NotificationsPage.vue | TheLoader | Средняя | 2 дня |
| `/blogs` | BlogsPage.vue | В разработке | Высокая | 1 неделя |
| `/blog/:id` | BlogPage.vue | Не начато | Средняя | 2 дня |
| `/moderation` | ModerationPage.vue | В разработке | Высокая | 1 неделя |
| `/donate` | DonatePage.vue | TheLoader | Низкая | 1 день |
| `/create-game/characters` | CreateCharactersPage.vue | Не начато | Высокая | 3 дня |
| `/game/:id/settings` | GameSettingsPage.vue | Не начато | Средняя | 2 дня |

---

### UX улучшения

**Приоритет:** P2-P3
**Усилия:** 1-2 недели

| Задача | Компонент | Приоритет | Усилия |
|--------|-----------|-----------|--------|
| SignalR подключение | NotificationsHub | P2 | 2 дня |
| Real-time уведомления | Header notifications badge | P2 | 1 день |
| Toast notifications | ToastService + TheToast.vue | P2 | 1 день |
| Loading states | Skeleton screens | P3 | 2 дня |
| Error boundaries | ErrorBoundary.vue | P2 | 1 день |
| Optimistic updates | Store mutations | P3 | 2 дня |
| Infinite scroll | ThePaging.vue enhancement | P3 | 1 день |
| Image lazy loading | LazyImage.vue | P3 | 1 день |

---

### BBCode редактор улучшения

**Приоритет:** P2
**Усилия:** 1 неделя

| Задача | Приоритет | Усилия | Описание |
|--------|-----------|--------|----------|
| WYSIWYG preview toggle | P2 | 1 день | Переключение между BBCode и preview |
| Таблицы поддержка | P2 | 1 день | [table][tr][td] теги |
| Списки поддержка | P2 | 1 день | [list][*] теги |
| Синтаксис-подсветка для кода | P2 | 1 день | [code=language] с highlight.js |
| Автосохранение | P3 | 1 день | LocalStorage draft saving |
| Mention suggestions | P3 | 2 дня | @username автодополнение |

---

## API консистентность

### HTTP статусы

**Приоритет:** P1-P4
**Усилия:** 1 день

| Endpoint | Текущий | Ожидаемый | Приоритет |
|----------|---------|-----------|-----------|
| `POST /account/password/reset` | 200 | 201 | P1 |
| Multiple DELETE endpoints | 200 | 204 | P4 |
| NotImplementedException endpoints | 500 | Удалить | P2 |

---

### URL структура

**Приоритет:** P4
**Усилия:** 1 неделя

**Проблемы:**
- Непоследовательная плюрализация (game vs games)
- Action verbs в URL (/password/reset вместо PATCH /password)
- Nested resources не всегда RESTful

**Примеры рефакторинга:**
```
// До:
POST /account/password/reset
POST /games/{id}/characters/{characterId}/accept

// После:
PATCH /account/password (в body: reset=true)
PATCH /games/{id}/characters/{characterId} (в body: status=accepted)
```

---

### NotImplementedException endpoints

**Приоритет:** P2
**Усилия:** 1 день

| Controller | Endpoint | Action |
|------------|----------|--------|
| GameController | POST /games/{id}/invites | Удалить или реализовать |
| AttributeSchemaController | GET /schemas/{id} | Удалить или реализовать |

---

## DevOps и инфраструктура

### Логирование

**Приоритет:** P2
**Усилия:** 1 день

| Сервис | Статус | Действие |
|--------|--------|----------|
| AuthenticationService | ✅ Есть ILogger | - |
| Mail.Sender | ❌ Нет | Добавить ILogger |
| Notifications | ❌ Нет | Добавить ILogger |
| Search | ❌ Нет | Добавить ILogger |
| Uploading | ⚠️ Частично | Расширить |

**Добавить:**
- Structured logging с Serilog
- Correlation IDs для distributed tracing
- Performance metrics (request duration, DB queries)
- Error tracking (Sentry)

---

### OAuth провайдеры

**Приоритет:** P4
**Усилия:** 1 неделя

| Провайдер | Статус | Усилия | Условие |
|-----------|--------|--------|---------|
| Discord | ✅ Готово | - | - |
| Google | ❌ Не начато | 2 дня | После стабилизации Discord |
| VK | ❌ Не начато | 2 дня | После Google |
| GitHub | ❌ Не начато | 2 дня | Nice-to-have |

---

### Аналитика

**Приоритет:** P4
**Усилия:** 2 дня

| Задача | Инструмент | Условие |
|--------|------------|---------|
| Privacy-friendly аналитика | Umami | После production deploy |
| User behavior tracking | Posthog | v1.5+ |
| Error tracking | Sentry | v1.5+ |
| Performance monitoring | Application Insights | v2.0+ |

---

### Deployment

**Приоритет:** P4
**Усилия:** 1-2 недели

| Задача | Усилия | Условие |
|--------|--------|---------|
| CI/CD pipeline (GitHub Actions) | 2 дня | После MVP |
| k3s/Helm charts | 1 неделя | При увеличении нагрузки |
| Blue-green deployment | 2 дня | После k3s |
| Automated backups | 1 день | Production |
| Monitoring (Prometheus + Grafana) | 3 дня | Production |
| Alerting (AlertManager) | 1 день | Production |

---

## Технический долг

### Критический долг

| Задача | Файл/Область | Риск | Усилия |
|--------|--------------|------|--------|
| Outbox Pattern не используется | Multiple services | Высокий | 1 день |
| Hardcoded crypto keys | TripleDesSymmetricCryptoService | Средний | 3 часа |
| NotImplementedException | GameController, AttributeSchemaController | Средний | 1 день |

---

### Рефакторинг

| Задача | Файл | Строк | Усилия | Приоритет |
|--------|------|-------|--------|-----------|
| BbParserWrapper | BbParserWrapper.cs | 376 | 2 дня | P1 |
| BBCodeEditor дубликат | BBCodeEditor-REFACTORED.vue | N/A | 1 час | P1 |
| Honeypot validation | AccountController, LoginController | ~20 | 4 часа | P2 |
| DbContext в контроллере | ExternalAuthController | ~20 | 1 день | P3 |

---

### Документация

| Задача | Файл | Приоритет | Усилия |
|--------|------|-----------|--------|
| CHANGELOG.md | docs/ | P3 | 2 часа |
| CONTRIBUTING.md | docs/ | P3 | 1 час |
| LICENSE | root | P3 | 30 мин |
| src/README.md | src/ | P3 | 1 час |
| API documentation (OpenAPI) | Swagger | P2 | 3 дня |

---

## Nice-to-have функции

### Пользовательские функции

| Функция | Усилия | Приоритет | Версия |
|---------|--------|-----------|--------|
| Блоги пользователей | 1 неделя | P2 | v1.5 |
| Групповые чаты | 3 дня | P3 | v1.5 |
| Голосовые сообщения | 1 неделя | P4 | v2.0 |
| Video чаты (WebRTC) | 2 недели | P4 | v2.0 |
| Wiki система | 2 недели | P4 | v2.0 |
| Markdown поддержка | 3 дня | P3 | v2.0 |
| Вложения (файлы) | 1 неделя | P2 | v1.5 |
| Two-factor authentication | 1 неделя | P3 | v2.0 |

---

### Игровые функции

| Функция | Усилия | Приоритет | Версия |
|---------|--------|-----------|--------|
| Real-time игровые комнаты | 2 недели | P3 | v2.0 |
| Карты и токены | 2 недели | P3 | v2.0 |
| Голосовые чаты для игр | 1 неделя | P4 | v2.0 |
| Интеграция с Roll20 | 2 недели | P4 | v3.0 |
| Интеграция с D&D Beyond | 2 недели | P4 | v3.0 |
| Plugin система | 3 недели | P4 | v3.0 |
| Marketplace для контента | 1 месяц | P4 | v3.0 |

---

### Технические функции

| Функция | Усилия | Приоритет | Версия |
|---------|--------|-----------|--------|
| Public API для разработчиков | 2 недели | P4 | v3.0 |
| Elasticsearch интеграция | 1 неделя | P4 | v2.0 |
| GraphQL API | 2 недели | P4 | v2.0 |
| WebSocket API | 1 неделя | P3 | v2.0 |
| Mobile SDK | 1 месяц | P4 | v3.0 |
| Мобильное приложение | 2 месяца | P3 | v2.0 |

---

## Удалено из плана

### Ложные срабатывания аудита

| Задача | Причина удаления | Статус |
|--------|------------------|--------|
| Docker passwords | Local dev конфигурация, задокументировано в SETUP.md | ❌ Не проблема |
| Mail error handling | Уже есть Polly middleware + DLX | ✅ Уже решено |
| Gaming tests | Заявлено 2 теста, фактически 18 | ✅ Уже есть |
| Discord OAuth security | Уже использует state + PKCE | ✅ Уже решено |

---

### Неактуальные задачи

| Задача | Причина удаления |
|--------|------------------|
| Миграция с Stylus на Sass | ✅ Уже выполнено |
| Vue 2 → Vue 3 миграция | ✅ Уже выполнено |
| Webpack → Vite миграция | ✅ Уже выполнено |
| OpenIddict интеграция | ✅ Уже выполнено |

---

## Оценка усилий

### По приоритетам

| Приоритет | Задач | Усилия (дни) | Усилия (недели) |
|-----------|-------|--------------|-----------------|
| P0 | 1 | 1 | 0.2 |
| P1 | 6 | 8 | 1.6 |
| P2 | 8 | 35 | 7.0 |
| P3 | 9 | 15 | 3.0 |
| P4 | 5 | 20 | 4.0 |
| **Итого** | **29** | **79** | **15.8** |

---

### По категориям

| Категория | Задач | Усилия (недели) |
|-----------|-------|-----------------|
| Качество кода | 7 | 2.0 |
| Тестирование | 10 | 5.0 |
| Производительность | 3 | 1.2 |
| Frontend | 8 | 3.5 |
| API консистентность | 3 | 1.5 |
| DevOps | 6 | 2.6 |

---

## Ссылки

- [Дорожная карта](./ROADMAP.md)
- [Стандарты проекта](../standards/CODE.md)
- [Документация по тестам](../guides/TESTING.md)
