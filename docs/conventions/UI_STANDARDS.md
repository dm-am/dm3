# Стандарты UI-компонентов

Единые стандарты для типовых UI-паттернов. Single Source of Truth.

---

## Z-Index Scale

Централизованная шкала z-index для всех overlay-компонентов.

| Уровень | Значение | Компоненты |
|---------|----------|------------|
| Dropdown | 100 | Autocomplete, filter dropdowns |
| Sticky | 200 | Headers, sidebars |
| Modal backdrop | 900 | Lightbox overlay |
| Modal | 1000 | Lightbox, InputDialog |
| Popover | 1100 | Popovers over modals |
| Tooltip | 9000 | Tooltips |
| Toast | 10000 | Notifications |

**Файл:** `src/assets/styles/_ZIndex.sass`

---

## Dropdown/Filter Standards

| Параметр | Значение | Обоснование |
|----------|----------|-------------|
| Height (input) | 38px | Visual consistency |
| Min-width | 260px | Readable content |
| Max-height | 300px | Scrollable, не занимает весь экран |
| Avatar size | 24px | Consistent in lists |
| Debounce (search) | 300ms | Balance UX/performance |
| Debounce (URL sync) | 50ms | Batch rapid clicks |

**Файл:** `src/assets/styles/_Filters.sass`

---

## Table UX Standards

| Требование | Реализация |
|------------|------------|
| Row hover | `background-color: $bg-element-hover` + transition 0.15s |
| Empty state | `EmptyState` или `SecondaryText` компонент |
| Loading state | `DataTableSkeleton` — только когда данных еще нет |
| Перезагрузка | Уже показанные строки остаются видимыми (`aria-busy="true"` на таблице), skeleton их не заменяет |
| Сортировка | `aria-sort` на заголовке; первая сортировка колонки может задавать естественное направление (`defaultDirection`) |
| Virtual scroll | >50 элементов |

**Файл:** `src/assets/styles/_Tables.sass`

---

## Modal/Lightbox Standards

| Требование | Реализация |
|------------|------------|
| role | `role="dialog"` |
| aria-modal | `aria-modal="true"` |
| Focus trap | Фокус внутри модалки |
| ESC | Закрытие по Escape |
| Click-outside | Закрытие по клику на backdrop |
| Focus restore | Возврат фокуса на trigger |

---

## Autocomplete/Listbox Standards

| Требование | Реализация |
|------------|------------|
| Input role | `role="combobox"` |
| aria-expanded | Состояние открытости |
| aria-controls | Ссылка на listbox ID |
| Listbox role | `role="listbox"` |
| Option role | `role="option"` |
| Arrow keys | Navigation |
| Enter | Selection |
| Escape | Close |

---

## Animation Standards

| Тип | Duration | Easing |
|-----|----------|--------|
| Fast (hover) | 0.1s | ease |
| Normal | 0.15s | ease |
| Slow (modals) | 0.3s | ease |

`prefers-reduced-motion` покрывается глобальным правилом в `Reset.sass` (0.01ms duration для всех transitions и animations). Не нужно добавлять media query в каждый компонент.

**Файл:** `src/assets/styles/_Animations.sass`

---

## Переключение темы

Переключение темы — **мгновенное**. CSS custom properties snap между значениями light/dark.

**Механизм** (`App.vue`): на время смены класса `theme_Light`/`theme_Dark` на `<html>` добавляется класс `.no-transitions`, блокирующий все CSS transitions через `!important`. Forced reflow (`offsetHeight`) гарантирует, что все значения обновятся за один кадр. После reflow transitions разблокируются.

**CSS** (`ThemeVariables.css`):
```css
html.no-transitions,
html.no-transitions *,
html.no-transitions *::before,
html.no-transitions *::after {
  transition: none !important;
}
```

**JS** (`App.vue`):
```ts
html.classList.add("no-transitions");
// ... swap theme classes ...
void html.offsetHeight; // Force reflow
html.classList.remove("no-transitions");
```

**Правила:**
1. **`transition: all` запрещен.** Всегда перечислять свойства явно (performance, predictability).
2. Per-component hover/focus/active transitions на цветовые свойства **допустимы** — `.no-transitions` автоматически подавляет их при смене темы.
3. Для стандартных transition — `+transition-safe()` / `+transition-safe-multi()` из `_Animations.sass`.
4. Любой новый компонент с `transition:` автоматически покрывается `.no-transitions` — никакого opt-in не нужно.

---

## Avatars

Используйте компонент `<AvatarImg>` (из `@/entities/user`) — он сам решает,
какой URL грузить из `picture` объекта и формирует srcset для DPR-aware
загрузки на retina.

```vue
<AvatarImg :picture="user.picture" :alt="user.username" :size="48" />
<AvatarImg :picture="user.picture" :alt="user.username" :size="220" prefer-original eager />
```

