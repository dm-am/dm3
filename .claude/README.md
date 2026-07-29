# Claude Code Configuration — DM3

## Агенты

Агенты вызываются через **Task tool**. Claude автоматически выбирает агента по контексту, но можно указать явно.

---

## code-reviewer

Проверка кода на security, RBAC, API standards, Clean Architecture.

### Примеры вызова

**Проверить папку:**
```
Task(
  subagent_type: "code-reviewer",
  prompt: "Review src/DM.Domain.Forum/Features/Topics/"
)
```

**Проверить файл:**
```
Task(
  subagent_type: "code-reviewer",
  prompt: "Review src/DM.Web.API/Features/Forum/TopicController.cs"
)
```

**Проверить последний коммит:**
```
Task(
  subagent_type: "code-reviewer",
  prompt: "Review changes in git diff HEAD~1"
)
```

**Проверить staged изменения перед коммитом:**
```
Task(
  subagent_type: "code-reviewer",
  prompt: "Review staged changes: git diff --staged"
)
```

**Фокус на безопасности:**
```
Task(
  subagent_type: "code-reviewer",
  prompt: "Security review: check AuthenticationService.cs for auth bypasses and token handling"
)
```

**Фокус на API:**
```
Task(
  subagent_type: "code-reviewer",
  prompt: "API review: verify TopicController.cs follows standards from docs/reference/standards.md"
)
```

### Пример вывода

```
## Code Review: TopicController.cs, TopicService.cs

### CRITICAL (1 issue)
- [ ] Missing [AuthenticationRequired] on POST /topics [TopicController.cs:42]

### HIGH (2 issues)
- [ ] No FluentValidation on CreateTopicRequest [TopicController.cs:45]
- [ ] N+1 query: loading comments without Include() [TopicService.cs:78]

### MEDIUM (1 issue)
- [ ] Function GetTopicWithComments() is 65 lines, consider splitting [TopicService.cs:50-115]

### LOW (0 issues)

---
**Verdict:** BLOCK

**Summary:** Critical auth issue must be fixed before merge. Add [AuthenticationRequired] attribute.
```

### В разговоре

```
Пользователь: Проверь код в Features/Topics/

Claude: Запускаю code-reviewer...
[Вызывает Task tool]
[Показывает отчёт]
```

---

## debugger

Анализ ошибок, тест-фейлов, проблем производительности.

### Примеры вызова

**Ошибка в тесте:**
```
Task(
  subagent_type: "debugger",
  prompt: "Test failure: TopicCreatingServiceShould.CreateTopic_ValidData
           Error: System.NullReferenceException at TopicCreatingService.cs:34"
)
```

**Ошибка из логов:**
```
Task(
  subagent_type: "debugger",
  prompt: "API error from logs:
           InvalidOperationException: Sequence contains no elements
           at TopicReadingService.GetByIdAsync() line 45"
)
```

**500 ошибка:**
```
Task(
  subagent_type: "debugger",
  prompt: "POST /v1/account/login returns 500 Internal Server Error.
           Started happening after last commit.
           Check API logs and find root cause."
)
```

**Проблема производительности:**
```
Task(
  subagent_type: "debugger",
  prompt: "Performance issue: GET /v1/games takes 3 seconds.
           Expected: <500ms.
           Investigate database queries and N+1 issues."
)
```

**Проблема с Docker:**
```
Task(
  subagent_type: "debugger",
  prompt: "Container dm-api keeps restarting.
           Check docker logs and find why."
)
```

**Проблема с MongoDB:**
```
Task(
  subagent_type: "debugger",
  prompt: "Sessions not saving to MongoDB.
           User logs in but session disappears.
           Check connection and collection."
)
```

### Пример вывода

