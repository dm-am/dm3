# DM3

Платформа для текстовых ролевых игр.

**Стек:** .NET 8, PostgreSQL, MongoDB, RabbitMQ, Vue 3, TypeScript

---

## Быстрый старт

```powershell
# Windows
.\scripts\dm.ps1 start    # Запуск Docker + API
.\scripts\dm.ps1 seed     # Тестовые данные
.\scripts\dm.ps1 stop     # Остановка
.\scripts\dm.ps1 reset    # Сброс БД и перезапуск
.\scripts\dm.ps1 status   # Статус сервисов
.\scripts\dm.ps1 logs     # Логи (или logs dm-api)

# Linux/Mac — те же команды через ./scripts/dm.sh

# Frontend (отдельно)
cd src/DM.Web.Client && npm install && npm run dev
```

| Сервис | URL |
|--------|-----|
| Frontend | http://localhost:5173 |
| API / Swagger | http://localhost:5000 |
| MinIO | http://localhost:9001 |
| MailHog | http://localhost:8025 |

**Подробнее:** [docs/guides/setup.md](./docs/guides/setup.md)

---

## Документация

### Руководства (как делать)

| Документ | Описание |
|----------|----------|
| [setup.md](./docs/guides/setup.md) | Установка, порты, тестовые аккаунты |
| [testing.md](./docs/guides/testing.md) | Тестирование |
| [deployment.md](./docs/guides/deployment.md) | Деплоймент и бэкапы |
| [mirroring.md](./docs/guides/mirroring.md) | Настройка зеркал |

### Архитектура (как устроено)

| Документ | Описание |
|----------|----------|
| [patterns.md](./docs/architecture/patterns.md) | **Паттерны, структура проектов, блюпринт (SSOT)** |
| [overview.md](./docs/architecture/overview.md) | Компоненты, порты, потоки данных |
| [database.md](./docs/architecture/database.md) | Схема БД |
| [security.md](./docs/architecture/security.md) | Аутентификация, авторизация, RBAC |

### Справочники

| Документ | Описание |
|----------|----------|
| [api.md](./docs/reference/api.md) | API эндпоинты |
| [standards.md](./docs/reference/standards.md) | Стандарты кода и API |
| [glossary.md](./docs/reference/glossary.md) | Термины |
| [policies.md](./docs/reference/policies.md) | Политика имён пользователей |

### Проект

| Документ | Описание |
|----------|----------|
| [roadmap.md](./docs/project/roadmap.md) | Дорожная карта |
| [migration.md](./docs/project/migration.md) | Миграция со старого сайта |

---

## Структура проекта

```
dm3/
├── src/                    # Backend (.NET 8)
│   ├── DM.Domain.*/        # Доменные сервисы
│   ├── DM.Infrastructure.*/# Инфраструктура
│   ├── DM.Web.API/         # REST API
│   ├── DM.Web.Client/      # Frontend (Vue 3)
│   └── DM.Workers.*/       # Background workers
├── test/                   # Backend тесты
├── docker/                 # Docker конфигурация
├── scripts/                # CLI скрипты
└── docs/                   # Документация
    ├── guides/             # Руководства
    ├── architecture/       # Архитектура
    ├── reference/          # Справочники
    └── project/            # Проект
```

---

## Принципы документации

- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
