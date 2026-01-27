# Тестирование DM3

> **Обновлено:** 2026-01-27

---

## Обзор

| Слой | Фреймворк | Тестов | Назначение |
|------|-----------|--------|------------|
| Backend Unit | xUnit + Moq + FluentAssertions | ~280 | Бизнес-логика |
| Backend Integration | xUnit + WebApplicationFactory | 120 | API endpoints |
| Frontend Unit | Vitest + Vue Test Utils | ~400 | Компоненты, утилиты, BBCode |

---

## Запуск тестов

### Backend

```bash
# Все тесты
dotnet test

# Конкретный проект
dotnet test test/DM.Services.Forum.Tests

# С фильтром
dotnet test --filter "TopicCreatingService"

# С отчётом
dotnet test --logger "trx;LogFileName=results.trx"
```

### Frontend

```bash
cd frontend/DM.Web.Modern

yarn test:unit              # Все тесты
yarn test:unit --watch      # Watch mode
yarn test:unit --coverage   # С покрытием
```

---

## Backend Unit Tests

### Структура проектов

| Проект | Тестов | Описание |
|--------|--------|----------|
| `DM.Services.Authentication.Tests` | 27 | Аутентификация |
| `DM.Services.Forum.Tests` | 35 | Форум |
| `DM.Services.Community.Tests` | 36 | Сообщество |
| `DM.Services.Gaming.Tests` | 18 | Игры |
| `DM.Services.Common.Tests` | 12 | Общие сервисы |
| `DM.Services.Uploading.Tests` | 41 | Загрузка файлов |
| `DM.Services.Notifications.Tests` | 28 | Уведомления |
| `DM.Services.MessageQueuing.Tests` | 13 | Очередь сообщений |

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

## Backend Integration Tests

**Проект:** `test/DM.Web.API.IntegrationTests`

### Покрытие (117 тестов)

| Controller | Тестов |
|------------|--------|
| AccountController | 18 |
| TopicController | 19 |
| CommentController | 16 |
| GameController | 34 |
| ConversationController | 18 |

### Запуск

```bash
dotnet test test/DM.Web.API.IntegrationTests
dotnet test --filter "GameController"
```

### SQLite ограничения

- OpenIddict не поддерживается (условный `UseOpenIddict()`)
- DateTimeOffset в ORDER BY может давать 500

---

## Frontend Tests

### Структура

| Путь | Тестов |
|------|--------|
| `utils/bbcode.spec.ts` | 201 |
| `components/inputs/tiptap-extensions/` | 90 |
| `utils/bbcode-roundtrip.spec.ts` | ~20 |

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
- [ ] `yarn test:unit` проходит
- [ ] Новый код покрыт тестами
- [ ] Нет flaky тестов