```
## Debug Report: NullReferenceException in TopicCreatingService

### Symptoms
- Error: System.NullReferenceException
- Location: TopicCreatingService.cs:34
- Test: TopicCreatingServiceShould.CreateTopic_ValidData

### Investigation
1. Checked TopicCreatingService.cs:34
   - Line: `var board = await _boardRepository.GetByIdAsync(request.BoardId);`
   - Next line: `board.TopicsCount++` — assumes board exists

2. Checked test setup
   - Mock returns null for GetByIdAsync()
   - Missing setup: `_boardRepository.Setup(x => x.GetByIdAsync(...)).ReturnsAsync(testBoard)`

3. Checked production code
   - No null check before accessing board properties

### Root Cause
Test mock not configured + production code missing null check.

### Fix
1. Add null check in TopicCreatingService:
   ```csharp
   var board = await _boardRepository.GetByIdAsync(request.BoardId, ct);
   if (board is null)
       throw new EntityNotFoundException<Board>(request.BoardId);
   ```

2. Fix test mock:
   ```csharp
   _boardRepository
       .Setup(x => x.GetByIdAsync(boardId, It.IsAny<CancellationToken>()))
       .ReturnsAsync(testBoard);
   ```

### Validation
- [ ] Test passes
- [ ] Other board-related tests still pass
- [ ] Manual test: create topic works
```

### В разговоре

```
Пользователь: Тест падает с NullReferenceException, разберись

Claude: Запускаю debugger...
[Вызывает Task tool]
[Показывает отчёт с root cause и fix]
```

---

## frontend-developer

Vue 3 разработка — компоненты, stores, composables, API интеграция.

### Примеры вызова

**Создать компонент:**
```
Task(
  subagent_type: "frontend-developer",
  prompt: "Create TopicList.vue component:
           - Displays list of forum topics
           - Shows title, author, date, comment count
           - Pagination with load more button
           - Loading and empty states"
)
```

**Создать страницу:**
```
Task(
  subagent_type: "frontend-developer",
  prompt: "Create GamePage.vue:
           - Route: /games/:id
           - Fetch game data from API
           - Show game info, rooms, characters
           - Tabs for different sections"
)
```

**Создать Pinia store:**
```
Task(
  subagent_type: "frontend-developer",
  prompt: "Create useGamesStore:
           - State: games list, current game, loading, error
           - Actions: fetchGames, fetchGame, createGame
           - Getters: activeGames, myGames"
)
```

**Создать composable:**
```
Task(
  subagent_type: "frontend-developer",
  prompt: "Create useCharacters composable:
           - Fetch characters for game
           - Filter by status (active, pending, retired)
           - Reactive character list"
)
```

**Добавить API интеграцию:**
```
Task(
  subagent_type: "frontend-developer",
  prompt: "Add API integration for game characters:
           - GET /v1/games/{id}/characters
           - POST /v1/games/{id}/characters
           - Types in api/models/game/character.ts
           - API client methods"
)
```

**Исправить баг:**
```
Task(
  subagent_type: "frontend-developer",
  prompt: "Fix bug: Login form shows 'undefined' instead of error message.
           API returns { errors: [{ message: '...' }] }
           but form shows 'undefined'.
           Check error handling in LoginForm.vue"
)
```

**Добавить фичу:**
```
Task(
  subagent_type: "frontend-developer",
  prompt: "Add infinite scroll to TopicList:
           - Load more topics on scroll
           - Show loading indicator
           - Handle end of list"
)
```

### Пример вывода

```
## Frontend Task: Create TopicList.vue

### Files Created

**src/components/forum/TopicList.vue**
```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useTopics } from '@/composables/useTopics';
import TopicCard from './TopicCard.vue';
import LoadingSpinner from '@/components/common/LoadingSpinner.vue';

const props = defineProps<{
  boardId: string;
}>();

const { topics, loading, error, loadMore, hasMore } = useTopics(props.boardId);

onMounted(() => loadMore());
</script>

