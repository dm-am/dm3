# Тестирование DM3

---

## Обзор

| Слой | Фреймворк | Назначение |
|------|-----------|------------|
| Backend Unit | xUnit + Moq + FluentAssertions | Бизнес-логика, интеграция |
| Frontend Unit | Vitest + Vue Test Utils | Компоненты, утилиты, BBCode |

---

## Что требуется от контрибьютора

**Новое поведение приезжает с тестом.** Правка, меняющая наблюдаемый результат (возвращаемые данные, статус-код, набор видимых зрителю данных), без теста не проходит ревью. Числового порога покрытия как цели нет: цель — покрытые ветки правил, а не процент.

**Unit — вариант по умолчанию.** Он дешевый, быстрый и указывает на сломанное правило точно. Integration-тест пишется тогда, когда мок не способен доказать проверяемое утверждение.

| Утверждение | Почему unit не годится |
|--------------|------------------------|
| Форма HTTP-контракта: статус-код, envelope, заголовки | Мок сервиса не проходит через фильтры, мапперы и сериализацию |
| Трансляция запроса в SQL, фильтры soft-delete, пагинация, сортировка | Мок репозитория возвращает то, что попросили, а не то, что вернет БД |
| Пайплайн авторизации на реальном endpoint: гость, роль, владелец | Атрибуты и middleware в unit-тесте не исполняются |
| Уникальность, нумерация, конкурентная вставка | Инвариант обеспечивает БД, а не код |
| Транзакционная граница: откат при ошибке в середине операции | Мок не откатывает |

Все остальное — unit: ветки правил, валидация, маппинг, расчеты, обработка ошибок.

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

# С отчетом
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

### Зеркальная структура

Тестовое дерево зеркалит production: тестовый проект на каждый production-проект, путь папки внутри тестового проекта повторяет путь тестируемого кода. Тест на новый класс кладется по зеркальному пути, а не в корень проекта.

Общие базовые классы и фикстуры не копируются по проектам — они живут в одном общем тестовом проекте. Билдеры тестовых данных лежат в `Dsl/` тестового проекта и собирают сущности fluent-методами: литеральные графы объектов в каждом тесте запрещены, иначе смена схемы правит сотни тестов вместо одного билдера.

Блюпринт дерева — [PATTERNS.md](../conventions/PATTERNS.md).

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
            .Setup(r => r.Create(It.IsAny<Topic>(), It.IsAny<CancellationToken>()))
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

Тесты колокейтятся рядом с исходником по слоям FSD:

```
src/
├── entities/{entity}/model/*.spec.ts   # Сторы и модели сущностей
├── features/{feature}/model/*.spec.ts  # Логика фич
├── shared/lib/utils/*.spec.ts          # Утилиты (BBCode и др.)
└── shared/ui/{Component}/*.spec.ts     # UI-компоненты
```

### Пример теста

```typescript
import { describe, it, expect } from 'vitest';
import { pluralize } from '@/shared/lib/utils/pluralize';

describe('pluralize', () => {
  it('picks the 2-4 form', () => {
    expect(pluralize(3, 'пост', 'поста', 'постов')).toBe('поста');
  });
});
```

---

## Matrix-тесты privacy-sensitive тегов

Тег, видимость которого зависит от прав зрителя, покрывается **матрицей**, а не одним happy-path тестом: по кейсу на каждую строку матрицы видимости — audience, роль зрителя, контекст (surface, per-post и per-room override'ы). Сама матрица и правила — [BBCODE_RENDERING.md](../architecture/BBCODE_RENDERING.md); тест обязан покрыть ее целиком, включая audience, где тег вырезается безусловно.

Обязателен негативный кейс: зритель без права получает результат, по которому невозможно установить сам факт наличия скрытого блока (инвариант zero-information erase).

Тесты матрицы — серверные: проверяемое утверждение в том, что фильтрация произошла до сериализации. Клиентские тесты покрывают round-trip редактора, а не видимость.

---

## Чек-лист перед коммитом

Список гейтов не дублируется текстом: его держит `scripts/hooks/pre-push`, который включается один раз на клон командой `git config core.hooksPath scripts/hooks`. Здесь только то, чего хук проверить не может:

- [ ] Новый код покрыт тестами
- [ ] Нет flaky тестов

---

## Ссылки

- [Установка](./LOCAL_SETUP.md) — Запуск проекта
- [Паттерны](../conventions/PATTERNS.md) — Правила разработки
- [Архитектура](../architecture/SYSTEM.md) — Как устроено

