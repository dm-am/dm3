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
| **Даты/время** | суффикс `Utc` | `CreatedUtc`, `ActivatedUtc`, `ExpiresUtc` |
| Тестовый класс | `{Class}Should.cs` | `TopicServiceShould.cs` |
| Repository | `I{Feature}Repository.cs` | `ITopicRepository.cs` |
| AutoMapper | `{Feature}MappingProfile.cs` | `TopicMappingProfile.cs` |

### Унифицированные имена между слоями

**Цель:** одинаковые имена свойств в Entity → Domain DTO → API DTO → Frontend.

| Категория | Правильно | Неправильно |
|-----------|-----------|-------------|
| Идентификатор пользователя | `Username` | `Login`, `UserLogin` |
| Собственный ID сущности | `Id` | `NoteId`, `TicketId` |
| Дата изменения | `ModifiedUtc` | `UpdatedUtc`, `EditedUtc`, `LastUpdateUtc` |
| Счетчики (множественное) | `LoginsCount`, `SharedIpsCount` | `LoginCount`, `SharedIpCount` |
| Автор | `AuthorUsername`, `Author` | `AuthorLogin` |
| Мастер игры | `MasterUsername`, `Master` | `MasterLogin` |

**Принцип:** Если имена совпадают между слоями, AutoMapper маппит автоматически (convention-based mapping).

### Даты и время

**Все поля типа `DateTime`, `DateTimeOffset` и `string` (для дат)** должны иметь суффикс `Utc`:

| Слой | Тип | Паттерн |
|------|-----|---------|
| Entity (DB) | `DateTime` | `SomethingUtc` |
| Domain DTO | `DateTimeOffset` | `SomethingUtc` |
| API DTO | `DateTimeOffset` | `SomethingUtc` |
| Frontend | `string` | `somethingUtc` (camelCase) |

**Примеры:**
- `CreatedUtc`, `ModifiedUtc`, `DeletedUtc`
- `StartsUtc`, `EndsUtc`, `ExpiresUtc`
- `ActivatedUtc`, `ClosedUtc`, `JoinedUtc`
- `TimestampUtc`, `LastReadUtc`, `LastActivityUtc`

**НЕ допускается:** `createdAt`, `startDate`, `expirationDate`, `timestamp`, `SomethingAtUtc`, `SomethingDateUtc`

#### Формат отображения дат

| Вариант | Формат | Пример |
|---------|--------|--------|
| Только дата | `DD.MM.YYYY` | `14.03.2026` |
| Дата и время | `DD.MM.YYYY [в] HH:mm` | `22.02.2026 в 12:45` |
| День рождения | `D MMMM` | `14 марта` |

**Локаль:** `ru-RU` (день.месяц.год)

**Дата и время** — всегда с литералом «в» между датой и временем. В dayjs литерал экранируется квадратными скобками: `DD.MM.YYYY [в] HH:mm`. Применяется ко всем tooltip'ам и UI-меткам, показывающим created / modified / deleted / lastActivity / отправлено / отредактировано / любые другие timestamp'ы — везде где рендерится дата+время одним полем.

**День рождения** — **без года** (privacy: скрываем возраст) и с **месяцем прописью по-русски**. Не `DD.MM` (цифры) и не `DD.MM.YYYY` (полная дата). Используется только для поля «День рождения» в профиле пользователя.

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

## Frontend: Контекстные подсказки

### Типы подсказок

| Паттерн | Когда появляется | Когда использовать |
|---------|------------------|-------------------|
| **Inline Hint** | Всегда видна | Требования к полям, форматы ввода |
| **Tooltip** | Hover / Focus | Дополнительная информация, пояснения |
| **Popover** | Click | Расширенная справка с интерактивом |

### Inline Hints (FormField #hint)

```vue
<FormField label="Пароль" name="password">
  <input v-model="password" type="password" />
  <template #hint>
    Минимум 8 символов, латиница и цифры
  </template>
</FormField>
```

**Используй для:** критической информации, которую пользователь должен знать ДО ввода.

### Tooltips

```vue
<Tooltip text="Непрочитанные посты">
  <span class="counter">{{ count }}</span>
</Tooltip>
```

**Используй для:** дополнительной информации при наведении (статусы, даты, сокращения).

**WCAG требования** (1.4.13):
- Появляется на hover И focus
- Закрывается по ESC
- Остается при наведении на сам tooltip

**НЕ используй** native `title` атрибут — он не доступен для клавиатуры/touch.

---

## Frontend: Иконки

### Архитектура

**Централизованная библиотека** — все SVG иконки в `shared/lib/utils/icons.ts`:

```typescript
// shared/lib/utils/icons.ts
export const icons: Record<string, IconDefinition> = {
  pencil: {
    viewBox: "-0.7 -1.2 25.4 25.4",
    path: '<path d="M12 20h9M16.5 3.5..." />',
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 2,
  },
};

export type IconName = keyof typeof icons;
```

### Использование

```vue
<script setup>
import { SvgIcon } from "@/shared/ui/Icon";
</script>

<template>
  <SvgIcon name="pencil" class="action-icon" />
</template>
```

