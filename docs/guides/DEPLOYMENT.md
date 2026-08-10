# Развертывание DM3

## Архитектура

```
Internet → Nginx → Frontend (Vue.js)
                 → API (.NET 8)
                 → MinIO (S3-compat, source-файлы)
                 → imgproxy (on-the-fly resize + AVIF/WebP)
                       ↓
              PostgreSQL / MongoDB / RabbitMQ
                       ↓
              Consumer Services (Email, Notifications)
```

---

## CI/CD (GitHub Actions)

**Файлы:** `.github/workflows/`

| Workflow | Файл | Триггеры | Действия |
|----------|------|----------|----------|
| Build & Test | `dotnet.yml` | push/PR в main, dev | Сборка, тесты, проверки качества и публикация образов; перечень job и их зависимости — в самом файле |
| Security | `security.yml` | push/PR + weekly | OWASP ZAP scan (full docker compose) |

**Образы публикуются в:** GHCR, под префиксом `IMAGE_PREFIX` из `dotnet.yml`. Имя каждого публикуемого образа обязано совпадать с тем, что тянет `docker/docker-compose.yml`, иначе CI публикует артефакт, который никто не потребляет.

**Теги:** `sha-<commit>`, `main`, `dev`, `latest` (только main)

---

## Docker Build

**Файлы:**
- Backend: [`docker/app.Dockerfile`](../../docker/app.Dockerfile)
- Frontend: [`src/DM.Web.Client/Dockerfile`](../../src/DM.Web.Client/Dockerfile)

**Принцип:** Multi-stage build (SDK → Runtime), non-root user `dmuser`

**Оптимизация:** файлы проектов копируются и восстанавливаются до исходников, поэтому слой пакетов не пересобирается на каждое изменение кода. Между прогонами слои переиспользует кэш сборки CI.

---

## Развертывание на VPS

### Автоматическая установка

```bash
DM_REF=<тег>
curl -sSL https://raw.githubusercontent.com/dm-am/dm3/$DM_REF/docker/setup-server.sh | DM_REF=$DM_REF bash
```

`DM_REF` — тег релиза. Он же выбирает тег образов, которые поднимет стенд. Ветка
тоже принимается, но это установка с движущейся цели: две одинаковые команды в
разные дни дадут разные стенды, и установщик про это скажет вслух, назвав коммит.
Для разработческого стенда так и надо, для боевого — нет.

Результат: http://<IP> за Basic Auth. Логин `preview`, пароль задает оператор: перед установкой `export DM_PREVIEW_PASSWORD=ваш_пароль`, иначе установщик остановится. Файл `docker/nginx/.htpasswd` в репозитории не хранится, его создает [`docker/scripts/init-htpasswd.sh`](../../docker/scripts/init-htpasswd.sh) на сервере. Сменить пароль: [Смена пароля](#preview-окружение).

### Ручная установка

```bash
git clone https://github.com/dm-am/dm3.git && cd dm3/docker

# Dev режим
bash scripts/init-env.sh local
docker compose up -d --build

# Preview
bash scripts/init-env.sh server
docker compose --profile production -f docker-compose.yml -f docker-compose.preview.yml up -d
```

### Preview окружение

**Правило:** сайт целиком живет в оверлее, а не в базовом compose. Базовый дает
инфраструктуру и API, оверлей добавляет nginx и контейнер SPA. Поэтому **любая**
команда, поднимающая или гасящая боевой стенд, называет оба файла — и инсталлятор,
и юнит systemd. Расхождение между ними означает, что перезапуск подменяет сайт
голым API, и его ловит отдельный гейт в CI.

