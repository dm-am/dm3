# Оптимизация OwnGames (Мои игры)

## Текущая ситуация: 6 последовательных запросов

```
GetOwn(userId)                    → 100-500ms  (SQL: 4-5 JOINs)
├─ FillEntityCounters(Comments)   → 50-100ms   (MongoDB aggregation)
├─ FillEntityCounters(Characters) → 50-100ms   (MongoDB aggregation)
├─ GetAvailableRoomIds()          → 50-200ms   (SQL: 4-5 JOINs)
├─ SelectByEntities(UnreadPosts)  → 50-100ms   (MongoDB aggregation)
└─ GetPendingPosts()              → 75-250ms   (SQL: 5-6 JOINs)
                                  ─────────────
                          ИТОГО:  400-1250ms
```

## Найденные проблемы

| Проблема | Влияние | Источник |
|----------|---------|----------|
| **6 последовательных запросов** | КРИТИЧНО | GameReadingService.cs |
| **Нет индексов на UserId, MasterId** | ВЫСОКОЕ | БД PostgreSQL |
| **Нет индексов в MongoDB** | ВЫСОКОЕ | UnreadCounters |
| **Избыточные данные в DTO** | СРЕДНЕЕ | Game.cs (4+ полных User объекта) |
| **3 URL картинок на каждого User** | НИЗКОЕ | GeneralUser.cs |
| **Email в публичном API** | НИЗКОЕ | GeneralUser.cs |

## Ключевое открытие

В кодовой базе уже используется `Task.WhenAll` для параллельных запросов:
- `AuthenticationService.cs:94-98` — 3 параллельных запроса
- `ForumReadingService.cs:64-68` — 2 параллельных запроса к MongoDB

Проблема в том, что `GameReadingService` использует один DbContext для всех запросов.
Нужно разделить запросы на SQL (PostgreSQL) и MongoDB — они могут выполняться параллельно.

---

## План оптимизации

### Фаза 1: Быстрые победы (30-50% ускорение)

#### 1.1. Параллелизация MongoDB запросов

**Файл:** `src/DM.Services.Gaming/BusinessProcesses/Games/Reading/GameReadingService.cs`

```csharp
// Было: последовательно
await unreadCountersRepository.FillEntityCounters(games, userId, g => g.Id, g => g.UnreadCommentsCount);
await unreadCountersRepository.FillEntityCounters(games, userId, g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character);

// Станет: параллельно (MongoDB thread-safe)
await Task.WhenAll(
    unreadCountersRepository.FillEntityCounters(games, userId, g => g.Id, g => g.UnreadCommentsCount),
    unreadCountersRepository.FillEntityCounters(games, userId, g => g.Id, g => g.UnreadCharactersCount, UnreadEntryType.Character)
);
```

#### 1.2. Добавить индексы PostgreSQL

**Создать миграцию:**

```sql
CREATE INDEX IX_Characters_UserId ON "Characters" ("UserId");
CREATE INDEX IX_Readers_UserId ON "Readers" ("UserId");
CREATE INDEX IX_Games_MasterId ON "Games" ("MasterId");
CREATE INDEX IX_Games_AssistantId ON "Games" ("AssistantId");
CREATE INDEX IX_Games_NannyId ON "Games" ("NannyId");
CREATE INDEX IX_RoomClaims_ParticipantId ON "RoomClaims" ("ParticipantId");
CREATE INDEX IX_PendingPosts_RoomId ON "PendingPosts" ("RoomId");
```

#### 1.3. Добавить индексы MongoDB

**Скрипт для MongoDB:**

```javascript
db.UnreadCounters.createIndex({ UserId: 1, EntityId: 1, EntryType: 1, IsRemoved: 1 });
db.UnreadCounters.createIndex({ ParentId: 1, EntryType: 1, IsRemoved: 1 });
db.UnreadCounters.createIndex({ EntityId: 1, EntryType: 1 });
```

---

### Фаза 2: Архитектурные изменения (50-70% ускорение)

#### 2.1. Использовать IDbContextFactory для параллельных SQL запросов

**Файл:** `src/DM.Web.API/Startup.cs`

```csharp
// Добавить регистрацию фабрики
services.AddPooledDbContextFactory<DmDbContext>(options => options
    .UseNpgsql(configuration.GetConnectionString(nameof(ConnectionStrings.Rdb))));
```

**Файл:** `src/DM.Services.Gaming/BusinessProcesses/Games/Reading/GameReadingRepository.cs`

```csharp
public class GameReadingRepository
{
    private readonly IDbContextFactory<DmDbContext> _factory;
    private readonly DmDbContext _dbContext; // для обратной совместимости

    public GameReadingRepository(
        IDbContextFactory<DmDbContext> factory,
        DmDbContext dbContext,
        IMapper mapper)
    {
        _factory = factory;
        _dbContext = dbContext;
        _mapper = mapper;
    }

    public async Task<(IDictionary<Guid, IEnumerable<Guid>> rooms, IEnumerable<PendingPost> pending)>
        GetRoomsAndPendingParallel(IEnumerable<Guid> gameIds, Guid userId)
    {
        var gameIdArray = gameIds.ToArray();

        var roomsTask = Task.Run(async () => {
            await using var ctx = await _factory.CreateDbContextAsync();
            var roomsInGames = await ctx.Rooms
                .Where(AccessibilityFilters.RoomAvailable(userId))
                .Where(r => gameIdArray.Contains(r.GameId))
                .Select(r => new { r.RoomId, r.GameId })
                .ToArrayAsync();
            return roomsInGames
                .GroupBy(g => g.GameId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.RoomId));
        });

        var pendingTask = Task.Run(async () => {
            await using var ctx = await _factory.CreateDbContextAsync();
            return await ctx.Rooms
                .Where(AccessibilityFilters.RoomAvailable(userId))
                .Where(r => gameIdArray.Contains(r.GameId))
                .SelectMany(r => r.PendingPosts)
                .ProjectTo<PendingPost>(_mapper.ConfigurationProvider)
                .ToArrayAsync();
        });

        await Task.WhenAll(roomsTask, pendingTask);
        return (roomsTask.Result, pendingTask.Result);
    }
}
```

