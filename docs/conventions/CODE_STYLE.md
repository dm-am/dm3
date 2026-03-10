# Стандарты разработки DM3

> **Принцип:** Код > документация. Паттерны смотреть в коде.
>
> **Архитектура:** [PATTERNS.md](./PATTERNS.md)

---

## Стандарты кода

### Именование

| Элемент | Конвенция | Пример |
|---------|-----------|--------|
| Приватные поля | `_camelCase` | `_repository` |
| Async методы | суффикс `Async` | `CreateAsync()` |
| Тестовый класс | `{Class}Should.cs` | `TopicServiceShould.cs` |
| Repository | `I{Feature}Repository.cs` | `ITopicRepository.cs` |
| AutoMapper | `{Feature}MappingProfile.cs` | `TopicMappingProfile.cs` |

### Валидация

- **FluentValidation** — бизнес-правила
- **DataAnnotations** — простые ограничения

### Логирование

**Добавлять:** Authentication, business operations, external integrations

**НЕ добавлять:** Repositories, Factories, Validators

### Тестирование

> [TESTING.md](../guides/TESTING.md)

### Безопасность

> [SECURITY.md](./SECURITY.md)

### Чеклисты

#### Новая фича

- [ ] Feature folder — [PATTERNS.md](./PATTERNS.md#webapi--feature-folders)
- [ ] Service + Repository + Intention + Validator + MappingProfile
- [ ] DI регистрация

#### Новый endpoint

- [ ] XML documentation
- [ ] `[ProducesResponseType]`
- [ ] `[AuthenticationRequired]` если нужно

#### Перед коммитом

- [ ] `dotnet build`
- [ ] `npm run type-check`

---

## Стандарты API

> **Подход:** Pragmatic REST (Stripe, GitHub)

### Философия

1. **Понятность > Догма**
2. **Консистентность**
3. **Предсказуемость**
4. **Простота**

### Три типа endpoints

| Тип | HTTP | Глаголы в URL |
|-----|------|---------------|
| **Resource** | GET/POST/PATCH/DELETE | Нет |
| **Query** | GET | Допустимы (`check-email`, `can-join`) |
| **Action** | POST | Допустимы (`register`, `join`, `ban`) |

### Response Format

**Одиночный ресурс** — напрямую
**Коллекция** — `ListEnvelope` с `paging`
**Ошибка** — `ErrorEnvelope`

### Pagination

**Offset:** `?skip=20&take=10`
**Cursor:** `?cursor=abc&limit=50`

---

## Ссылки

- [Паттерны](./PATTERNS.md)
- [API Design](./API_DESIGN.md)
- [Тестирование](../guides/TESTING.md)
