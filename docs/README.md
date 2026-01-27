# DM3 Documentation

> Платформа для текстовых ролевых игр

---

## Быстрый старт

```bash
cd docker && docker-compose up -d           # Инфраструктура
cd frontend/DM.Web.Modern && yarn && yarn dev  # Frontend
```

- Frontend: http://localhost:5173
- API: http://localhost:5051/swagger
- MinIO: http://localhost:9001 (minio/miniokey) — создать bucket `dm-uploads`

**Подробнее:** [guides/SETUP.md](./guides/SETUP.md)

---

## Структура

```
docs/
├── guides/           # Как делать
│   ├── SETUP.md      # Установка и запуск
│   ├── TESTING.md    # Тестирование
│   └── DEPLOYMENT.md # Деплоймент
│
├── architecture/     # Как устроено
│   ├── OVERVIEW.md   # Общая архитектура
│   ├── DATABASE.md   # Схема БД (ER)
│   └── AUTHENTICATION.md
│
├── api/              # API
│   └── REFERENCE.md  # Эндпоинты
│
├── standards/        # Правила
│   └── CODE.md       # Стандарты кода
│
├── tasks/            # Задачи
│   ├── ROADMAP.md    # План развития
│   └── BACKLOG.md    # Бэклог
│
└── reference/        # Справочники
    ├── GLOSSARY.md   # Термины
    └── 12_FACTOR.md  # Методология
```

---

## Навигация

| Вопрос | Документ |
|--------|----------|
| Как запустить? | [guides/SETUP.md](./guides/SETUP.md) |
| Как тестировать? | [guides/TESTING.md](./guides/TESTING.md) |
| Как деплоить? | [guides/DEPLOYMENT.md](./guides/DEPLOYMENT.md) |
| Какой API? | [api/REFERENCE.md](./api/REFERENCE.md) |
| Как устроено? | [architecture/OVERVIEW.md](./architecture/OVERVIEW.md) |
| Схема БД? | [architecture/DATABASE.md](./architecture/DATABASE.md) |
| Какие стандарты? | [standards/CODE.md](./standards/CODE.md) |
| Что такое X? | [reference/GLOSSARY.md](./reference/GLOSSARY.md) |
| Какие задачи? | [tasks/ROADMAP.md](./tasks/ROADMAP.md) |

---

## Технологии

**Backend:** .NET 8, PostgreSQL, MongoDB, RabbitMQ, OpenIddict

**Frontend:** Vue 3, TypeScript, Vite, Pinia, Tiptap

**Инфраструктура:** Docker, Nginx, MinIO, OpenSearch

---

## Принципы документации

- **Один README** — только здесь
- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
