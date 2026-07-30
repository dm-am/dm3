# Стандарты разработки DM3

> **Принцип:** Код > документация. Паттерны смотреть в коде.
>
> **Архитектура:** [PATTERNS.md](./PATTERNS.md)

---

## Русский текст

Правила действуют везде, где текст виден человеку: строки интерфейса, сообщения
об ошибках, стартовые данные, комментарии, документация, описания в OpenAPI.

- **Буква "е с двумя точками" (U+0451) не используется.** Всегда обычная "е".
  Проверяется поиском по этому символу: находки в новом тексте недопустимы.
- **Кавычки прямые** — `"..."`. Типографские "елочки" зарезервированы за одним
  местом: девиз на странице "О сайте". Правило распространяется и на комментарии
  в коде, и на стартовые данные.
- **Без тире-заменителей смысла.** Формулировка, которая держится на длинном тире
  вместо связки, обычно скрывает, что мысль не додумана.

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

### Именование Vue-компонентов

- **Однословное имя** допустимо **только** для примитивов UI-kit (`shared/ui`): `Button`, `Form`, `Select`, `Dialog`, `Icon`. Это generic-строительные блоки без домена.
- **Доменный компонент — всегда многословный:** контекст + сущность (`GameCard`, `TestimonialCard`, `CharacterSheet`). Одно доменное слово (`Poll`, `Topic`, `Comment` как имя компонента) запрещено — не читается на месте импорта.
- **Единственные-в-приложении layout-синглтоны** именуются голым существительным без артикля и без суффикса: `Header`, `Footer`, `Sidebar`, `ScrollNav`. Не `TheHeader`, не `MainHeader`, не `HeaderComponent`.

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

**Дата и время** — всегда с литералом "в" между датой и временем. В dayjs литерал экранируется квадратными скобками: `DD.MM.YYYY [в] HH:mm`. Применяется ко всем tooltip'ам и UI-меткам, показывающим created / modified / deleted / lastActivity / отправлено / отредактировано / любые другие timestamp'ы — везде где рендерится дата+время одним полем.

**День рождения** — **без года** (privacy: скрываем возраст) и с **месяцем прописью по-русски**. Не `DD.MM` (цифры) и не `DD.MM.YYYY` (полная дата). Используется только для поля "День рождения" в профиле пользователя.

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

### Dev-only код

Код, нужный только для локальной разработки, не должен полагаться на окружение как на защиту.

- **Окружение — не граница авторизации.** `IsDevelopment()` это признак конфигурации, а не право. Поверхность, недопустимая в production, несет требование роли в дополнение к проверке окружения, чтобы ошибка в переменной окружения не была единственным, что отделяет анонима от действия.
- **Гейт закрывается, а не открывается.** Проверка формулируется как "разрешено только если Development", и по умолчанию поверхность недоступна.
- **Недостижимость проверяется тестом.** На каждый dev-only endpoint есть тест, что вне Development он не отвечает. Иначе гейт молча ломается при рефакторинге.
- **Генерация тестовых данных не живет в поставляемой сборке.** Сиды — отдельный инструмент, подключаемый локальной оснасткой, а не код внутри API.

### Prose-поля и `v-html`

> Полностью: [BBCODE_RENDERING.md](../architecture/BBCODE_RENDERING.md#инвариант-prose-полей)

- Пользовательское prose-поле, видимое другим — **только** подтип `BbText` (сервер рендерит и экранирует). Plain-string prose-поле, долетающее до `v-html`, — баг stored XSS.
- Клиентский `v-html` sink принимает лишь server-rendered HTML от `BbText` либо экранированный `highlightMatch()`. Plain-text по замыслу (заголовки, имена, причины модерации, приватные заметки) — через `{{ }}`.
- Новое prose-поле по умолчанию берет подтип `BbText`, зеркалящий ближайшее существующее поле.

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

> Единый список гейтов живет в [TESTING.md](../guides/TESTING.md); прогоняет их хук `scripts/hooks/pre-push`.

---

## Стандарты API

> [API_DESIGN.md](./API_DESIGN.md) — типы endpoint'ов, форматы ответов, коды, пагинация.

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

**Централизованная библиотека** — иконки интерфейса лежат в одном реестре `shared/lib/utils/icons.ts`, а не в компонентах:

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
5. **Иконки, чей ключ хранится в базе** (каталоги наград и достижений) — в отдельный реестр, парный бэкендному каталогу-валидатору; в общую библиотеку интерфейса их не класть

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

**Skeleton-first.** Предпочитаем скелетоны, повторяющие layout будущего контента. SSOT: [UI_STANDARDS.md](./UI_STANDARDS.md) → "Loading State Standards" (там же — когда допустим текст "Загрузка...").

### НЕ делай

- ❌ CSS-спиннеры / анимированные индикаторы
- ❌ Компоненты типа `LoadingSpinner`
- ❌ Иконки загрузки

---

## Frontend: Sass

- Собственные декларации правила — до include миксинов с вложенными правилами; переопределения свойств миксина — после include, обернутые в блок `&` (иначе mixed-decls deprecation).

---

## Frontend: CSS Transitions

- **`transition: all` запрещен.** Всегда перечислять свойства явно (performance и predictability).
- Переключение темы — **мгновенный snap** CSS-переменных (без анимации, через временную блокировку transitions). Подробности: [UI_STANDARDS.md](./UI_STANDARDS.md) → "Переключение темы".
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