Сервер отдает 3 URL в `picture: { smallUrl, mediumUrl, originalUrl }`:
- `smallUrl` — 100×100 (через imgproxy, AVIF/WebP auto)
- `mediumUrl` — 400×400 (через imgproxy)
- `originalUrl` — ≤1024 px aspect-preserving (прямой MinIO link)

Подробнее: [UPLOADS.md](../architecture/UPLOADS.md).

---

## Состояния данных

Каждое представление данных обязано иметь **четыре состояния**: загрузка (skeleton) → ошибка → пусто → контент.

| Правило | Пояснение |
|---------|-----------|
| Ошибка ≠ пусто | При ошибке запроса показывается сообщение об ошибке, а не empty state |
| Нет вечных skeleton | Skeleton живет только пока идет загрузка; при ошибке сменяется сообщением |
| Тексты ошибок на русском | «Не удалось загрузить …»; сырые `error.title` с сервера пользователю не показываем |
| API-клиент не бросает исключения | Возвращает `{ data, error }` — поле `error` проверяется всегда |
| Stale-while-revalidate | При перезагрузке уже показанные данные не прячутся под skeleton |

---

## Гейтинг гостя

Действия, требующие авторизации, **никогда не делают молчаливый редирект**.

| Сценарий | Паттерн |
|----------|---------|
| CTA-ссылка / подсказка «Войдите» | router-link, добавляющий `action: "login"` в query **текущего** маршрута — модал входа открывается на месте, фильтры и контекст сохраняются |
| Route guard (`requiresAuth`) | Редирект на главную с `?action=login` — гость сразу видит форму входа |
| Микродействия (лайк, голос) | Не рендерить для гостя или disabled + tooltip |
| Счетчики «непрочитанного» | Гостю показываются нейтральные подписи («Постов: N»), без слова «непрочитанных» |

Поддерживаемые значения query: `action=login | register | recovery`.

---

## Клавиатура и фокус

| Требование | Реализация |
|------------|------------|
| Видимый фокус | Глобальный `:focus-visible` (outline) — компоненты его не подавляют |
| Кликабельные действия | `<button>`, а не `<a>` без `href` |
| Tooltips | Показываются и по фокусу (focusin/focusout), не только по hover |
| Декоративные иконки и разделители | `aria-hidden="true"` |
| Иконки-кнопки и числовые ссылки | Обязательный `aria-label` |

---

## Формат даты-времени

Полная дата-время отображается единообразно: **`DD.MM.YYYY в HH:mm`** (общий util, без локальных копий форматирования). Пустое значение — «—».

---

## Empty State Standards

### Two-State Pattern

При наличии фильтров используем два разных сообщения:

| Состояние | Шаблон | Пример |
|-----------|--------|--------|
| Нет данных вообще | `{Genitive} пока нет` | "Игр пока нет" |
| Нет по фильтрам | `{Genitive} по заданным фильтрам не найдено` | "Игр по заданным фильтрам не найдено" |

**Код:**
```vue
{{ hasActiveFilters ? '{Items} по заданным фильтрам не найдено' : '{Items} пока нет' }}
```

### Компоненты

| Тип | Компонент | Когда |
|-----|-----------|-------|
| Таблица/список | `SecondaryText` | Простые списки |
| С иконкой | `EmptyState` | Центральный контент |
| Inline (dropdown) | `SecondaryText` | Внутри dropdown |

### Без фильтров

Если фильтров нет — только одно сообщение: `{Items} пока нет`

---

## Loading State Standards

### Skeleton-first подход

Предпочитаем skeleton'ы для улучшения perceived performance.

| Контекст | Компонент | Когда |
|----------|-----------|-------|
| Сайдбары | `SidebarSkeleton` | Списки ссылок |
| DataTables | `DataTableSkeleton` | Табличные данные |
| Профиль | `ProfileSkeleton` | Header профиля |
| Кнопки | `:loading` prop | Actions |
| Inline | `...` | Краткие индикаторы |

### Когда использовать текст

Текст `"Загрузка..."` допустим когда:
- Контент переменной высоты (комментарии)
- Нельзя предсказать layout
- В модальных окнах

### Shimmer animation

Все skeleton'ы используют единую shimmer-анимацию:
- Duration: 1.5s
- Easing: ease-in-out / infinite
- Background gradient: `$bg-element-hover` → `$bg-element`

**SASS mixin:** `+skeleton-shimmer` в `_Skeleton.sass`

---

## Ссылки

- [PATTERNS.md](PATTERNS.md) — Feature-Sliced Design структура
- [CODE_STYLE.md](CODE_STYLE.md) — Conventions для кода
- [PERFORMANCE.md](PERFORMANCE.md) — Debounce и оптимизация
