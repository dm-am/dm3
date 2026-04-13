# Performance Optimization Standards

Стандарты и принципы оптимизации производительности для DM3.

## Принципы

### 1. Measure First (Сначала измерь)

- **Не оптимизируй без метрик** — сначала профилируй, потом оптимизируй
- **Устанавливай baseline** — фиксируй текущие показатели перед изменениями
- **A/B тестирование** — сравнивай before/after с реальными данными

### 2. User-Perceived Performance (Воспринимаемая производительность)

- **Time to First Byte (TTFB)** < 200ms
- **First Contentful Paint (FCP)** < 1.5s
- **Time to Interactive (TTI)** < 3s
- **Cumulative Layout Shift (CLS)** < 0.1

### 3. Optimize the Critical Path (Критический путь)

- Сначала оптимизируй то, что видит пользователь
- Отложи загрузку того, что ниже viewport
- Приоритизируй интерактивность над полнотой

---

## Frontend

### Fetching данных

| Практика | Описание |
|----------|----------|
| **Дедупликация запросов** | Один endpoint — один запрос за сессию (до инвалидации) |
| **Кэширование в store** | Данные с TTL, stale-while-revalidate pattern |
| **Батчинг запросов** | Группировка нескольких запросов в один |
| **Prefetch** | Предзагрузка данных для вероятных переходов |
| **Debounce/Throttle** | Поиск: 300ms debounce, scroll: 100ms throttle |

### Рендеринг Vue

| Практика | Описание |
|----------|----------|
| **v-once** | Для статического контента |
| **v-memo** | Мемоизация тяжелых поддеревьев |
| **Правильные key** | Уникальные ID, не индексы массива |
| **shallowRef** | Для больших объектов без глубокой реактивности |
| **computed vs methods** | Computed для кэшированных вычислений |
| **Lazy components** | defineAsyncComponent для тяжелых компонентов |
| **Singleton resources** | Переиспользование дорогих объектов (canvas, workers) |

### Списки

| Практика | Описание |
|----------|----------|
| **Виртуальный скролл** | Для списков > 50 элементов |
| **Пагинация** | Серверная пагинация с разумным размером (20-50) |
| **Skeleton** | Placeholder во время загрузки |

### State Management

| Практика | Описание |
|----------|----------|
| **Нормализация** | Плоская структура, ID-based references |
| **Гранулярность** | Мелкие реактивные единицы вместо крупных объектов |
| **Селекторы** | Computed для выборки из store |
| **Инвалидация** | Явные правила когда данные устаревают |

### Bundle Size

| Практика | Описание |
|----------|----------|
| **Tree shaking** | Именованные импорты, не default |
| **Code splitting** | Route-based splitting через dynamic import() в router |
| **Lazy modals** | vue-final-modal загружает компоненты только при открытии |
| **Анализ бандла** | Регулярный аудит с vite-bundle-analyzer |

---

## Backend

### База данных (EF Core)

| Практика | Описание |
|----------|----------|
| **AsNoTracking** | Для read-only запросов |
| **Projection** | Select только нужные поля |
| **Include стратегия** | Минимум Include, избегать глубокой вложенности |
| **Split Queries** | AsSplitQuery для множественных коллекций |
| **Индексы** | На все WHERE, ORDER BY, JOIN поля |
| **Compiled Queries** | Для частых запросов с параметрами |

### N+1 Prevention

```
ПЛОХО:
foreach (var game in games)
    game.Tags = await GetTags(game.Id);

ХОРОШО:
var games = await context.Games
    .Include(g => g.Tags)
    .ToListAsync();
```

### Batch Population

| Практика | Описание |
|----------|----------|
| **Avoid inline aggregations** | COUNT/SUM в ProjectTo генерируют N subqueries |
| **Use .Ignore()** | Помечай проблемные поля, заполняй отдельным запросом |
| **GROUP BY batch** | Один запрос с группировкой вместо N отдельных |
| **Dictionary lookup** | O(1) заполнение в памяти после batch-загрузки |

### Parallel Queries

| Практика | Описание |
|----------|----------|
| **Task.WhenAll** | Независимые запросы параллельно |
| **Avoid for dependent** | Если B зависит от A — последовательно |
| **Single DbContext** | EF Core не thread-safe, не шарить контекст |

