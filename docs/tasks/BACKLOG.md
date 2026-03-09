# Бэклог задач DM3

## Зеркала

| Задача | Описание |
|--------|----------|
| Разобраться с dm.am | Почему не грузится в России? |
| Session transfer | Реализовать перенос сессий между зеркалами |

---

## Интеграции

| Задача | Описание |
|--------|----------|
| Discord бот | Уведомления через бота (реализовать senders) |
| Telegram бот | Уведомления через бота (реализовать senders) |
| Google OAuth | Вход через Google аккаунт |

---

## Рефакторинг

| Задача | Файл/Область |
|--------|--------------|
| Рефакторинг BbParserWrapper | `src/DM.Infrastructure.Core/Parsing/BbParserWrapper.cs` |
| Удалить NotImplementedException | `SchemaFactory.cs` в Game модуле |
| Удалить console.log | Frontend (найти в src/) |
| ConfigureAwait(false) | Добавить во все async методы |

---

## Тесты

| Задача | Описание |
|--------|----------|
| Тесты для Mail.Sender | Unit тесты с моками SMTP |
| Тесты для Search | Unit тесты + OpenSearch mock |
| Расширить E2E тесты | Playwright тесты для всех страниц |

**Недостающие тест-проекты:**
- `DM.Workers.Mail.Tests`
- `DM.Workers.SearchIndexer.Tests`

---

## Инфраструктура

| Задача | Описание |
|--------|----------|
| k3s/Helm deployment | Kubernetes манифесты |
| Umami аналитика | Self-hosted аналитика |
| Добавить LICENSE файл | MIT или другая лицензия |

---

## Кэширование

| Задача | Описание |
|--------|----------|
| User profiles | Redis/Memory cache для профилей |
| Forum moderators | Кэш списка модераторов |

---

## Frontend страницы

| Страница | Статус |
|----------|--------|
| `/game/:id/characters` | Не готово |
| `/notifications` | Не готово |
| `/blogs` | Не готово |
| `/moderation` | Не готово |
| `/support` | Не готово (ссылки в RegistrationForm, RecoveryForm, ActivationPage, FeedbackLinks) |
| `/complaint` | Не готово (ссылка в FeedbackLinks) |

---

## Ссылки

- [Дорожная карта](./ROADMAP.md)
- [Миграция со старого сайта](./MIGRATION.md)
- [Стандарты кода](../standards/CODE.md)

---

## Принципы документации


- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