### Категории иконок

| Категория | Примеры | Стиль |
|-----------|---------|-------|
| Действия | `pencil`, `trash`, `close` | Stroke 2px |
| Навигация | `chevronUp`, `arrowBack`, `scrollDown` | Filled |
| Статусы | `checkmark`, `warning`, `error` | Stroke |
| Соцсети | `vk`, `discord`, `youtube` | Filled (брендовые) |

### Правила добавления

1. **Проверь существующие** — возможно, иконка уже есть
2. **Добавь в icons.ts** с JSDoc комментарием
3. **Используй currentColor** — для наследования цвета
4. **Оптимизируй viewBox** — минимальные размеры

### НЕ делай

- ❌ Inline SVG в компонентах
- ❌ Icon fonts (Font Awesome, etc.)
- ❌ Отдельные .svg файлы как компоненты
- ❌ Дублирование path в разных местах

### Почему централизованная библиотека

| Подход | Bundle | DX | Переиспользование |
|--------|--------|----|--------------------|
| Inline SVG | Дубли | Копипаста | ❌ |
| Icon fonts | 60KB+ | Хорошо | ✅ |
| SVG sprites | Оптимально | Сложно | ✅ |
| **icons.ts** | Оптимально | Просто | ✅ |

**Trade-off:** Проект средний (~40 иконок), поэтому:
- Не нужен tree-shaking (unplugin-icons)
- Не нужен build pipeline (vite-svg-loader)
- Достаточно простого объекта с типизацией

---

## Frontend: Toast-уведомления

### Принципы

1. **Контрастность WCAG AA** — минимум 4.5:1 для текста
2. **Семантические цвета** — dedicated CSS variables, не общие accent-переменные
3. **Консистентность между темами** — одинаковый UX в light/dark mode

### CSS Variables

```css
/* ThemeVariables.css */
--toast-success-bg: #2e7d32;   /* Material Green 800 */
--toast-success-text: #fff;
--toast-success-border: #1b5e20;

--toast-error-bg: #c62828;     /* Material Red 800 */
--toast-error-text: #fff;
--toast-error-border: #b71c1c;

--toast-warning-bg: #f9a825;   /* Material Yellow 800 */
--toast-warning-text: #1a1a1a; /* Dark text on yellow! */
--toast-warning-border: #f57f17;

--toast-info-bg: #1565c0;      /* Material Blue 800 */
--toast-info-text: #fff;
--toast-info-border: #0d47a1;
```

### Визуальная иерархия

| Тип | Использование |
|-----|---------------|
| `success` | Успешное действие (сохранено, отправлено) |
| `error` | Ошибки, требующие внимания пользователя |
| `warning` | Предупреждения, потенциальные проблемы |
| `info` | Нейтральная информация |

### НЕ делай

- ❌ `$accent-green` для toast-success — в dark theme контраст 1:1
- ❌ `$text-on-green` — это цвет для текста НА зеленом фоне интерфейса
- ❌ Разные цвета для light/dark без проверки контрастности

---

## Frontend: Индикаторы загрузки

### Принцип

**Skeleton-first.** Предпочитаем скелетоны, повторяющие layout будущего контента. SSOT: [UI_STANDARDS.md](./UI_STANDARDS.md) → «Loading State Standards» (там же — когда допустим текст «Загрузка...»).

### НЕ делай

- ❌ CSS-спиннеры / анимированные индикаторы
- ❌ Компоненты типа `LoadingSpinner`
- ❌ Иконки загрузки

---

## Frontend: CSS Transitions

- **`transition: all` запрещен.** Всегда перечислять свойства явно (performance и predictability).
- Переключение темы — **мгновенный snap** CSS-переменных (без анимации, через временную блокировку transitions). Подробности: [UI_STANDARDS.md](./UI_STANDARDS.md) → «Переключение темы».
- Для hover/expand/collapse — использовать миксины `+transition-safe()` / `+transition-safe-multi()` из `_Animations.sass`, которые уважают `prefers-reduced-motion`.

---

## Frontend: безопасный `v-html`

`v-html` разрешен **только** на полях, которые сервер уже отрендерил как HTML (поля, сериализованные из BbText DTO). Раз-сырой пользовательский текст биндить через `v-html` **запрещено** — это XSS-vector и, для BBCode-контента, обход permission-фильтрации.

Клиентский BBCode-renderer существует **только как внутренний инструмент редактора** для переключения между режимами WYSIWYG и raw BBCode. Любой другой импорт клиентского BBCode-рендерера — ошибка. Display-компоненты (чат, комментарии, посты, топики, профили) должны получать готовый HTML от сервера.

Контракт рендеринга — в [BBCODE_RENDERING.md](../architecture/BBCODE_RENDERING.md).

---

## Ссылки

- [Паттерны](./PATTERNS.md)
- [API Design](./API_DESIGN.md)
- [BBCODE_RENDERING.md](../architecture/BBCODE_RENDERING.md) — контракт рендеринга
- [Тестирование](../guides/TESTING.md)
