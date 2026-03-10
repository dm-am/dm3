# Тестирование DM3

---

## Обзор

| Слой | Фреймворк | Назначение |
|------|-----------|------------|
| Backend Unit | xUnit + Moq + FluentAssertions | Бизнес-логика, интеграция |
| Frontend Unit | Vitest + Vue Test Utils | Компоненты, утилиты, BBCode |

---

## Запуск тестов

### Backend

```bash
# Все тесты
dotnet test

# Конкретный проект
dotnet test test/DM.Domain.Forum.Tests

# С фильтром
dotnet test --filter "TopicCreatingService"

# С отчётом
dotnet test --logger "trx;LogFileName=results.trx"
```

### Frontend

```bash
cd src/DM.Web.Client

npm run test:unit              # Все тесты
npm run test:unit -- --watch      # Watch mode
npm run test:unit -- --coverage   # С покрытием
```

---

## Backend Unit Tests

### Структура проектов

| Проект | Описание |
|--------|----------|
| `DM.Domain.Account.Tests` | Аутентификация, регистрация, сессии |
| `DM.Domain.Blog.Tests` | Блоги, публикации |
| `DM.Domain.Community.Tests` | Сообщество, опросы, отзывы |
| `DM.Domain.Forum.Tests` | Форум, топики |
| `DM.Domain.Game.Tests` | Игры, комнаты, персонажи |
| `DM.Domain.Messaging.Tests` | Сообщения, чаты |
| `DM.Domain.Moderation.Tests` | Модерация, баны |
| `DM.Domain.Personal.Tests` | Профили, уведомления |
| `DM.Infrastructure.Core.Tests` | Ядро (parsing, utilities) |
| `DM.Infrastructure.Messaging.Tests` | Очередь сообщений |
| `DM.Infrastructure.Persistence.Tests` | Репозитории |
| `DM.Web.API.IntegrationTests` | API интеграционные тесты |

### Паттерн именования

```
{Behavior}_When_{Condition}
```

Примеры: `CreateTopic_When_ValidInput`, `ThrowException_When_UserNotFound`

### Пример теста

```csharp
public class TopicCreatingServiceShould : UnitTestBase
{
    [Fact]
    public async Task CreateTopic_When_ValidInput()
    {
        // Arrange
        var createTopic = new CreateTopic { Title = "Test" };
        _repository
            .Setup(r => r.Create(It.IsAny<ForumTopic>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Topic { Id = Guid.NewGuid() });

        // Act
        var result = await _service.CreateTopic(createTopic);

        // Assert
        result.Should().NotBeNull();
    }
}
```

### CancellationToken в Mocks

```csharp
// Правильно
repository.Setup(r => r.Get(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(entity);

// Неправильно (CS0854)
repository.Setup(r => r.Get(It.IsAny<Guid>()))
    .ReturnsAsync(entity);
```

---

## Frontend Tests

### Структура

```
src/
├── components/**/*.spec.ts   # Тесты компонентов
└── utils/**/*.spec.ts        # Тесты утилит (BBCode и др.)
```

### Пример теста

```typescript
import { describe, it, expect } from 'vitest';
import { bbcodeToHtml } from '@/utils/bbcode';

describe('bbcodeToHtml', () => {
  it('converts bold text', () => {
    expect(bbcodeToHtml('[b]text[/b]')).toContain('<strong>text</strong>');
  });
});
```

---

## Чек-лист перед коммитом

- [ ] `dotnet test` проходит
- [ ] `npm run test:unit` проходит
- [ ] Новый код покрыт тестами
- [ ] Нет flaky тестов

---

## Ссылки

- [Установка](./LOCAL_SETUP.md) — Запуск проекта
- [Паттерны](../conventions/PATTERNS.md) — Правила разработки
- [Архитектура](../architecture/SYSTEM.md) — Как устроено