**Образы, а не сборка на сервере.** Инсталлятор и юнит поднимают стенд без
`--build`: миграция, API, оба воркера и фронтенд берут образы, собранные и
опубликованные CI. Тег у всех четырех один, `IMAGE_TAG` в `docker/.env`
(`latest` с main, имя ветки с ветки, короткий sha с каждого прогона), потому что
собраны они одним прогоном из одного коммита. Сборка на сервере остается
аварийным путем: compose собирает сам, только если реестр недоступен. Образ,
который CI публикует, но никто не тянет, это то же самое, что отсутствие
доставки.

**Обновление.** Профиль `production` в командах инсталлятора и юнита поднимает
watchtower: раз в пять минут он перечитывает тег и перезапускает контейнеры с
меткой `com.centurylinklabs.watchtower.enable=true`. Пин на конкретный sha
обновления останавливает, тег неподвижен. Миграции watchtower не прогоняет,
контейнер `migration` одноразовый, поэтому релиз со схемой требует
`systemctl restart dm3`.

**Файлы:**
- [`docker/docker-compose.preview.yml`](../../docker/docker-compose.preview.yml)
- [`docker/nginx/nginx.conf`](../../docker/nginx/nginx.conf)
- [`docker/scripts/init-htpasswd.sh`](../../docker/scripts/init-htpasswd.sh)

**Смена пароля:**
```bash
cd /opt/dm3/docker
bash scripts/init-htpasswd.sh                                              # спросит пароль
docker compose -f docker-compose.yml -f docker-compose.preview.yml restart nginx
```

**SSL:** Раскомментировать HTTPS блок в nginx.conf + `certbot --nginx -d yourdomain.com`

### Точка присутствия

Второй публичный адрес сайта обслуживает не второй экземпляр приложения, а
обратный прокси: он отдает фронтенд со своего диска и передает наверх запросы к
API. Секретов на нем нет, к хранилищам он не подключается. Зачем это нужно, чем
не является и какие требования предъявляет к машине — [MIRRORING.md](./MIRRORING.md).

Отсюда для развертывания следуют три вещи. Файл окружения точки присутствия не содержит ни
паролей хранилищ, ни ключа шифрования: без кода приложения они ей не нужны, а
лишний секрет на машине в чужой юрисдикции обесценивает всю схему. Команда
называет сервисы поименно, иначе профиль поднимет весь набор по умолчанию вместе
с хранилищами и миграцией, нацеленной на основную базу. Nginx берет тот же
конфиг, но без `auth_basic` и с включенным HTTPS.

```bash
cd docker
cp .env.example .env.mirror
# Заполнить: публичный адрес точки присутствия и адрес API основного сервера
docker compose --env-file .env.mirror -f docker-compose.yml -f docker-compose.preview.yml -f docker-compose.mirror.yml --profile mirror up -d nginx dmfront watchtower
```

Обновляется точка присутствия так же, как основной стенд: watchtower тянет новый образ
фронтенда по метке контейнера. Сборка интерфейса на точке присутствия обязана совпадать с
той, что отдает основной адрес.

---

## Переменные окружения

**Источник истины:** [`docker/docker-compose.yml`](../../docker/docker-compose.yml) (секция `x-workload-env`)

| Категория | Переменные |
|-----------|------------|
| Базы данных | `DM_ConnectionStrings__Rdb`, `DM_ConnectionStrings__Mongo` |
| RabbitMQ | `DM_RabbitMqConfiguration__*` |
| MinIO (source storage) | `DM_CdnConfiguration__*` |
| imgproxy (transform layer) | `DM_ImageProxyConfiguration__Endpoint/Key/Salt/SourceUrlPrefix` |
| Email | `DM_EmailConfiguration__*` |

**Production:** `docker/.env` создает `docker/scripts/init-env.sh server` — копирует пример, генерирует крипто-ключ и все секреты (`POSTGRES_PASSWORD`, `RABBITMQ_DEFAULT_PASS`, `MINIO_ROOT_PASSWORD`, `MONGO_*_PASSWORD`, `MINIO_APP_PASSWORD`, `MINIO_IMGPROXY_PASSWORD`, `IMGPROXY_KEY`, `IMGPROXY_SALT`, `GF_SECURITY_ADMIN_PASSWORD`), ставит `ASPNETCORE_ENVIRONMENT=Production` и пинит `IMAGE_TAG`. Копия `.env.example` руками оставляет пароли из репозитория и пустой ключ, на котором compose останавливается до старта контейнеров.

