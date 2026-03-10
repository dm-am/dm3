# Мониторинг DM3

> **User Story:** "Где смотреть логи? Как настроить алерты?"

---

## Обзор стека

```
Serilog ────► OpenSearch ────► Kibana (:5601)
OpenTelemetry ────► Jaeger (:16686)
/metrics ────► Prometheus (:9090) ────► Grafana (:3000)
```

---

## Логирование (Serilog)

### Конфигурация

```
Sinks: OpenSearch (dm_logstash-{date}), Console
Enrichers: Application, Environment, LogContext, ActivityEnricher (TraceId, SpanId)
```

### Где смотреть

| Что | URL |
|-----|-----|
| OpenSearch Dashboard | http://localhost:5601 |
| Индекс | `dm_logstash-{date}` |

### Полезные запросы

```
# Ошибки за последний час
level: "Error" AND @timestamp:[now-1h TO now]

# Запросы конкретного пользователя
userId: "12345"

# Медленные запросы (> 1s)
duration: >1000
```

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

`/metrics` на всех .NET сервисах

### Scrape targets

| Сервис | Порт |
|--------|------|
| dm-api | 5000 |
| consumer-mail | 5003 |
| consumer-search | 5001 |
| consumer-notification | 5002 |
| postgres-exporter | 9187 |
| node-exporter | 9100 |

### Где смотреть

| Что | URL |
|-----|-----|
| Prometheus | http://localhost:9090 |
| Targets | http://localhost:9090/targets |

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

### Логи не появляются в OpenSearch

1. Проверить что OpenSearch запущен: `docker ps | grep opensearch`
2. Проверить подключение: `curl http://localhost:9200`
3. Проверить индекс: `curl http://localhost:9200/_cat/indices`

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
