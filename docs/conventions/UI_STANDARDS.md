# Стандарты UI-компонентов

Единые стандарты для типовых UI-паттернов. Single Source of Truth.

---

## Z-Index Scale

Централизованная шкала z-index для всех overlay-компонентов.

| Уровень | Значение | Компоненты |
|---------|----------|------------|
| Dropdown | 100 | Autocomplete, filter dropdowns |
| Sticky | 200 | Headers, sidebars |
| Drawer scrim | 900 | Мобильный off-canvas drawer — backdrop |
| Drawer | 950 | Мобильный off-canvas drawer — панель |
| Dialog backdrop | 900 | Dialog overlay |
| Dialog | 1000 | Dialog, InputDialog |
| Popover | 1100 | Popovers over dialogs |
| Tooltip | 9000 | Tooltips |
| Toast | 10000 | Notifications |

Drawer держится чуть ниже уровня диалога: диалог, открытый изнутри drawer'а (например, форма входа из гостевого блока), должен перекрывать его.

**Файл:** `src/assets/styles/_ZIndex.sass`

---

## Брейкпоинты

Централизованная шкала breakpoint-токенов для layout-медиазапросов.

| Токен | Значение | Назначение |
|-------|----------|------------|
| `$bp-shell` | 1000px | Порог схлопывания 3-колоночного шелла (сайдбары уходят из потока, появляется бургер + drawer) |
| `$bp-tablet` | 768px | Reflow контентных сеток (сброс колонки) |
| `$bp-mobile` | 600px | Одноколоночная верстка, компактные контролы |
| `$bp-narrow` | 480px | Минимальная поддерживаемая ширина (маленькие телефоны) |

**Файл:** `src/assets/styles/_Breakpoints.sass` (глобально доступен через `additionalData` в `vite.config.ts`, как `Variables`/`Layout`/`Themes`).

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

## Dialog Standards

**Словарь:** модальное окно называется **dialog**. Общий примитив — `Dialog` (`shared/ui/Layout/Dialog.vue`) + `DialogTitle`, конкретные модалки — `*Dialog`. Термины "Modal"/"Lightbox" в именах и копиях не используются. Самокатные оверлеи запрещены (см. [PATTERNS.md](./PATTERNS.md) → "Модалки — общий примитив Dialog").

| Требование | Реализация |
|------------|------------|
| role | `role="dialog"` |
| aria-modal | `aria-modal="true"` |
| Focus trap | Фокус внутри диалога |
| ESC | Закрытие по Escape |
| Click-outside | Закрытие по клику на backdrop |
| Focus restore | Возврат фокуса на trigger |

---

## Мобильный off-canvas drawer

Бургер-меню (`<= $bp-shell`) открывает левый off-canvas drawer — единая точка входа в мобильную навигацию (гостевой блок/приветствие, основные разделы, контекстная панель левого сайдбара).

| Требование | Реализация |
|------------|------------|
| Компонент | `MobileDrawer` (`shared/ui/Drawer`) — self-rolled (Teleport + Transition), не vue-final-modal: скролл блокируется на `.main` (реальный scroll-контейнер приложения — `body` всегда `overflow: hidden`), `lock-scroll` библиотеки этого не умеет |
| Состояние открытости | `useUiStore().isMobileDrawerOpen` (Pinia) — бургер в `Header.vue` и сам drawer читают/пишут через один стор |
| Закрытие | Scrim-клик, крестик, Escape, смена маршрута (`App.vue` следит за `route.path`) |
| z-index | `$z-drawer-scrim` / `$z-drawer` — см. Z-Index Scale |

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

## Button/Control States

| Требование | Реализация |
|------------|------------|
| База и состояния | Относительные оверлеи (`$control-bg-overlay`, `$hover-overlay`), не абсолютные цвета — контрол читается на любом фоне |
| Hover и active | Одинаковая эмфаза (один слой `$hover-overlay`) — открытое меню не спорит с индикаторами выбора |
| Disabled | `opacity: $disabled-opacity`, цвета не меняются |
| Focus | См. "Клавиатура и фокус" |

**Файл:** `src/assets/styles/Inputs.sass`

---

## Pagination Standards

| Требование | Реализация |
|------------|------------|
| Блок "пунктир + пагинация + пунктир" | `PagingWithSeparators`; отступ до соседнего контента — фиксированный `$medium` |
| Вертикальная центровка цифр | Оптическая (по чернилам глифов, не по line box) — компенсация живет в `PagingWithSeparators` |

---

## Навигационные полосы

Навигационные полосы (табы, разделы) едины по всему сайту: компактный левый ряд, разделитель " | " реальными текстовыми узлами (копируется "Общий | Игровые системы | ..."), без тултипов. Пункты — обычные ссылки: $link, hover — $link-hover с подчеркиванием; активный — $link + вес (подчеркивание только на hover). Полоса помещается в одну строку за счет компактности — переносы, клипы и горизонтальные скроллы запрещены; "·" запрещен. Табы, живущие на одной странице (ЛК), выносят активный первым с FLIP-анимацией свапа; полосы-ссылки между страницами держат стабильный порядок.

---

## Ссылки на поверхности тултипа

Внутри тултипов и rich-поповеров ссылки используют `$tooltip-link` / `$tooltip-link-hover`: поверхность тултипа темная в обеих темах, обычный `$link` на ней нечитаем.

