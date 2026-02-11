# План аудита DM3

## Методология

Двухэтапный аудит:
1. **Горизонтальный срез** — по компонентам (cross-cutting)
2. **Вертикальный срез** — по доменам (Swagger группы)

---

## Этап 1: Горизонтальный срез

Порядок по критичности:

| # | Компонент | Что проверять |
|---|-----------|---------------|
| 1 | **Security** | Аутентификация, авторизация, CSRF, XSS, SQL injection, секреты |
| 2 | **Data Access** | Запросы, индексы, N+1, миграции, консистентность |
| 3 | **API** | Контракты, версионирование, документация, REST conventions |
| 4 | **Business Logic** | Покрытие тестами, валидация, edge cases, бизнес-правила |
| 5 | **Infrastructure** | Docker, CI/CD, мониторинг, логирование, бэкапы |
| 6 | **Frontend** | Структура, state management, производительность, доступность |
| 7 | **Code Quality** | Naming, структура, DRY, SOLID, технический долг |

### 1. Security

- [ ] PBKDF2 итерации (≥600K)
- [ ] Cookie флаги (HttpOnly, Secure, SameSite)
- [ ] Rate limiting на auth endpoints
- [ ] Account lockout после N попыток
- [ ] Security headers (HSTS, CSP, X-Frame-Options)
- [ ] CORS whitelist (не wildcard)
- [ ] Секреты не в git
- [ ] Input validation (SQL, XSS, path traversal)

### 2. Data Access

- [ ] Индексы на часто используемых полях
- [ ] Нет N+1 запросов
- [ ] Soft delete работает корректно
- [ ] Миграции применяются
- [ ] Connection pooling настроен
- [ ] Retry policies для БД

### 3. API

- [ ] REST conventions соблюдаются
- [ ] HTTP статус-коды корректны
- [ ] Swagger документация полная
- [ ] Versioning (v1/) работает
- [ ] Error responses стандартизированы
- [ ] Pagination реализована

### 4. Business Logic

- [ ] Unit тесты написаны
- [ ] Integration тесты написаны
- [ ] Edge cases обработаны
- [ ] Валидация входных данных
- [ ] Транзакционность где нужно

### 5. Infrastructure

- [ ] Docker health checks
- [ ] Resource limits настроены
- [ ] CI/CD pipeline работает
- [ ] Мониторинг (Prometheus/Grafana)
- [ ] Бэкапы настроены
- [ ] Логирование структурировано

### 6. Frontend

- [ ] Компоненты переиспользуемы
- [ ] State management организован
- [ ] Типы для API responses
- [ ] Loading/error states
- [ ] Keyboard navigation

### 7. Code Quality

- [ ] Naming conventions единообразны
- [ ] Нет дублирования кода
- [ ] SOLID соблюдается
- [ ] Nullable reference types включены
- [ ] Warnings минимальны

---

## Этап 2: Вертикальный срез

По Swagger группам:

| # | Домен | Контроллеры |
|---|-------|-------------|
| 1 | **Account** | Login, Registration, Profile, Security |
| 2 | **Forum** | Boards, Topics, Comments |
| 3 | **Game** | Games, Characters, Rooms, Posts |
| 4 | **Community** | Users, Reviews, Polls |
| 5 | **Messaging** | Conversations, Messages |
| 6 | **Moderation** | Bans, Warnings |

Для каждого домена проверить:
- [ ] API контракты
- [ ] Авторизация на endpoints
- [ ] Валидация входных данных
- [ ] Тесты
- [ ] Производительность запросов

---

## Статусы

- ⬜ Не проверено
- ✅ Проверено, ОК
- ⚠️ Проблема найдена
- ❌ Критическая проблема

---

## Ссылки

- [Аутентификация](./architecture/AUTHENTICATION.md) — детали security
- [База данных](./architecture/DATABASE.md) — схема и индексы
- [API Reference](./api/REFERENCE.md) — эндпоинты
- [Стандарты кода](./standards/CODE.md) — правила

---

## Принципы документации

- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
