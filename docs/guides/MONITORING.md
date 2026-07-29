# Мониторинг DM3

> **User Story:** "Где смотреть логи? Как настроить алерты?"

---

## Обзор стека

```
Serilog ────────► Loki (:3100) ──────┐
OpenTelemetry ──► Jaeger (:16686)    ├──► Grafana (:3000)
/metrics ───────► Prometheus (:9090) ┘
```

---

## Логирование (Serilog)

### Конфигурация

```
Sinks: Loki (push напрямую из приложения), Console
Enrichers: Application, Environment, LogContext, ActivityEnricher (TraceId, SpanId)
```

**Метки против содержимого.** Loki индексирует метки, а не текст строки, поэтому
меток ровно две — `app` и `env`. Все остальное (пользователь, корреляционный
токен, длительность) остается в теле строки и отбирается фильтром LogQL. Метка на
величину с большим числом значений размножает потоки без предела — это
единственный способ сделать Loki медленным, и правило это, а не деталь настройки.

Агента-сборщика нет: Serilog отправляет пакеты по HTTP сам, файлов на диске не
возникает.

### Где смотреть

Grafana → Explore → датасорс Loki. Отдельного UI у логов нет намеренно: метрики,
трейсы и логи смотрятся из одного места, а `TraceId` в строке лога — кликабельная
ссылка в трейс Jaeger (derived field датасорса).

### Полезные запросы (LogQL)

```
# Ошибки API за последний час (интервал задается в UI)
{app="DM.API"} | json | level = "Error"

# Все, что писалось про конкретный корреляционный токен
{app=~"DM.+"} |= "1b2c3d4e"

# Поток одного воркера
{app="DM.Notifications.Consumer"}
```

Ретеншен — 30 дней, задан в `docker/loki/loki.yaml` и применяется компактором.

---

## Tracing (OpenTelemetry)

### Инструментация

- ASP.NET Core
- gRPC
- HTTP client
- EF Core
- MongoDB
- RabbitMQ

### Где смотреть

| Что | URL |
|-----|-----|
| Jaeger UI | http://localhost:16686 |
| Протокол | OTLP gRPC (порт 4317) |

### Поиск trace

1. Выбрать Service: `dm-api`
2. Найти по TraceId из логов
3. Просмотреть spans и зависимости

---

## Metrics (Prometheus)

### Endpoint

`/metrics` и `/_health` — на каждом .NET сервисе. Хост, отдающий gRPC, слушает
cleartext HTTP/2 и не отвечает HTTP/1.1-клиенту, поэтому health и метрики у него
на отдельном порту.

### Scrape targets

**Источник истины:** [`docker/prometheus.yml`](../../docker/prometheus.yml).
Имя job обязано совпадать с `container_name` из compose: при расхождении target
просто отсутствует, а алерт на `up == 0` молчит — именно поэтому рядом стоит
`ConsumerScrapeTargetMissing` на `absent()`.

### Где смотреть

| Что | URL |
|-----|-----|
| Prometheus | http://localhost:9090 |
| Targets | http://localhost:9090/targets |

> **Доступ только с самой машины.** Все служебные интерфейсы — Prometheus,
> Grafana, Loki, Jaeger, консоль MinIO, порты воркеров —
> публикуются на `127.0.0.1`, а не наружу. Правила брандмауэра тут не помощник:
> опубликованный докером порт идет через цепочки nat/DOCKER и FORWARD, минуя
> INPUT, поэтому единственная надежная граница — сам адрес привязки. С удаленной
> машины смотреть через SSH-туннель:
>
> ```bash
> ssh -N -L 3000:127.0.0.1:3000 -L 9090:127.0.0.1:9090 user@server
> ```

### Полезные запросы PromQL

```promql
# Количество запросов в секунду
rate(http_requests_total[5m])

# Средняя latency
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))

# Ошибки 5xx
sum(rate(http_requests_total{status=~"5.."}[5m]))
```

---

## Dashboards (Grafana)

### Доступ

| Что | URL | Credentials |
|-----|-----|-------------|
| Grafana | http://localhost:3000 | из `docker/.env` |

### Доступные dashboards

| Dashboard | Что показывает |
|-----------|----------------|
| API Overview | Requests, latency, errors |
| Infrastructure | CPU, memory, disk |
| Consumers | Message rates, processing time |

Dashboards настроены автоматически (auto-provisioned).

---

## Alerting

### Правила

7 правил в `docker/prometheus/alerts.yml`:

| Правило | Условие |
|---------|---------|
| ApiDown | dm-api не отвечает > 1 мин |
| HighErrorRate | > 1% ошибок за 5 мин |
| HighLatency | p95 > 2 сек за 5 мин |
| ConsumerDown | Consumer не отвечает > 5 мин |
| PostgresDown | PostgreSQL не отвечает > 1 мин |
| HighMemoryUsage | > 85% памяти |
| DiskSpaceLow | < 10% свободного места |

### Настройка уведомлений

1. Открыть Grafana → Alerting → Notification channels
2. Добавить канал (Telegram, Discord, Email)
3. Привязать к правилам

### Telegram бот (рекомендуется)

```yaml
# docker/prometheus/alertmanager.yml
receivers:
  - name: telegram
    telegram_configs:
      - bot_token: '<BOT_TOKEN>'
        chat_id: <CHAT_ID>
```

---

## Troubleshooting

### Логи не появляются в Loki

1. Проверить что Loki запущен и готов: `curl http://localhost:3100/ready`
2. Проверить, что поток вообще создан: `curl -s http://localhost:3100/loki/api/v1/label/app/values`
3. Если потока нет — смотреть консольный sink (`docker logs dm-api`): он пишет
   всегда, и ошибка отправки в Loki видна там же

### Jaeger не показывает traces

1. Проверить что Jaeger запущен: `docker ps | grep jaeger`
2. Проверить OTLP endpoint: `curl http://localhost:4317`
3. Проверить env var: `OTEL_EXPORTER_OTLP_ENDPOINT`

### Grafana dashboards пустые

1. Проверить Prometheus targets: http://localhost:9090/targets
2. Проверить data source в Grafana
3. Проверить временной диапазон (Last 5 minutes)

---

## Production

### Рекомендации

- Увеличить retention для логов (30+ дней)
- Настроить alert notifications (Telegram/Discord)
- Добавить Better Stack для external monitoring
- Настроить backup для Prometheus data

### External monitoring

Добавить Better Stack для мониторинга из интернета:
1. Зарегистрироваться на betterstack.com
2. Добавить heartbeat для `/_health`
3. Настроить Telegram alerts

---

## Ссылки

- [DEPLOYMENT.md](./DEPLOYMENT.md) — production setup
- [CONFIGURATION.md](../references/CONFIGURATION.md) — все порты и настройки
- [LOCAL_SETUP.md](./LOCAL_SETUP.md) — локальный запуск