### Кэширование

| Уровень | Применение |
|---------|------------|
| **Response Compression** | Brotli/Gzip middleware (Fastest level для TTFB) |
| **Response Cache** | `[ResponseCache(Duration=N)]` для статических справочников |
| **Distributed Cache** | Сессии, частые запросы |
| **Memory Cache** | Горячие данные (статистика, счетчики) |
| **Query Cache** | EF Core second-level cache (опционально) |

**Важно:** Response Caching НЕ подходит для user-specific данных (unread counts). Используй `Cache-Control: private, no-store` для таких endpoints.

### Fast-path caching для hot read endpoints

Endpoint, который читается на каждой загрузке home page (news, tags, site statistics, popular lists) — кандидат на сервисный кэш в `ICache` с коротким TTL. Правила:

1. **Кэшировать только cache-friendly shape запроса**. Если клиент передал search, фильтры по дате или автора — обойти кэш, не плодить ключи. Ключ шаблон: `{entity}:list:{scope}:{шаблон-параметров}:{accessPolicy}`.
2. **Не кэшировать user-specific поля**. Кэш хранит «общедоступный» слепок; per-user данные (unread counts, own flags) заполняются поверх кэша в каждом запросе.
3. **Полагаться на TTL, не на явную инвалидацию**, когда данные меняются редко и допустима задержка в минуту-две. Phone-book-scale enumeration инвалидации (все take × все accessPolicy) — обычно ошибка; короткий TTL проще и предсказуемее.
4. **accessPolicy — часть ключа**: гости и привилегированные пользователи никогда не разделяют одну запись кэша (правильность).
5. **Fast path должен пропускать дорогие шаги**, которые не нужны для compact read (например, отдельный `SELECT COUNT(*)` для paging metadata, когда клиент рендерит единственный список без номеров страниц).

### Perceived-performance tuning для polling endpoints

Polling-эндпойнты (SiteStatistics, unread counters) — **средство, а не цель**. Правила:

- **Серверный TTL кэша должен превышать интервал polling клиента** — иначе половина запросов идёт в БД впустую. Если клиент опрашивает раз в 60 секунд, кэшируй хотя бы на 90 секунд.
- **Интервал polling выводится из UX-требования**, а не удобства. «Статистика сайта» — приблизительные числа, 60s достаточно; unread counter в чате — другое дело.
- **Polling прерывается когда вкладка скрыта** (`document.visibilitychange` → `stopPolling`). Обязательная энергосберегающая практика.

### Decoupling enrichment from critical path

Если UI может отрендериться с compact payload, а extra info (tooltip, advanced stats) доступен через отдельный endpoint — enrichment НЕ должен блокировать first paint. Паттерн:

```ts
// ПЛОХО: критический путь ждёт второй запрос
bestOfWeek.value = (await fetchRatedPosts()).resources[0];
bestOfWeekGame.value = await enrichGame(bestOfWeek.value); // +1 roundtrip
loaded.value = true; // Only fires after both complete

// ХОРОШО: compact виден сразу, enrichment догоняет в фоне
bestOfWeek.value = (await fetchRatedPosts()).resources[0];
loaded.value = true; // First paint unblocked
void enrichGame(bestOfWeek.value).then(g => { bestOfWeekGame.value = g; });
```

Ключевое свойство: UI **корректно отрисовывается БЕЗ** enriched-поля, оно только дополняет tooltip/additional details. Если enrichment обязателен для корректности — это не fire-and-forget кейс, а serial dependency.

### API Design

| Практика | Описание |
|----------|----------|
| **Пагинация** | Cursor-based для бесконечного скролла, offset для страниц |
| **Lightweight Projections** | `Ref` DTOs для списков (только необходимые поля, counts вместо arrays) |
| **Compression** | Brotli/Gzip для всех JSON ответов (Startup.cs: AddResponseCompression) |
| **Response Caching** | `[ResponseCache]` для статических справочников (tags, boards) |
| **ETags** | Conditional requests для кэшируемых данных |

### Async Patterns

| Практика | Описание |
|----------|----------|
| **ConfigureAwait(false)** | В библиотечном коде |
| **ValueTask** | Для методов часто возвращающих синхронно |
| **Parallel.ForEachAsync** | Для независимых операций |
| **Channel** | Для producer-consumer сценариев |

