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

**Подробнее:** [docs/guides/LOCAL_SETUP.md](./docs/guides/LOCAL_SETUP.md)

---

## Документация

### architecture/ — Как устроена система

| Документ | Зачем читать |
|----------|--------------|
| [SYSTEM](./docs/architecture/SYSTEM.md) | Понять архитектуру и компоненты |
| [DATABASE](./docs/architecture/DATABASE.md) | Понять схему данных |
| [AUTHENTICATION](./docs/architecture/AUTHENTICATION.md) | Понять как работает вход |
| [AUTHORIZATION](./docs/architecture/AUTHORIZATION.md) | Понять права и роли |

### conventions/ — Правила разработки

| Документ | Зачем читать |
|----------|--------------|
| [PATTERNS](./docs/conventions/PATTERNS.md) | Структурировать новый код (SSOT) |
| [CODE_STYLE](./docs/conventions/CODE_STYLE.md) | Писать код правильно |
| [API_DESIGN](./docs/conventions/API_DESIGN.md) | Проектировать API |
| [SECURITY](./docs/conventions/SECURITY.md) | Соблюдать требования безопасности |
| [DATA_STORAGE](./docs/conventions/DATA_STORAGE.md) | Выбрать хранилище |
| [USERNAME_POLICY](./docs/conventions/USERNAME_POLICY.md) | Валидировать имена |

### guides/ — Как делать

| Документ | Зачем читать |
|----------|--------------|
| [LOCAL_SETUP](./docs/guides/LOCAL_SETUP.md) | Запустить локально |
| [TESTING](./docs/guides/TESTING.md) | Тестировать |
| [DEPLOYMENT](./docs/guides/DEPLOYMENT.md) | Развернуть |
| [MIRRORING](./docs/guides/MIRRORING.md) | Настроить зеркало |
| [MONITORING](./docs/guides/MONITORING.md) | Мониторить |

### plans/ — Планы

| Документ | Зачем читать |
|----------|--------------|
| [ROADMAP](./docs/plans/ROADMAP.md) | Узнать статус |
| [DM2_MIGRATION](./docs/plans/DM2_MIGRATION.md) | Мигрировать данные |

### references/ — Справка

| Документ | Зачем читать |
|----------|--------------|
| [GLOSSARY](./docs/references/GLOSSARY.md) | Найти термин |
| [CONFIGURATION](./docs/references/CONFIGURATION.md) | Найти настройку |

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
    ├── architecture/       # ФАКТЫ — как устроена система
    ├── conventions/        # ПРАВИЛА — как писать код
    ├── guides/             # ИНСТРУКЦИИ — как делать
    ├── plans/              # ПЛАНЫ — что делать
    └── references/         # СПРАВКА — где найти
```

---

## Принципы документации

- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
