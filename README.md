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
cd frontend/DM.Web.Modern && npm install && npm run dev
```

| Сервис | URL |
|--------|-----|
| Frontend | http://localhost:5173 |
| API / Swagger | http://localhost:5000 |
| MinIO | http://localhost:9001 |
| MailHog | http://localhost:8025 |

**Подробнее:** [docs/guides/SETUP.md](./docs/guides/SETUP.md)

---

## Документация

### Guides (как делать)

| Документ | Описание |
|----------|----------|
| [SETUP.md](./docs/guides/SETUP.md) | Установка, порты, тестовые аккаунты |
| [TESTING.md](./docs/guides/TESTING.md) | Тестирование |
| [DEPLOYMENT.md](./docs/guides/DEPLOYMENT.md) | Деплоймент и бэкапы |
| [MIRRORING.md](./docs/guides/MIRRORING.md) | Настройка зеркал |

### Architecture (как устроено)

| Документ | Описание |
|----------|----------|
| [OVERVIEW.md](./docs/architecture/OVERVIEW.md) | Общая архитектура |
| [DATABASE.md](./docs/architecture/DATABASE.md) | Схема БД |
| [AUTHENTICATION.md](./docs/architecture/AUTHENTICATION.md) | Аутентификация и безопасность |
| [RBAC.md](./docs/architecture/RBAC.md) | Роли и права доступа |

### Reference

| Документ | Описание |
|----------|----------|
| [API REFERENCE.md](./docs/api/REFERENCE.md) | API эндпоинты |
| [CODE.md](./docs/standards/CODE.md) | Стандарты кода |
| [GLOSSARY.md](./docs/reference/GLOSSARY.md) | Термины |

### Tasks

| Документ | Описание |
|----------|----------|
| [ROADMAP.md](./docs/tasks/ROADMAP.md) | План развития |
| [BACKLOG.md](./docs/tasks/BACKLOG.md) | Бэклог задач |
| [AUDIT.md](./docs/tasks/AUDIT.md) | План аудита |

---

## Структура проекта

```
dm3/
├── src/                    # Backend (.NET 8)
│   ├── DM.Services.*/      # Доменные сервисы
│   ├── DM.Web.API/         # REST API + appsettings.json
│   └── DM.Web.Core/        # Web инфраструктура
├── test/                   # Backend тесты
├── frontend/DM.Web.Modern/ # Frontend (Vue 3 + TypeScript)
├── docker/                 # Docker конфигурация + .env
├── scripts/                # CLI скрипты (dm.ps1, dm.sh, seed.js)
└── docs/                   # Документация
```

---

## Принципы документации

- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