---

## Infrastructure

### Database

- **Connection pooling** — настроен корректный размер пула
- **Query timeout** — разумные таймауты (30s default, 5s для UI)
- **Read replicas** — для тяжелых read-only запросов (будущее)

### Caching Layer

- **Redis** — для distributed cache
- **Инвалидация** — pub/sub для синхронизации между инстансами

### CDN

- **Static assets** — JS, CSS, images через CDN
- **Cache headers** — immutable для versioned assets

---

## Monitoring

### Метрики

| Метрика | Цель |
|---------|------|
| **P95 Response Time** | < 500ms |
| **Error Rate** | < 0.1% |
| **Throughput** | Requests per second baseline |
| **Database Query Time** | P95 < 100ms |

### Alerts

- Response time P95 > 1s
- Error rate > 1%
- Memory usage > 80%
- CPU usage > 70% sustained

---

## Singleton Resources

Паттерн для ресурсов, которые дорого создавать, но можно переиспользовать:

```typescript
// ПЛОХО: Canvas создается при каждом вызове
function measureText(text: string, font: string): number {
  const canvas = document.createElement("canvas");
  const ctx = canvas.getContext("2d")!;
  ctx.font = font;
  return ctx.measureText(text).width;
}

// ХОРОШО: Singleton canvas переиспользуется
let measureCanvas: HTMLCanvasElement | null = null;
let measureCtx: CanvasRenderingContext2D | null = null;

function measureText(text: string, font: string): number {
  if (!measureCanvas) {
    measureCanvas = document.createElement("canvas");
    measureCtx = measureCanvas.getContext("2d");
  }
  if (!measureCtx) return 0;
  measureCtx.font = font;
  return measureCtx.measureText(text).width;
}
```

Применение:
- Canvas для измерения текста (tooltips, layout)
- Web Workers для фоновых вычислений
- Audio/Video contexts

---

## URL-Synced Filters: Action Queue Pattern

Паттерн для composables с URL-синхронизацией (фильтры, сортировка, пагинация).

### Архитектура

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           ACTION QUEUE PATTERN                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  setSearch("та") ──┐                                                         │
│                    ├──► dispatch() ──► reducer() ──► pendingState            │
│  setStatus("Active")                                                         │
│                    │                                                         │
│                    └──► [debounce 50ms] ──► router.replace() ──► URL         │
│                                                                              │
│  URL ◄─────────────────────────────────────────────────────────────────────  │
│   │                                                                          │
│   └──► computed filterState ◄── parseQueryToState(route.query)               │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Проблема: Lost Updates

```typescript
// ПЛОХО: Race condition при быстрых действиях
function setSearch(search: string) {
  const newState = { ...filterState.value };  // ❌ Читает URL (stale!)
  newState.search = search;
  updateUrl(newState);  // Ставит pendingState, debounce 50ms
}

function setStatus(status: string) {
  const newState = { ...filterState.value };  // ❌ URL ещё не обновился!
  newState.status = status;                   // Потеряли search="та"
  updateUrl(newState);
}

// Пользователь вводит "та", кликает статус → search="" (потерян!)
```

### Решение: Action Queue + Reducer

