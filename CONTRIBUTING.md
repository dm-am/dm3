# Руководство по участию в разработке

> **Метаданные:**
> - Документ: CONTRIBUTING.md
> - Версия: 1.0
> - Дата создания: 2026-01-27
> - Последнее обновление: 2026-01-27

Добро пожаловать! Мы рады, что вы хотите внести вклад в проект DM3.

---

## Содержание

- [Кодекс поведения](#кодекс-поведения)
- [С чего начать](#с-чего-начать)
- [Процесс разработки](#процесс-разработки)
- [Стандарты кода](#стандарты-кода)
- [Требования к коммитам](#требования-к-коммитам)
- [Требования к тестам](#требования-к-тестам)
- [Документация](#документация)
- [Code Review](#code-review)
- [Коммуникация](#коммуникация)

---

## Кодекс поведения

Участвуя в проекте, вы соглашаетесь соблюдать следующие принципы:

- **Уважение**: относитесь к другим участникам с уважением
- **Конструктивность**: критика должна быть конструктивной и обоснованной
- **Открытость**: будьте открыты к обратной связи
- **Профессионализм**: соблюдайте профессиональную этику

Недопустимо:
- Оскорбительные комментарии
- Личные нападки
- Троллинг
- Публикация приватной информации других людей
- Любое поведение, которое можно считать домогательством

---

## С чего начать

### 1. Ознакомьтесь с проектом

**Обязательно прочитайте:**
- [README.md](./README.md) - общее описание проекта
- [docs/SETUP.md](./docs/SETUP.md) - установка окружения
- [docs/architecture/OVERVIEW.md](./docs/architecture/OVERVIEW.md) - архитектура системы
- [docs/PROJECT_STANDARDS.md](./docs/PROJECT_STANDARDS.md) - стандарты разработки
- [docs/tasks/CURRENT.md](./docs/tasks/CURRENT.md) - текущие задачи

### 2. Настройте окружение разработки

```bash
# 1. Клонируйте репозиторий
git clone https://github.com/quilin/dm.git
cd dm

# 2. Настройте окружение согласно docs/SETUP.md
# - Docker Desktop
# - Node.js 20+
# - .NET 8 SDK

# 3. Запустите инфраструктуру
cd docker
docker-compose up -d

# 4. Запустите frontend
cd ../frontend/DM.Web.Modern
yarn install
yarn dev

# 5. Запустите backend (в Visual Studio / Rider / VS Code)
# Или через командную строку:
cd ../../src/DM.Web.API
dotnet run
```

### 3. Найдите задачу для работы

**Хорошие задачи для начала:**
- Поищите Issues с меткой `good-first-issue` в GitHub
- Проверьте [docs/tasks/BACKLOG.md](./docs/tasks/BACKLOG.md) - задачи с низким приоритетом (P3-P4)
- Спросите в Discord канале #dev-chat

**Перед началом работы:**
- Убедитесь, что задача не занята (проверьте Issues и PR)
- Создайте Issue, если его нет
- Обсудите подход к решению в комментариях Issue

---

## Процесс разработки

### Workflow: Fork + Feature Branch + Pull Request

```bash
# 1. Сделайте fork репозитория на GitHub

# 2. Клонируйте свой fork
git clone https://github.com/YOUR_USERNAME/dm.git
cd dm

# 3. Добавьте upstream remote
git remote add upstream https://github.com/quilin/dm.git

# 4. Создайте feature ветку от dev
git checkout dev
git pull upstream dev
git checkout -b feature/your-feature-name

# 5. Внесите изменения и закоммитьте
git add .
git commit -m "[Component] Brief description"

# 6. Синхронизируйте с upstream перед push
git pull upstream dev --rebase

# 7. Push в ваш fork
git push origin feature/your-feature-name

# 8. Создайте Pull Request на GitHub
# Базовая ветка: dev
# Сравниваемая ветка: feature/your-feature-name
```

### Соглашения по именованию веток

| Тип | Формат | Пример |
|-----|--------|--------|
| Новая функция | `feature/description` | `feature/add-game-search` |
| Исправление бага | `fix/description` | `fix/login-validation` |
| Рефакторинг | `refactor/description` | `refactor/extract-game-service` |
| Документация | `docs/description` | `docs/update-setup-guide` |
| Тесты | `test/description` | `test/add-forum-tests` |

### Синхронизация с upstream

Регулярно синхронизируйте вашу ветку с upstream:

```bash
# Получить изменения из upstream
git fetch upstream

# Rebase вашей ветки на upstream/dev
git rebase upstream/dev

# Если возникли конфликты, разрешите их и продолжите
git add .
git rebase --continue

# Force push в ваш fork (ТОЛЬКО в свою feature ветку!)
git push origin feature/your-feature-name --force-with-lease
```

---

## Стандарты кода

### Backend (.NET)

**Следуйте официальному руководству:**
- [docs/PROJECT_STANDARDS.md](./docs/PROJECT_STANDARDS.md)

**Ключевые требования:**

1. **Nullable Reference Types**: всегда включены
   ```csharp
   // ✅ Правильно
   public string Name { get; set; } = string.Empty;
   public User? CurrentUser { get; set; }

   // ❌ Неправильно
   public string Name { get; set; }  // CS8618 warning
   ```

2. **Async/Await**: используйте `ConfigureAwait(false)` в library коде
   ```csharp
   // ✅ Правильно
   var result = await repository.GetAsync(id).ConfigureAwait(false);

   // ❌ Неправильно (в library коде)
   var result = await repository.GetAsync(id);
   ```

3. **Архитектура**: соблюдайте Hexagonal Architecture
   - Бизнес-логика только в services
   - Контроллеры только для маршрутизации и валидации
   - Repository pattern для доступа к данным

4. **Naming Conventions**:
   - Classes: `PascalCase`
   - Methods: `PascalCase`
   - Private fields: `_camelCase`
   - Local variables: `camelCase`

5. **Dependency Injection**: регистрируйте зависимости в модулях Autofac
   ```csharp
   builder.RegisterType<TopicService>()
       .As<ITopicService>()
       .InstancePerLifetimeScope();
   ```

### Frontend (Vue 3 + TypeScript)

1. **Composition API**: используйте только Composition API
   ```vue
   <script setup lang="ts">
   import { ref, computed } from 'vue'

   const count = ref(0)
   const doubled = computed(() => count.value * 2)
   </script>
   ```

2. **TypeScript**: строгая типизация
   ```typescript
   // ✅ Правильно
   interface User {
     id: string
     name: string
     email: string
   }

   const fetchUser = async (id: string): Promise<User> => {
     return await userApi.get(id)
   }
   ```

3. **Pinia Stores**: один store на домен
   ```typescript
   export const useUserStore = defineStore('user', () => {
     const user = ref<User | null>(null)
     const isAuthenticated = computed(() => user.value !== null)

     const login = async (credentials: LoginDto) => {
       user.value = await authApi.login(credentials)
     }

     return { user, isAuthenticated, login }
   })
   ```

4. **Naming Conventions**:
   - Components: `PascalCase` (TheHeader.vue, UserCard.vue)
   - Composables: `camelCase` с префиксом `use` (useFetchData.ts)
   - Stores: `camelCase` с суффиксом `Store` (useUserStore)

### Общие требования

1. **Комментарии**: пишите комментарии на русском языке
   ```csharp
   // Проверяем права доступа пользователя к игре
   intentionManager.ThrowIfForbidden(GameIntention.Edit, game);
   ```

2. **Логирование**: используйте структурированное логирование
   ```csharp
   _logger.LogInformation("User {UserId} created topic {TopicId}", userId, topicId);
   ```

3. **Обработка ошибок**: не глушите исключения
   ```csharp
   // ✅ Правильно
   try {
       await service.ProcessAsync();
   } catch (Exception ex) {
       _logger.LogError(ex, "Failed to process");
       throw;  // Re-throw или обработайте
   }

   // ❌ Неправильно
   try {
       await service.ProcessAsync();
   } catch { }  // Молчаливое игнорирование
   ```

---

## Требования к коммитам

### Формат commit message

```
[Компонент] Краткое описание (до 72 символов)

Детальное описание изменений, если необходимо.
Объясните "почему", а не "что".

- Маркированный список изменений
- Если их несколько

Refs #123
Fixes #456
```

### Примеры хороших commit messages

```
[DM.Forum] Добавлена пагинация для списка топиков

Реализована пагинация с параметрами page и size.
По умолчанию выводится 20 топиков на страницу.

Refs #123
```

```
[DM.Gaming] Исправлен баг с созданием персонажа

При создании персонажа без указания аватара
возникала ошибка NullReferenceException.

Fixes #456
```

```
[DM.Front] Рефакторинг компонента UserCard

Извлечены вложенные компоненты:
- UserAvatar
- UserBadge
- UserActions

Улучшена читаемость и тестируемость.
```

### Префиксы компонентов

| Префикс | Область |
|---------|---------|
| `[DM.API]` | API контроллеры |
| `[DM.Auth]` | Аутентификация |
| `[DM.Forum]` | Форумная система |
| `[DM.Gaming]` | Игровая система |
| `[DM.Community]` | Сообщество (пользователи, опросы) |
| `[DM.Messaging]` | Личные сообщения |
| `[DM.Front]` | Frontend |
| `[DM.Infra]` | Инфраструктура (Docker, CI/CD) |
| `[DM.Docs]` | Документация |
| `[DM.Tests]` | Тесты |

### Требования к коммитам

- **Atomic commits**: один коммит = одно логическое изменение
- **Частые коммиты**: коммитьте часто, маленькими порциями
- **Не коммитьте**:
  - Сгенерированные файлы (если они в .gitignore)
  - Секреты и пароли
  - Личные настройки IDE
  - Закомментированный код (удалите его)
  - Console.WriteLine / console.log для отладки

---

## Требования к тестам

### Обязательные тесты

**Каждый Pull Request должен включать тесты:**

1. **Unit тесты** - для бизнес-логики
   ```csharp
   [Fact]
   public async Task CreateTopic_WithValidData_CreatesTopicSuccessfully()
   {
       // Arrange
       var service = new TopicCreatingService(/*...*/);
       var dto = new CreateTopic { Title = "Test", Text = "Content" };

       // Act
       var result = await service.Create(forumId, dto);

       // Assert
       Assert.NotNull(result);
       Assert.Equal("Test", result.Title);
   }
   ```

2. **Integration тесты** - для API endpoints
   ```csharp
   [Fact]
   public async Task POST_CreateTopic_ReturnsCreated()
   {
       // Arrange
       var client = _factory.CreateClient();
       var dto = new CreateTopic { Title = "Test", Text = "Content" };

       // Act
       var response = await client.PostAsJsonAsync($"/v1/boards/{forumId}/topics", dto);

       // Assert
       Assert.Equal(HttpStatusCode.Created, response.StatusCode);
   }
   ```

3. **Frontend тесты** - для компонентов (если изменяете UI)
   ```typescript
   describe('UserCard', () => {
     it('renders user name', () => {
       const user = { id: '1', name: 'Alice' }
       const wrapper = mount(UserCard, { props: { user } })

       expect(wrapper.text()).toContain('Alice')
     })
   })
   ```

### Где размещать тесты

```
dm3/
├── test/
│   ├── DM.Services.Authentication.Tests/
│   ├── DM.Services.Forum.Tests/
│   ├── DM.Services.Gaming.Tests/
│   └── DM.Web.API.IntegrationTests/
└── frontend/DM.Web.Modern/
    └── src/
        └── components/
            └── __tests__/
```

### Запуск тестов

```bash
# Backend unit tests
dotnet test

# Backend integration tests
dotnet test --filter "Category=Integration"

# Frontend tests
cd frontend/DM.Web.Modern
yarn test

# Frontend tests в watch режиме
yarn test:watch
```

### Покрытие кода

**Минимальные требования:**
- Новый код: >80% покрытие
- Критическая бизнес-логика: 100% покрытие
- Контроллеры: integration тесты для всех endpoints

**Проверка покрытия:**
```bash
# Backend
dotnet test /p:CollectCoverage=true

# Frontend
yarn test:coverage
```

---

## Документация

### Когда нужна документация

**Обязательно документируйте:**
- Новые API endpoints (Swagger комментарии)
- Изменения в архитектуре (docs/architecture/)
- Новые стандарты или практики (docs/PROJECT_STANDARDS.md)
- Breaking changes (CHANGELOG.md)

### Swagger комментарии

```csharp
/// <summary>
/// Создаёт новый топик на форуме
/// </summary>
/// <param name="forumId">ID форума</param>
/// <param name="dto">Данные топика</param>
/// <returns>Созданный топик</returns>
/// <response code="201">Топик успешно создан</response>
/// <response code="400">Невалидные данные</response>
/// <response code="403">Нет прав на создание топика</response>
[HttpPost]
[ProducesResponseType(typeof(Envelope<Topic>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public async Task<IActionResult> CreateTopic(
    [FromRoute] Guid forumId,
    [FromBody] CreateTopic dto)
{
    // Implementation
}
```

### Документация в коде

```csharp
// ✅ Хорошие комментарии (объясняют "почему")
// Используем exponential backoff, чтобы не перегружать OpenSearch
// при временных сбоях
await Policy
    .Handle<OpenSearchException>()
    .WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
    .ExecuteAsync(() => indexer.IndexAsync(document));

// ❌ Плохие комментарии (дублируют код)
// Создаём пользователя
var user = new User();
```

### Обновление документации

**При изменении функциональности обновите:**
- README.md (если изменились инструкции по установке)
- docs/architecture/OVERVIEW.md (если изменилась архитектура)
- CHANGELOG.md (все значимые изменения)

---

## Code Review

### Создание Pull Request

**Хороший PR должен:**

1. **Иметь понятное описание**
   ```markdown
   ## Описание
   Добавлена пагинация для списка игр

   ## Изменения
   - Добавлен параметр `page` в API
   - Обновлён компонент GamesList
   - Добавлены тесты

   ## Тестирование
   1. Откройте /games
   2. Проверьте, что выводится 20 игр
   3. Нажмите "Следующая страница"

   ## Скриншоты
   ![Pagination](screenshot.png)

   Closes #123
   ```

2. **Быть небольшим** (до 400 строк изменений)
   - Если больше - разбейте на несколько PR
   - Рефакторинг и новая функциональность - в разных PR

3. **Проходить все проверки**
   - CI/CD pipeline успешно завершён
   - Все тесты проходят
   - Линтеры не выдают ошибок

4. **Иметь связь с Issue**
   - `Refs #123` - связанная задача
   - `Closes #123` - PR закрывает задачу

### Процесс review

1. **Автоматические проверки**
   - CI/CD pipeline
   - Code coverage
   - Linters

2. **Ручной review** (1-2 reviewers)
   - Архитектура и дизайн
   - Качество кода
   - Тесты
   - Документация

3. **Обсуждение**
   - Reviewers оставляют комментарии
   - Автор вносит правки
   - Обсуждаем до консенсуса

4. **Approval**
   - Минимум 1 approval от reviewer
   - Все conversations resolved
   - CI успешно прошёл

5. **Merge**
   - Maintainer делает merge в dev
   - Ветка удаляется после merge

### Как быть хорошим reviewer

**DO:**
- Будьте конструктивны и вежливы
- Объясняйте "почему", а не только "что"
- Предлагайте альтернативы
- Хвалите хороший код

**DON'T:**
- Не делайте личных замечаний
- Не требуйте идеального кода (perfect is enemy of good)
- Не затягивайте review (отвечайте в течение 24-48 часов)

**Примеры комментариев:**

```
✅ Хорошо:
"Отличная идея с извлечением этой логики в отдельный метод!
Предлагаю также добавить unit тест для этого метода, чтобы
зафиксировать поведение."

❌ Плохо:
"Это плохой код. Переделай."
```

---

## Коммуникация

### GitHub Issues

**Используйте Issues для:**
- Сообщений о багах
- Предложений новых функций
- Вопросов по архитектуре
- Обсуждения технических решений

**Шаблон Issue для бага:**
```markdown
**Описание бага:**
Краткое описание проблемы

**Шаги для воспроизведения:**
1. Откройте страницу X
2. Нажмите кнопку Y
3. Увидите ошибку Z

**Ожидаемое поведение:**
Что должно происходить

**Фактическое поведение:**
Что происходит на самом деле

**Окружение:**
- Браузер: Chrome 120
- ОС: Windows 11
- Версия: dev branch, commit abc123

**Скриншоты:**
[Приложите скриншоты если возможно]
```

### Discord

**Используйте Discord для:**
- Быстрых вопросов
- Обсуждения идей
- Координации работы
- Знакомства с командой

**Каналы:**
- `#dev-chat` - общий чат разработчиков
- `#code-review` - запросы на review
- `#architecture` - обсуждение архитектуры
- `#help` - вопросы и помощь

### Встречи

**Регулярные встречи:**
- **Спринт планирование** - раз в 2 недели (по понедельникам)
- **Daily standup** - ежедневно (опционально, в Discord)
- **Code review session** - по запросу

---

## Контрольный список перед созданием PR

- [ ] Код соответствует [стандартам проекта](./docs/PROJECT_STANDARDS.md)
- [ ] Добавлены/обновлены unit тесты
- [ ] Добавлены/обновлены integration тесты (если изменяли API)
- [ ] Все тесты проходят локально (`dotnet test` и `yarn test`)
- [ ] Добавлены Swagger комментарии (если новый/изменённый endpoint)
- [ ] Обновлена документация (если нужно)
- [ ] Обновлён CHANGELOG.md
- [ ] Commit messages соответствуют формату
- [ ] Ветка синхронизирована с upstream/dev
- [ ] PR description заполнен полностью
- [ ] Issue связан с PR (`Refs #123` или `Closes #123`)

---

## Дополнительные ресурсы

### Документация проекта

- [README.md](./README.md) - главная страница
- [docs/SETUP.md](./docs/SETUP.md) - установка окружения
- [docs/architecture/OVERVIEW.md](./docs/architecture/OVERVIEW.md) - архитектура
- [docs/PROJECT_STANDARDS.md](./docs/PROJECT_STANDARDS.md) - стандарты кода
- [docs/SECURITY.md](./docs/SECURITY.md) - безопасность
- [docs/tasks/ROADMAP.md](./docs/tasks/ROADMAP.md) - дорожная карта

### Внешние ресурсы

- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Hexagonal Architecture](https://alistair.cockburn.us/hexagonal-architecture/)
- [Vue 3 Style Guide](https://vuejs.org/style-guide/)
- [C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)

---

## Вопросы?

Если у вас есть вопросы по процессу разработки:

1. Проверьте документацию (возможно, ответ уже есть)
2. Спросите в Discord канале `#help`
3. Создайте Issue с вопросом
4. Напишите maintainer напрямую

**Контакты:**
- GitHub: [@quilin](https://github.com/quilin)
- Discord: dm3-dev server

---

## Благодарности

Спасибо за ваш вклад в проект DM3! Каждый Pull Request делает проект лучше. ❤️

---

> **Последнее обновление:** 2026-01-27
> **Версия документа:** 1.0
> **Maintainer:** @quilin
