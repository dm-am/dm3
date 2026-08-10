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

# Гейты CI перед пушем — один раз на клон
git config core.hooksPath scripts/hooks
```

| Сервис | URL |
|--------|-----|
| Frontend | http://localhost:5173 |
| API / Swagger | http://localhost:5000 |
| MinIO | http://localhost:9001 |
| imgproxy | http://localhost:8080 |
| MailHog | http://localhost:8025 |

**Подробнее:** [docs/guides/LOCAL_SETUP.md](./docs/guides/LOCAL_SETUP.md)

---

## Документация

### architecture/ — Как устроена система

| Документ | User Story |
|----------|------------|
| [SYSTEM](./docs/architecture/SYSTEM.md) | "Из чего состоит система? Какая архитектура?" |
| [AUTHENTICATION](./docs/architecture/AUTHENTICATION.md) | "Как работает вход? Какие параметры безопасности?" |
| [AUTHORIZATION](./docs/architecture/AUTHORIZATION.md) | "Какие роли? Кто что может?" |
| [UPLOADS](./docs/architecture/UPLOADS.md) | "Как работает upload аватаров? Какие варианты, форматы, защиты?" |
| [BBCODE_RENDERING](./docs/architecture/BBCODE_RENDERING.md) | "Как рендерится BBCode? Кто видит приватные теги?" |

### conventions/ — Правила разработки

| Документ | User Story |
|----------|------------|
| [PATTERNS](./docs/conventions/PATTERNS.md) | "Как структурировать новую фичу? Какой блюпринт?" |
| [CODE_STYLE](./docs/conventions/CODE_STYLE.md) | "Какие стандарты кода? Какой чеклист?" |
| [API_DESIGN](./docs/conventions/API_DESIGN.md) | "Какие принципы API? Какие форматы ответов?" |
| [SECURITY](./docs/conventions/SECURITY.md) | "Какие требования к безопасности?" |
| [DATA_STORAGE](./docs/conventions/DATA_STORAGE.md) | "Когда PostgreSQL, когда MongoDB?" |
| [USERNAME_POLICY](./docs/conventions/USERNAME_POLICY.md) | "Какие символы разрешены в именах?" |
| [UI_STANDARDS](./docs/conventions/UI_STANDARDS.md) | "Какие правила верстки, токенов, диалогов?" |
| [URL_STRUCTURE](./docs/conventions/URL_STRUCTURE.md) | "Как устроены адреса страниц?" |
| [PERFORMANCE](./docs/conventions/PERFORMANCE.md) | "Какие правила оптимизации? Что нельзя делать?" |

### guides/ — Как делать

| Документ | User Story |
|----------|------------|
| [LOCAL_SETUP](./docs/guides/LOCAL_SETUP.md) | "Как запустить проект локально?" |
| [TESTING](./docs/guides/TESTING.md) | "Как запускать тесты? Какие паттерны?" |
| [DEPLOYMENT](./docs/guides/DEPLOYMENT.md) | "Как развернуть на сервере?" |
| [MIRRORING](./docs/guides/MIRRORING.md) | "Зачем сайту второй адрес? Как поднять точку присутствия?" |
| [MONITORING](./docs/guides/MONITORING.md) | "Где смотреть логи? Как настроить алерты?" |

### plans/ — Планы

| Документ | User Story |
|----------|------------|
| [Документация_по_разработке_DM3.docx](./docs/Документация_по_разработке_DM3.docx) | "Что строится целиком? Какой полный план функционала?" |
| [PROGRESS](./docs/PROGRESS.md) | "Что уже готово?" |
| [ROADMAP](./docs/plans/ROADMAP.md) | "Что планируется? Что рассматривается? Какой техдолг?" |
| [DM2_MIGRATION](./docs/plans/DM2_MIGRATION.md) | "Как мигрировать данные со старого сайта?" |
| [ERROR_PAGES_AND_LORE](./docs/plans/ERROR_PAGES_AND_LORE.md) | "Как оформлены страницы ошибок? Какой лор и достижения?" |

### references/ — Справка

| Документ | User Story |
|----------|------------|
| [GLOSSARY](./docs/references/GLOSSARY.md) | "Что означает этот термин?" |
| [CONFIGURATION](./docs/references/CONFIGURATION.md) | "Где найти настройки? Какой файл редактировать?" |

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