```typescript
// =============================================================================
// CONSTANTS
// =============================================================================

/** Debounce delay for URL updates. Batches rapid clicks into single navigation. */
const DEBOUNCE_MS = 50;

// =============================================================================
// RETURN TYPE (explicit interface for better DX)
// =============================================================================

export interface FilterComposable {
  filterState: ComputedRef<FilterState>;
  searchParams: ComputedRef<SearchParams>;
  hasActiveFilters: ComputedRef<boolean>;
  setSearch: (search: string) => void;
  setStatus: (status: StatusValue | null) => void;
  // ... other actions
}

// =============================================================================
// ACTION TYPES (Discriminated Union)
// =============================================================================

type FilterAction =
  | { type: "SET_SEARCH"; search: string }
  | { type: "SET_STATUS"; status: StatusValue | null }
  | { type: "ADD_TAG"; tagId: number }
  | { type: "REMOVE_TAG"; tagId: number }
  | { type: "SET_SORT"; sortBy: string; sortOrder?: "asc" | "desc" }
  | { type: "CLEAR_FILTERS" };

// =============================================================================
// PURE REDUCER (Testable, no side effects)
// =============================================================================

function reducer(state: FilterState, action: FilterAction): FilterState {
  const newState: FilterState = {
    ...state,
    tags: new Set(state.tags),  // ✅ Новые Set для immutability
  };

  switch (action.type) {
    case "SET_SEARCH":
      newState.search = action.search;
      return newState;

    case "SET_STATUS":
      // Сбрасываем под-фильтры при смене статуса
      if (newState.status !== action.status) {
        newState.recruitmentFilter = "any";
      }
      newState.status = action.status;
      return newState;

    case "ADD_TAG":
      newState.tags.add(action.tagId);
      return newState;

    case "REMOVE_TAG":
      newState.tags.delete(action.tagId);
      return newState;

    case "SET_SORT": {
      newState.sortBy = action.sortBy;
      newState.sortOrder = action.sortOrder ?? getDefaultSortOrder(action.sortBy);
      return newState;
    }

    case "CLEAR_FILTERS":
      return createDefaultState();

    default: {
      // ✅ Exhaustiveness check — TypeScript ошибка если забыли case
      const _exhaustive: never = action;
      return _exhaustive;
    }
  }
}

// =============================================================================
// MODULE-LEVEL DISPATCH INFRASTRUCTURE
// =============================================================================

let debounceTimer: ReturnType<typeof setTimeout> | null = null;
let pendingState: FilterState | null = null;
let isNavigating = false;
let routerInstance: ReturnType<typeof useRouter> | null = null;

/**
 * Dispatch an action - applies it to pending state and schedules URL update.
 * This is the ONLY way to modify filter state.
 */
function dispatch(action: FilterAction, getCurrentState: () => FilterState) {
  // ✅ Читаем pendingState если есть, иначе URL
  const baseState = pendingState ?? getCurrentState();
  const newState = reducer(baseState, action);

  // ✅ Reducer может вернуть тот же объект (оптимизация)
  if (newState === baseState) return;

  pendingState = newState;

  if (debounceTimer) clearTimeout(debounceTimer);

  debounceTimer = setTimeout(async () => {
    debounceTimer = null;
    if (!pendingState || !routerInstance) return;
    if (isNavigating) return;

    const query = buildQueryFromState(pendingState);
    pendingState = null;

    // ✅ router.currentRoute.value — не closure!
    const currentRoute = routerInstance.currentRoute.value;

    // Skip if unchanged
    if (queriesEqual(query, currentRoute.query)) return;

    isNavigating = true;
    try {
      // ✅ route NAME, не path — Vue Router распознаёт тот же route
      await routerInstance.replace({ name: currentRoute.name as string, query });
    } catch (err: unknown) {
      const error = err as { name?: string };
      if (error?.name !== "NavigationDuplicated" && error?.name !== "NavigationCancelled") {
        console.error("[useFilter] Navigation error:", err);
      }
    } finally {
      isNavigating = false;
    }
  }, DEBOUNCE_MS);
}

// =============================================================================
// COMPOSABLE
// =============================================================================

export function useFilter(): FilterComposable {
  const route = useRoute();
  const router = useRouter();

  routerInstance = router;

  // ✅ URL — единственный источник истины
  const filterState = computed<FilterState>(() => parseQueryToState(route.query));

  const getCurrentState = () => filterState.value;

  // ✅ Тонкие обёртки — просто создают action и dispatch
  const setSearch = (search: string) =>
    dispatch({ type: "SET_SEARCH", search }, getCurrentState);

  const setStatus = (status: StatusValue | null) =>
    dispatch({ type: "SET_STATUS", status }, getCurrentState);

  const addTag = (tagId: number) =>
    dispatch({ type: "ADD_TAG", tagId }, getCurrentState);

  const setSort = (sortBy: string, sortOrder?: "asc" | "desc") =>
    dispatch({ type: "SET_SORT", sortBy, sortOrder }, getCurrentState);

  const clearFilters = () =>
    dispatch({ type: "CLEAR_FILTERS" }, getCurrentState);

  return {
    filterState,
    setSearch,
    setStatus,
    addTag,
    setSort,
    clearFilters,
  };
}
```

### Преимущества Action Queue