---

## Цвет ссылок игр и блогов по статусу

Ссылки-названия игр и блогов раскрашиваются по статусу единообразно везде (сайдбары, таблицы /games и /blogs, профильные таблицы, хлебные крошки постов): **зеленый** ($accent-green) — "новый" модуль (активирован менее 7 дней назад и не закрыт); **приглушенный серый** ($text-muted, до hover) — Closed; остальное (Draft, активные старше 7 дней) — обычный $link. Серый приоритетнее зеленого. Архивные комнаты трактуются как закрытые — тем же приглушенным серым; архивность передается цветом (и, где уместно, тегом "архив"), НИКОГДА суффиксом в самом названии. Текстовые бейджи статуса (GameStatusBadge/BlogStatusBadge) цвета не несут.

---

## Animation Standards

| Тип | Duration | Easing |
|-----|----------|--------|
| Fast (hover) | 0.1s | ease |
| Normal | 0.15s | ease |
| Slow (modals) | 0.3s | ease |

`prefers-reduced-motion` покрывается глобальным правилом в `Reset.sass` (0.01ms duration для всех transitions и animations). Не нужно добавлять media query в каждый компонент.

Все раскрытия/сворачивания контента (спойлеры, NSFW, "показать полностью", аккордеоны, раскрываемые списки) анимируются единообразно плавно — единые токены длительности и кривой из `_Animations.sass`; мгновенные тогглы высоты запрещены.

Любой разворачиваемый КОНТЕНТ обязан работать с глобальной кнопкой "Развернуть/Свернуть все" (реестр expandables); ручной тоггл участника обязан вызывать `notifyExpandableChanged`. Секции с подменяемым контентом строятся ТОЛЬКО на `useExpandableSection` — он дает весь контракт разом (плавность единым темпом, реестр, семантика ручного тоггла, глобальный класс зоны `.expand-zone`); собирать это поведение вручную по частям запрещено. Для v-for-аккордеонов и раскрытий с постоянно смонтированным контентом — CSS-only глобальный `.expand-fold` (grid-rows, тот же темп). Текстовая обрезка — `TruncatedContent` (line-snap ниже).

Границы реестра: ИНСТРУМЕНТАЛЬНЫЕ раскрытия (инлайн-формы создания/редактирования, аккордеоны настроек) анимируются так же (`useAnimatedHeightToggle`/`.expand-fold`), но в реестр НЕ входят — "Развернуть все" читает контент, а не открывает инструменты. Персистентные сайдбар-секции (localStorage) тоже вне реестра, но анимируются едиными токенами. Показ УДАЛЕННОГО контента (модераторские "Показать/Скрыть" у удаленных комментариев/сообщений) — вообще не сворачивание/разворачивание: это осознанное модераторское действие, простой мгновенный свап вне контракта; "Развернуть все" никогда не вскрывает удаленное.

**Файл:** `src/assets/styles/_Animations.sass`

---

## Обрезка текста (line-snap)

Любая обрезка текста по высоте ("показать полностью" и подобные) режется строго МЕЖДУ строками: кламп снапится к низу последней строки, целиком влезающей в бюджет, по ФАКТИЧЕСКИМ line-box'ам контента (замер в рантайме), а не фиксированными px и не предположением о равномерной сетке. Единая line-height у многоблочного контента не гарантируется — межабзацные отступы и разные line-height сдвигают строки с любой предполагаемой сетки, поэтому мерить нужно реальные строки. Полусрезанная последняя строка — дефект.

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
| Тексты ошибок на русском | "Не удалось загрузить …"; сырые `error.title` с сервера пользователю не показываем |
| API-клиент не бросает исключения | Возвращает `{ data, error }` — поле `error` проверяется всегда |
| Stale-while-revalidate | При перезагрузке уже показанные данные не прячутся под skeleton |

---

## Гейтинг гостя

Действия, требующие авторизации, **никогда не делают молчаливый редирект**.

| Сценарий | Паттерн |
|----------|---------|
| CTA-ссылка / подсказка "Войдите" | router-link, добавляющий `action: "login"` в query **текущего** маршрута — модал входа открывается на месте, фильтры и контекст сохраняются |
| Route guard (`requiresAuth`) | Редирект на главную с `?action=login` — гость сразу видит форму входа |
| Микродействия (лайк, голос) | Не рендерить для гостя или disabled + tooltip |
| Счетчики "непрочитанного" | Гостю показываются нейтральные подписи ("Постов: N"), без слова "непрочитанных" |

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

Полная дата-время отображается единообразно: **`DD.MM.YYYY в HH:mm`** (общий util, без локальных копий форматирования). Пустое значение — "—".

**Записанное исключение:** полоса событий глобального чата для ближних дат использует компактный формат без года — `DD.MM` / `DD.MM в HH:mm` (by design: однострочная полоса, экономия места). Везде остальное действует полный формат.

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

### Размещение и единичность

Все skeleton'ы живут в едином месте — `shared/ui/Skeleton`. На одну загружаемую единицу — **один** skeleton-компонент в единственном числе (`ChatMessageSkeleton`, `GamePostSkeleton`, не `*Skeletons`); повторение под список делается через проп `:count`, а не отдельным компонентом. Дубли одного skeleton по разным слоям запрещены.

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