<template>
  <div class="topic-list">
    <LoadingSpinner v-if="loading && !topics.length" />

    <div v-else-if="error" class="error">
      {{ error }}
    </div>

    <template v-else>
      <TopicCard
        v-for="topic in topics"
        :key="topic.id"
        :topic="topic"
      />

      <button
        v-if="hasMore"
        @click="loadMore"
        :disabled="loading"
        class="load-more"
      >
        {{ loading ? 'Загрузка...' : 'Загрузить ещё' }}
      </button>

      <div v-if="!topics.length" class="empty">
        Нет тем
      </div>
    </template>
  </div>
</template>
```

**src/composables/useTopics.ts**
```typescript
import { ref, Ref } from 'vue';
import { api } from '@/api/client';
import type { Topic } from '@/api/models/forum';

export function useTopics(boardId: string) {
  const topics: Ref<Topic[]> = ref([]);
  const loading = ref(false);
  const error = ref<string | null>(null);
  const hasMore = ref(true);
  const skip = ref(0);
  const take = 20;

  async function loadMore() {
    if (loading.value || !hasMore.value) return;

    loading.value = true;
    error.value = null;

    try {
      const response = await api.get(`/v1/boards/${boardId}/topics`, {
        params: { skip: skip.value, take }
      });

      topics.value.push(...response.data.resources);
      skip.value += take;
      hasMore.value = response.data.resources.length === take;
    } catch (e) {
      error.value = 'Не удалось загрузить темы';
    } finally {
      loading.value = false;
    }
  }

  return { topics, loading, error, hasMore, loadMore };
}
```

### Summary
Created TopicList component with pagination, loading states, and useTopics composable.
```

### В разговоре

```
Пользователь: Создай компонент для списка персонажей игры

Claude: Запускаю frontend-developer...
[Вызывает Task tool]
[Создаёт компонент, composable, типы]
```

---

## Комбинирование агентов

### Пример: Фича + Review

```
1. Пользователь: Добавь эндпоинт GET /v1/games/{id}/statistics

2. Claude реализует эндпоинт...

3. Пользователь: Проверь

4. Claude:
   Task(
     subagent_type: "code-reviewer",
     prompt: "Review GameStatisticsController.cs and GameStatisticsService.cs"
   )

5. Code-reviewer находит issues → Claude исправляет → Review снова → APPROVE
```

### Пример: Баг + Debug + Fix

```
1. Пользователь: Тест TopicServiceShould.GetTopic падает

2. Claude:
   Task(
     subagent_type: "debugger",
     prompt: "Investigate TopicServiceShould.GetTopic test failure"
   )

3. Debugger находит root cause

4. Claude применяет fix

5. Claude запускает тест для проверки
```

### Пример: Frontend + API

```
1. Пользователь: Нужна страница со статистикой игры

2. Claude:
   Task(
     subagent_type: "frontend-developer",
     prompt: "Create GameStatisticsPage.vue with charts for player activity"
   )

3. Frontend-developer создаёт компонент

4. Claude проверяет что API endpoint существует, если нет — создаёт
```

---

## Hooks (автоматическая защита)

Hooks срабатывают **автоматически**. Не нужно вызывать вручную.

### block-dangerous-git.sh

**Что блокирует:**
```bash
git checkout         # BLOCKED
git reset --hard     # BLOCKED
git clean -fd        # BLOCKED
git stash drop       # BLOCKED
```

**Что разрешено:**
```bash
git stash            # OK
git stash list       # OK
git stash pop        # OK
git status           # OK
git diff             # OK
git add/commit/push  # OK
```

### block-new-migrations.sh

**Что блокирует:**
```bash
# Создание новых миграций
Write → src/DM.Infrastructure.Persistence/Migrations/20250309_AddSomething.cs  # BLOCKED
```

**Что разрешено:**
```bash
# Редактирование существующей
Edit → src/DM.Infrastructure.Persistence/Migrations/InitialCreate.cs  # OK
```
