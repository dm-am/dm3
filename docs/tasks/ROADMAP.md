# Дорожная карта DM3

## Стек

- **Backend:** .NET 8, PostgreSQL, MongoDB, RabbitMQ
- **Frontend:** Vue 3, TypeScript, Vite
- **Инфраструктура:** Docker, Nginx, SignalR, Prometheus, Grafana, Jaeger

---

## Текущий релиз (MVP)

**Готово:**
- Аутентификация (BFF Pattern, HttpOnly cookies)
- Форум (доски, топики, комментарии, лайки)
- Игры (создание, персонажи, комнаты, посты)
- Сообщения (личные переписки, глобальные чат-события)
- Уведомления (SignalR)
- Поиск (OpenSearch)
- Блоги (создание, публикации, комментарии, лайки)
- Infrastructure hardening (Docker health checks, resource limits, secrets, CI/CD matrix builds)
- Мониторинг (Prometheus + Grafana dashboards + alerting rules)
- Database resilience (EF Core retry, MongoDB retry)
- Бэкап-скрипты (PostgreSQL, MongoDB, MinIO + verify)

**В работе:**
- Frontend страницы

**Готово (недавно):**
- E2E тесты (Playwright, cookie auth)
- Remember me (опция "Запомнить меня")

---

## Планируется

| Задача | Статус |
|--------|--------|
| Зеркало в России | Подготовлено (ожидает домен) |
| Umami аналитика | Будет |
| k3s + Helm + Terraform | Рассматривается |
| Bun | Рассматривается |
| Google OAuth | Рассматривается |

---

## Ссылки

- [Бэклог](./BACKLOG.md)
- [Стандарты кода](../standards/CODE.md)

---

## Принципы документации


- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
