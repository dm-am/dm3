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
dotnet test test/DM.Services.Forum.Tests

# С фильтром
dotnet test --filter "TopicCreatingService"

# С отчётом
dotnet test --logger "trx;LogFileName=results.trx"
```

### Frontend

```bash
cd frontend/DM.Web.Modern

npm run test:unit              # Все тесты
npm run test:unit -- --watch      # Watch mode
npm run test:unit -- --coverage   # С покрытием
```

---

## Backend Unit Tests

### Структура проектов

| Проект | Описание |
|--------|----------|
| `DM.Services.Authentication.Tests` | Аутентификация, шифрование, сессии |
| `DM.Services.Forum.Tests` | Форум |
| `DM.Services.Community.Tests` | Сообщество, чат-события |
| `DM.Services.Game.Tests` | Игры |
| `DM.Services.Common.Tests` | Общие сервисы |
| `DM.Services.Core.Tests` | Ядро (parsing, utilities) |
| `DM.Services.Notifications.Tests` | Уведомления |
| `DM.Services.MessageQueuing.Tests` | Очередь сообщений |
| `DM.Services.Uploading.Tests` | Загрузка файлов |
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

- [README](../README.md) — Обзор документации
- [Установка](./SETUP.md) — Запуск проекта
- [Стандарты кода](../standards/CODE.md) — Правила разработки
- [Архитектура](../architecture/OVERVIEW.md) — Как устроено

---

## Принципы документации


- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
