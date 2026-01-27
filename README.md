# DM3

Платформа для текстовых ролевых игр.

---

## Документация

| Документ | Описание |
|----------|----------|
| [docs/README.md](./docs/README.md) | **Индекс документации** |
| [docs/SETUP.md](./docs/SETUP.md) | Установка и запуск |
| [docs/ARCHITECTURE.md](./docs/ARCHITECTURE.md) | Архитектура проекта |
| [docs/PROJECT_STANDARDS.md](./docs/PROJECT_STANDARDS.md) | Стандарты разработки |

---

## Быстрый старт

### 1. Требования

- Docker Desktop (WSL2 на Windows)
- Node.js 20+
- Yarn

### 2. Запуск

```bash
# Инфраструктура
cd docker && docker-compose up -d

# Frontend
cd frontend/DM.Web.Modern
yarn install && yarn dev
```

### 3. Доступ

| Сервис | URL |
|--------|-----|
| Frontend | http://localhost:5173 |
| API | http://localhost:5051 |
| Swagger | http://localhost:5051/swagger |
| MinIO | http://localhost:9001 (minio/miniokey) |
| RabbitMQ | http://localhost:15672 (guest/guest) |
| MailHog | http://localhost:5025 |

### 4. Настройка MinIO

1. Откройте http://localhost:9001, войдите `minio` / `miniokey`
2. Создайте bucket `dm-uploads`
3. Установите Access Policy: `Public`

---

## Тестовые аккаунты

Перейдите на `/dev/accounts` и нажмите "Создать тестовые аккаунты".

| Логин | Пароль | Роль |
|-------|--------|------|
| Alice | `Test123!` | RegularUser |
| Rayzen | `Test123!` | SeniorModerator |

---

## Структура проекта

```
dm3/
├── src/                    # Backend (.NET 8)
│   ├── DM.Services.*/      # Доменные сервисы
│   ├── DM.Web.API/         # REST API
│   └── DM.Web.Core/        # Web инфраструктура
├── test/                   # Backend тесты
├── frontend/
│   └── DM.Web.Modern/      # Frontend (Vue 3 + TypeScript)
├── docker/                 # Docker конфигурация
└── docs/                   # Документация
```

---

## Работа с почтой

Email-ы отправляются в MailHog: http://localhost:5025