---

## Откат

| Метод | Команда |
|-------|---------|
| Docker образ | `IMAGE_TAG=<sha>` в `docker/.env`, затем `sudo systemctl restart dm3` |
| PostgreSQL | `gunzip -c /var/backups/postgresql/<файл>.sql.gz \| docker exec -i dm-pg psql -U postgres dm3` |
| MongoDB | `docker exec -i dm-mongo mongorestore --archive --gzip --drop < /var/backups/mongodb/<файл>.archive.gz` |
| MinIO | `docker run --rm --network host -v /var/backups/minio/<каталог>:/backup --entrypoint sh minio/mc -c 'mc alias set dst "$MINIO_ENDPOINT" "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" && mc mirror /backup dst/dm-uploads'` |
| Git | `git revert HEAD && git push` |

Команды восстановления соответствуют тому, что кладут скрипты бэкапа: PostgreSQL
и MongoDB сжаты gzip-ом, а MinIO — каталог объектов, а не архив. Прежние команды
в этой таблице выполниться не могли: psql получал gzip вместо SQL, а mongorestore
искал архив по пути внутри контейнера, куда он не смонтирован, и без `--gzip`.

---

## Масштабирование

**Вертикальное:** `deploy.resources.limits` (cpus, memory) — настроено для всех сервисов.

**Горизонтальное — API в одном экземпляре, и это ограничение, а не недоделка.**
Внутри процесса API живут периодические задания (чистки, дайджесты, напоминания),
и выборов лидера между экземплярами нет: второй экземпляр выполнит ту же работу
второй раз. Часть заданий идемпотентна, часть нет, и разбирать это по одному
дешевле не станет. Поэтому `container_name` фиксирует единственность на уровне
compose — поднять реплики нельзя даже случайно.

Снимается это одним из двух способов, и оба — отдельная работа:
вынести задания в собственный процесс (как уже сделано с почтой и уведомлениями),
либо завести распределенную блокировку и брать ее перед каждым запуском.
Пока ни того ни другого нет, `deploy.replicas` для API — заявка, которую нечем
обеспечить.

**БД:** Connection pooling; размер пула задается строкой подключения в `x-workload-env` (`docker/docker-compose.yml`) и здесь не дублируется.

---

## Мониторинг

**Prometheus:** http://localhost:9090 — метрики всех сервисов (API + consumers + PostgreSQL + Node)

**Grafana:** http://localhost:3000 — 3 dashboard'а (API Overview, Infrastructure, Consumers) настроены автоматически

**Jaeger:** http://localhost:16686 — distributed tracing (OTLP gRPC)

**Alerting:** `docker/prometheus/alerts.yml` — правила вычисляет Prometheus, доставляет alertmanager (http://localhost:9093) почтой на тот же relay, что и письма приложения; получатель и его настройки — в [MONITORING.md](MONITORING.md#alerting)

---

## Бэкапы

**Скрипты:** `docker/scripts/backup-postgres.sh`, `backup-mongodb.sh`, `backup-minio.sh`

**Проверка:** `docker/scripts/verify-backup.sh` — возраст, размер, целостность gzip

**Retention:** 30 дней для всех типов бэкапов

---

## Health Checks

Адреса и что проверяет каждый — [CONFIGURATION.md](../references/CONFIGURATION.md#health-endpoints).

---

## Ссылки

- [Установка](./LOCAL_SETUP.md) — локальная разработка
- [Тестирование](./TESTING.md) — тесты и CI
- [Конфигурация](../references/CONFIGURATION.md) — все настройки