| Аспект | getBaseState() | Action Queue |
|--------|----------------|--------------|
| Правильность | Можно забыть вызвать | Невозможно пропустить |
| Тестируемость | Нужен router mock | Reducer — чистая функция |
| TypeScript | Ручная проверка | Exhaustiveness check |
| Расширяемость | Дублирование логики | Один switch |
| Отладка | Сложно | Actions можно логировать |

### Ключевые правила

1. **URL — единственный источник истины** (computed от route.query)
2. **Все мутации через dispatch()** — единственная точка изменения состояния
3. **Reducer — чистая функция** — тестируется без router
4. **Module-level state** — debounceTimer, pendingState, isNavigating НА УРОВНЕ МОДУЛЯ
5. **router.currentRoute.value** — внутри setTimeout для избежания stale closures
6. **Route NAME, не path** — `router.replace({ name, query })`
7. **Exhaustiveness check** — `default: { const _: never = action; return _; }`
8. **Константы** — `const DEBOUNCE_MS = 50` вместо magic numbers
9. **Explicit return types** — `export interface FilterComposable { ... }`

### Симптомы нарушения паттерна

| Симптом | Причина |
|---------|---------|
| Потеря фильтров при быстрых кликах | Читали URL вместо pendingState |
| Бесконечная загрузка | Race condition между navigations |
| Мерцание UI | Компонент remount из-за path вместо name |
| vue-i18n `__disposer` TypeError | `legacy: false` не установлен |

### vue-i18n конфигурация

```typescript
// plugins.ts
export const i18n = createI18n({
  locale: "ru",
  legacy: false, // ✅ ОБЯЗАТЕЛЬНО! Composition API mode
});
```
Без `legacy: false` возникает `__disposer` TypeError при быстром unmount.

---

## Anti-patterns (Чего избегать)

### Frontend

- Inline arrow functions в templates (`@click="() => fn()"`)
- Deep watchers на больших объектах
- Синхронные вычисления в render
- Index as key в v-for
- Fetching в каждом mount без кэша
- Создание DOM элементов в горячем пути (loop, scroll)
- Множественные watchEffect в singleton composables (см. выше)

### Backend

- N+1 запросы
- SELECT * вместо projection
- Tracking entities при read-only
- Синхронные блокирующие вызовы
- Большие payload без пагинации

---

## BBCode render caching

Результаты серверного рендеринга BBCode кэшируются по композитному ключу **`(sourceHash, audience, permissionBucket)`**. Permission bucket — это класс эквивалентности зрителей: все, кто попадает в один бакет для одного исходного текста, получают байт-в-байт идентичный HTML и разделяют одну запись кэша.

**Принципы:**
- Контент без privacy-sensitive тегов бакетируется грубо (`anon` / `user`) — максимальный hit-rate.
- Контент с `[private]` / `[mod]` получает бакет, отражающий **причину** видимости (автор, адресат, lead, per-room, per-post). Идентичные причины → одна запись кэша на многих зрителей.
- Инвалидация — по `sourceHash` при редактировании источника (вторичный индекс `sourceHash → keys`).
- Защита от cache-stampede — per-source-hash семафор: конкурентные миссы на один источник сериализуются в один rendering call.

Конкретные TTL и бюджеты памяти — это параметры, не конвенции; они настраиваются по метрикам. Контракт ключей и правил фильтрации — в [BBCODE_RENDERING.md](../architecture/BBCODE_RENDERING.md).

---

## Checklist перед релизом

- [ ] Профилирование ключевых страниц (Lighthouse)
- [ ] Проверка bundle size (< 500KB initial)
- [ ] Проверка API response times (< 200ms P50)
- [ ] Проверка database query plans
- [ ] Load testing (целевая нагрузка × 2)
- [ ] Memory leak testing (long session)

---

## CSS Transitions и тема

Переключение темы анимируется через **View Transition API** (`document.startViewTransition()` в `App.vue`), а не через CSS transitions. Подробности — в [UI_STANDARDS.md](./UI_STANDARDS.md) → «Переключение темы». `transition: all` запрещен (performance overhead и непредсказуемость).

---

## Ссылки

- [Web Vitals](https://web.dev/vitals/)
- [Vue Performance](https://vuejs.org/guide/best-practices/performance.html)
- [EF Core Performance](https://learn.microsoft.com/en-us/ef/core/performance/)
