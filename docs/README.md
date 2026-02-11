# Документация DM3

Навигация по документации проекта.

---

## Архитектура

| Документ | Описание |
|----------|----------|
| [OVERVIEW.md](./architecture/OVERVIEW.md) | Общая архитектура системы |
| [DATABASE.md](./architecture/DATABASE.md) | Схема базы данных (PostgreSQL + MongoDB) |
| [AUTHENTICATION.md](./architecture/AUTHENTICATION.md) | Аутентификация и сессии |
| [RBAC.md](./architecture/RBAC.md) | Роли и права доступа |

---

## Руководства

| Документ | Описание |
|----------|----------|
| [SETUP.md](./guides/SETUP.md) | Установка и запуск |
| [TESTING.md](./guides/TESTING.md) | Тестирование |
| [DEPLOYMENT.md](./guides/DEPLOYMENT.md) | Деплоймент |
| [MIRRORING.md](./guides/MIRRORING.md) | Настройка зеркал |

---

## Справочники

| Документ | Описание |
|----------|----------|
| [API REFERENCE.md](./api/REFERENCE.md) | Справочник API |
| [GLOSSARY.md](./reference/GLOSSARY.md) | Глоссарий терминов |
| [CODE.md](./standards/CODE.md) | Стандарты кода |

---

## Задачи

| Документ | Описание |
|----------|----------|
| [ROADMAP.md](./tasks/ROADMAP.md) | Дорожная карта |
| [BACKLOG.md](./tasks/BACKLOG.md) | Бэклог задач |

---

## Быстрый старт

```powershell
.\scripts\dm.ps1 start    # Запуск (Docker + API)
.\scripts\dm.ps1 seed     # Тестовые данные
.\scripts\dm.ps1 reset    # Сброс БД + перезапуск
```

Frontend: `cd frontend/DM.Web.Modern && npm install && npm run dev`

---

## Принципы документации

- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