#### 2.2. Объединить GetOwn + GetAvailableRoomIds в один запрос

```csharp
// Один запрос вместо двух
public async Task<IEnumerable<GameWithRooms>> GetOwnWithRooms(Guid userId)
{
    return await dbContext.Games
        .Where(AccessibilityFilters.GameAvailable(userId))
        .Where(g => g.Characters.Any(c => !c.IsRemoved && c.Status == CharacterStatus.Active && c.UserId == userId) ||
                    g.Readers.Any(r => r.UserId == userId) ||
                    g.MasterId == userId || g.AssistantId == userId || g.NannyId == userId)
        .Select(g => new GameWithRooms
        {
            Game = _mapper.Map<Game>(g),
            RoomIds = g.Rooms
                .Where(r => !r.IsRemoved && (
                    r.AccessType == RoomAccessType.Open ||
                    g.MasterId == userId || g.AssistantId == userId ||
                    r.RoomClaims.Any(c => c.Character.UserId == userId || c.Reader.UserId == userId)))
                .Select(r => r.RoomId)
                .ToList()
        })
        .ToArrayAsync();
}
```

---

### Фаза 3: Кэширование (дополнительно 20-30%)

#### 3.1. Кэш UnreadCounters (5 минут TTL)

**Файл:** `src/DM.Services.Common/BusinessProcesses/UnreadCounters/UnreadCountersRepository.cs`

```csharp
private readonly IMemoryCache _cache;

public async Task<IDictionary<Guid, int>> SelectByEntitiesCached(
    Guid userId, UnreadEntryType entryType, params Guid[] entityIds)
{
    // Для небольших запросов (до 10 сущностей) используем кэш
    if (entityIds.Length <= 10)
    {
        var result = new Dictionary<Guid, int>();
        var uncachedIds = new List<Guid>();

        foreach (var id in entityIds)
        {
            var key = $"unread:{userId}:{entryType}:{id}";
            if (_cache.TryGetValue(key, out int count))
            {
                result[id] = count;
            }
            else
            {
                uncachedIds.Add(id);
            }
        }

        if (uncachedIds.Count > 0)
        {
            var fetched = await SelectByEntities(userId, entryType, uncachedIds.ToArray());
            foreach (var (id, count) in fetched)
            {
                result[id] = count;
                _cache.Set($"unread:{userId}:{entryType}:{id}", count, TimeSpan.FromMinutes(5));
            }
        }

        return result;
    }

    return await SelectByEntities(userId, entryType, entityIds);
}
```

#### 3.2. Инвалидация при изменениях

```csharp
public async Task Increment(Guid entityId, UnreadEntryType entryType)
{
    // ... существующая логика ...

    // Инвалидация кэша
    // Нужно удалить все ключи для этой сущности (для всех пользователей)
    // Вариант: использовать Redis с pattern delete или отказаться от кэша increment
}
```

---

## Оценка результата

| Фаза | Время до | Время после | Улучшение |
|------|----------|-------------|-----------|
| Текущее | 800ms | - | - |
| Фаза 1 | 800ms | 400-500ms | ~40% |
| Фаза 2 | 500ms | 200-300ms | ~60% |
| Фаза 3 | 300ms | 150-200ms | ~75% |

---

## Рекомендуемый порядок реализации

1. **Фаза 1.1** — Параллелизация MongoDB (1-2 часа, безопасно)
2. **Фаза 1.2** — Индексы PostgreSQL (миграция, 1 час)
3. **Фаза 1.3** — Индексы MongoDB (скрипт, 30 мин)
4. **Фаза 2.1** — IDbContextFactory (3-4 часа, требует рефакторинга DI)
5. **Фаза 2.2** — Объединение запросов (2-3 часа)
6. **Фаза 3** — Кэширование (2-3 часа)

---

## Связанные файлы

- `src/DM.Services.Gaming/BusinessProcesses/Games/Reading/GameReadingService.cs` — основной сервис
- `src/DM.Services.Gaming/BusinessProcesses/Games/Reading/GameReadingRepository.cs` — репозиторий
- `src/DM.Services.Gaming/BusinessProcesses/Games/Reading/IGameReadingRepository.cs` — интерфейс
- `src/DM.Services.Common/BusinessProcesses/UnreadCounters/UnreadCountersRepository.cs` — счётчики непрочитанного
- `src/DM.Services.Gaming/BusinessProcesses/Shared/AccessibilityFilters.cs` — фильтры доступа
- `src/DM.Services.Gaming/Dto/GameProfile.cs` — маппинг AutoMapper
- `src/DM.Web.API/Startup.cs` — регистрация DI
