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
| Loading state | `LoadingSpinner` компонент |
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

**Обязательно:** `prefers-reduced-motion: reduce` → `transition: none`

**Файл:** `src/assets/styles/_Animations.sass`

---

## Переключение темы (View Transition API)

Переключение темы использует **View Transition API** (`document.startViewTransition()`) в `App.vue`. API захватывает скриншот текущего состояния страницы, применяет class swap на `<html>`, захватывает новое состояние, и cross-fade'ит между двумя bitmap'ами за 0.3s. Всё — цвета, картинки, `filter:invert()` на хэдере/футере — переходит синхронно как единое целое.

**Почему не CSS transitions:** хэдер и футер переключают тему через `filter: $filter-invert` (CSS custom property). Нерегистрированные custom properties — строки, их нельзя интерполировать. `filter: invert()` при transition проходит через уродливые промежуточные состояния (серая каша). View Transition API решает это на уровне рендеринга: оба состояния — bitmap'ы, cross-fade между ними всегда чистый.

**Fallback:** если View Transition API недоступен — мгновенный swap (graceful degradation).

**CSS (Reset.sass):**
```sass
::view-transition-old(root),
::view-transition-new(root)
  animation-duration: 0.3s

@media (prefers-reduced-motion: reduce)
  ::view-transition-old(root),
  ::view-transition-new(root)
    animation-duration: 0s
```

**Два механизма, дополняющих друг друга:**

1. **View Transition API** (`document.startViewTransition()` в App.vue) — bitmap cross-fade всей страницы при смене темы. Всё (цвета, картинки, filter:invert) переходит атомарно.
2. **Глобальное правило** `*, *::before, *::after { transition: background-color, color, border-color, fill, stroke, box-shadow 0.3s }` в `Reset.sass` — плавные hover/focus/active цветовые переходы на интерактивных элементах (кнопки, строки таблиц, dropdown items). Не для темы, а для UX.

**Правила:**
1. **`transition: all` запрещён.** Всегда перечислять свойства явно (performance, predictability).
2. **Не ставить `transition` на theme-dependent цветовые свойства в base state** — глобальное правило уже покрывает их для hover/state переходов.
3. Для non-theme свойств (`opacity`, `transform`, `width`, `height`, `max-height`) — `+transition-safe()` / `+transition-safe-multi()` из `_Animations.sass`.

---

## Avatar Sizes

| Контекст | Размер |
|----------|--------|
| Inline (списки, dropdowns) | 24px |
| Preview (chat preview) | 48px |
| Profile (header) | 96px |

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
