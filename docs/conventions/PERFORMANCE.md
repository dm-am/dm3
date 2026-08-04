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
| **Memory Cache** | Горячие данные (статистика, счетчики) |
| **Query Cache** | EF Core second-level cache (опционально) |

**Важно:** Response Caching НЕ подходит для user-specific данных (unread counts). Используй `Cache-Control: private, no-store` для таких endpoints.

### Fast-path caching для hot read endpoints

Endpoint, который читается на каждой загрузке home page (news, tags, site statistics, popular lists) — кандидат на сервисный кэш в `ICache` с коротким TTL. Правила:

1. **Кэшировать только cache-friendly shape запроса**. Если клиент передал search, фильтры по дате или автора — обойти кэш, не плодить ключи. Ключ шаблон: `{entity}:list:{scope}:{шаблон-параметров}:{accessPolicy}`.
2. **Не кэшировать user-specific поля**. Кэш хранит "общедоступный" слепок; per-user данные (unread counts, own flags) заполняются поверх кэша в каждом запросе.
3. **Полагаться на TTL, не на явную инвалидацию**, когда данные меняются редко и допустима задержка в минуту-две. Phone-book-scale enumeration инвалидации (все take × все accessPolicy) — обычно ошибка; короткий TTL проще и предсказуемее.
4. **accessPolicy — часть ключа**: гости и привилегированные пользователи никогда не разделяют одну запись кэша (правильность).
5. **Fast path должен пропускать дорогие шаги**, которые не нужны для compact read (например, отдельный `SELECT COUNT(*)` для paging metadata, когда клиент рендерит единственный список без номеров страниц).

### Perceived-performance tuning для polling endpoints

Polling-эндпойнты (SiteStatistics, unread counters) — **средство, а не цель**. Правила:

- **Серверный TTL кэша должен превышать интервал polling клиента** — иначе половина запросов идет в БД впустую. Если клиент опрашивает раз в 60 секунд, кэшируй хотя бы на 90 секунд.
- **Интервал polling выводится из UX-требования**, а не удобства. "Статистика сайта" — приблизительные числа, 60s достаточно; unread counter в чате — другое дело.
- **Polling прерывается когда вкладка скрыта** (`document.visibilitychange` → `stopPolling`). Обязательная энергосберегающая практика.

### Decoupling enrichment from critical path

Если UI может отрендериться с compact payload, а extra info (tooltip, advanced stats) доступен через отдельный endpoint — enrichment НЕ должен блокировать first paint. Паттерн:

```ts
// ПЛОХО: критический путь ждет второй запрос
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

Кэш процессный, distributed cache нет. Что из этого следует для кода:

- **Кэш не источник истины** — любое кэшированное значение воспроизводимо из БД.
- **Общего кэша между инстансами не предполагать** — у каждого инстанса свой прогрев и свой TTL.
- Если distributed cache появится — инвалидация через pub/sub между инстансами. Планы по кэшированию — в [ROADMAP.md](../plans/ROADMAP.md).

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

Фильтры, сортировка и пагинация синхронизируются с URL. Наивная реализация — прочитать состояние из `route.query`, поменять одно поле, заменить URL — теряет обновления: второе действие читает URL, который первое еще не успело обновить из-за debounce, и правка первого исчезает. Поэтому состояние меняется только через очередь действий с редьюсером.

Место в структуре: композабл фильтра на сущность — в слое фич, общая dispatch-инфраструктура — в shared-композаблах.

### Ключевые правила

1. **URL — единственный источник истины** (computed от route.query)
2. **Все мутации через dispatch()** — единственная точка изменения состояния
3. **Reducer — чистая функция** — тестируется без router
4. **База для действия — pending-состояние, а не URL** — иначе быстрая серия действий теряет предыдущие
5. **Dispatch-состояние живет вне композабла** — debounce-таймер, pending-состояние и флаг навигации создаются один раз на тип фильтра, а не на экземпляр компонента
6. **Текущий route читать в момент навигации** (`router.currentRoute.value` внутри debounce-колбэка), а не из замыкания
7. **Route NAME, не path** — на path компонент перемонтируется при изменении query
8. **Exhaustiveness check в редьюсере** — забытый case ловится компилятором, а не в рантайме
9. **Debounce URL-обновления** — 50ms, батчит быстрые клики в одну навигацию; значение в именованной константе
10. **Явный return type композабла** — интерфейс, а не вывод типа

### Симптомы нарушения паттерна

| Симптом | Причина |
|---------|---------|
| Потеря фильтров при быстрых кликах | Читали URL вместо pending-состояния |
| Бесконечная загрузка | Race condition между navigations |
| Мерцание UI | Компонент remount из-за path вместо name |

---

## Anti-patterns (Чего избегать)

### Frontend

- Inline arrow functions в templates (`@click="() => fn()"`)
- Deep watchers на больших объектах
- Синхронные вычисления в render
- Index as key в v-for
- Fetching в каждом mount без кэша
- Создание DOM элементов в горячем пути (loop, scroll)
- Множественные watchEffect в singleton composables

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

Переключение темы — **мгновенное**: временный класс `.no-transitions` подавляет все CSS transitions на время смены темы, а forced reflow (`offsetHeight`) гарантирует снап значений за один кадр. Подробности — в [UI_STANDARDS.md](./UI_STANDARDS.md) → "Переключение темы". `transition: all` запрещен (performance overhead и непредсказуемость).

---

## Ссылки

- [Web Vitals](https://web.dev/vitals/)
- [Vue Performance](https://vuejs.org/guide/best-practices/performance.html)
- [EF Core Performance](https://learn.microsoft.com/en-us/ef/core/performance/)
