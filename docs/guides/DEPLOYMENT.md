# Развертывание DM3

Как поставить, обновить и откатить стенд на сервере. Команду установки, запрет копировать `.env.example` на точку присутствия и команду восстановления хранилища сверяют `DeploymentConfigurationShould` и `StorageConfigurationShould`, они же держат правила стенда по файлам `docker/`. Остальное держит ревью.

Состав стенда и путь запроса через край описаны в [SYSTEM.md](../architecture/SYSTEM.md), раздел "Топология рантайма".

## CI/CD (GitHub Actions)

Сборка, тесты и публикация образов идут в `.github/workflows/dotnet.yml` на push и PR в `main` и `dev`, на теги `v*` и раз в сутки. Сканы безопасности `security.yml` (CodeQL, baseline OWASP ZAP) вызываются из этого графа, и публикация образов их ждет. Раз в неделю `security.yml` запускается и сам по себе, полным сканом. ZAP сканирует API напрямую, без края и SPA, поэтому HTML-документ и заголовки края этим гейтом не проверяются.

Образы уходят в GHCR под префиксом `IMAGE_PREFIX`, и имя каждого обязано совпадать с тем, что тянет `docker/docker-compose.yml`. Каждый образ получает тег из короткого sha коммита без приставки (пустой `prefix` у `docker/metadata-action`), на этот формат опирается откат. Подвижные имена (`latest` и `main` с main, `dev` с dev, имя релизного тега) ставит джоба `promote-images` после публикации всех образов прогона.

## Docker Build

Бэкенд собирается по [`docker/app.Dockerfile`](../../docker/app.Dockerfile) и работает от пользователя `dmuser`, фронтенд по [`src/DM.Web.Client/Dockerfile`](../../src/DM.Web.Client/Dockerfile). Кэш сборки между прогонами есть только у джоб публикации (`type=gha`), проверочные джобы собирают образ через `docker compose up --build` с нуля.

## Развертывание на VPS

### Автоматическая установка

```bash
export DM_PREVIEW_PASSWORD=ваш_пароль
DM_REF=<тег>
curl -sSL https://raw.githubusercontent.com/dm-am/dm3/$DM_REF/docker/setup-server.sh | DM_REF=$DM_REF bash
```

`DM_REF` это тег релиза, он же выбирает тег образов. Ветка тоже принимается, но тогда одна и та же команда в разные дни дает разные стенды, и установщик говорит об этом, называя коммит. Боевой стенд ставится только с тега.

Без `DM_PREVIEW_PASSWORD` установщик останавливается. Это пароль Basic Auth с логином `preview`, за которым стенд отвечает по `http://<IP>`, пока нет домена. Файл `docker/nginx/.htpasswd` в репозитории не хранится, его создает на сервере [`docker/scripts/init-htpasswd.sh`](../../docker/scripts/init-htpasswd.sh). Остальные шаги установщика перечислены по порядку в [`docker/setup-server.sh`](../../docker/setup-server.sh).

### Ручная установка

```bash
git clone https://github.com/dm-am/dm3.git && cd dm3/docker
bash scripts/init-env.sh server
docker compose --profile production -f docker-compose.yml -f docker-compose.preview.yml up -d --wait storage seaweedfs
docker compose --profile production -f docker-compose.yml -f docker-compose.preview.yml up -d
```

Хранилище поднимается первым и с `--wait`, как в установщике: сайт стартует и без него, и несозданный бакет иначе виден только по ошибке 500 на каждой загрузке. Локальный стенд описан в [LOCAL_SETUP.md](./LOCAL_SETUP.md).

### Preview окружение

Сайт целиком живет в оверлее: базовый compose дает инфраструктуру и API, [`docker/docker-compose.preview.yml`](../../docker/docker-compose.preview.yml) добавляет nginx и контейнер SPA. Поэтому любая команда, поднимающая или гасящая боевой стенд, называет оба файла, и установщик, и юнит `docker/dm3.service`, иначе перезапуск подменяет сайт голым API. Расхождение ловит `scripts/deployment/check-compose-topology.sh`.

Стенд поднимается без `--build`, из образов, опубликованных CI, с одним тегом `IMAGE_TAG` из `docker/.env` на все приложения, потому что собраны они одним прогоном. Сборка на сервере остается аварийным путем, когда реестр недоступен.

Пароль Basic Auth меняется тем же скриптом. Скрипт заменяет файл новым, а контейнер nginx видит прежний, пока его не перезапустят.

```bash
cd /opt/dm3/docker
bash scripts/init-htpasswd.sh                                              # спросит пароль
docker compose -f docker-compose.yml -f docker-compose.preview.yml restart nginx
```

### Обновление стенда

Профиль `production` поднимает watchtower: раз в пять минут он перечитывает тег и перезапускает контейнеры с меткой `com.centurylinklabs.watchtower.enable=true`. Пин `IMAGE_TAG` на sha обновления останавливает.

Миграции watchtower не прогоняет, одноразовый контейнер `migration` отрабатывает только на подъеме стенда. Пока действует правило единственной `InitialCreate` ([DATA_STORAGE.md](../conventions/DATA_STORAGE.md), раздел "Миграции"), схема не доезжает и с перезапуском: пересозданная миграция уже числится в `__EFMigrationsHistory`, и API поднимается на старой схеме. До триггера из того же раздела схему на стенде обновляет только полный ресет базы.

Повторный запуск установщика обновлением не служит: в непустом каталоге установки он отказывается работать, ничего не изменив. Обновляются три вещи по отдельности.

- Образы приложений носит watchtower.
- Дерево на сервере обновляет оператор, затем `systemctl restart dm3`: `up -d` контейнеры, читающие конфигурацию с диска, не пересоздает.
- Файл окружения, учетные данные края и локальные правки конфигурации оператор ведет на месте. Fetch с merge установщик не делает намеренно, он снес бы эти правки без свидетелей.

После подмены контейнеров стенд проверяется прогоном `docker/scripts/smoke-edge.sh` против публичного адреса.

Какой релиз писал лог, видно из свойства `Release` каждой строки: CI передает коммит в сборку аргументом `SOURCE_REVISION`. Образ, собранный без него (локальный `docker compose up --build`), пишет `unknown`.

### Край держит адреса с момента старта

Имена контейнеров nginx разрешает один раз, при разборе конфигурации, без `resolver` намеренно (причина в [`docker/nginx/nginx.conf`](../../docker/nginx/nginx.conf)). Поэтому пересоздание любого контейнера за краем завершается пересозданием самого края, иначе край отвечает 502 на адрес, которого больше нет. Для образов приложений это делает watchtower по меткам `enable` и `depends-on` на сервисе nginx. Для остального (хранилище `storage`, рендерер картинок, ручной `--force-recreate`, смена публичного адреса) это обязанность того, кто пересоздает.

### HTTPS

Появляется вместе с доменом, до него стенд отвечает по http. Самоподписанного сертификата не будет: предупреждение браузера учит кликать сквозь предупреждения.

```bash
cd /opt/dm3/docker
bash scripts/init-ssl.sh <домен>            # выпуск через webroot, край не останавливается
sudo systemctl restart dm3
sudo certbot renew --dry-run                # продление отрабатывает без участия оператора
```

Почему webroot, а не плагин `--nginx`, сказано в [`docker/scripts/init-ssl.sh`](../../docker/scripts/init-ssl.sh). Редирект на https скрипт вешает на имя домена, и по IP стенд отвечает как раньше.

### Точка присутствия

Второй публичный адрес обслуживает обратный прокси без приложения и без секретов, он описан в [POINT_OF_PRESENCE.md](./POINT_OF_PRESENCE.md) вместе с командой запуска. Файл окружения на нем создается из `docker/.env.pop.example`, а не из `docker/.env.example`: тот везет на машину в чужой юрисдикции все пароли из репозитория. Край точки присутствия подчиняется тому же правилу края.

## Переменные окружения

Переменные приложения задает якорь `x-workload-env` в [`docker/docker-compose.yml`](../../docker/docker-compose.yml). Серверный `docker/.env` создает `docker/scripts/init-env.sh server`: генерирует ключ шифрования и все секреты, ставит `ASPNETCORE_ENVIRONMENT=Production` и пинит `IMAGE_TAG`. Копия `docker/.env.example` руками оставляет пароли из репозитория и пустой ключ, на котором compose не стартует.

Имя бакета после установки не меняется: `STORAGE_BUCKET` задают один раз, до первого старта. На томе с данными другое имя создает второй, пустой бакет, healthcheck остается зеленым, а старые картинки перестают открываться. Переименование бакета и переезд в другое S3-хранилище идут как восстановление из бэкапа.

## Откат

| Метод | Команда |
|-------|---------|
| Docker образ | `IMAGE_TAG=<sha>` в `docker/.env`, затем `sudo systemctl restart dm3` |
| PostgreSQL | `gunzip -c /var/backups/postgresql/<файл>.sql.gz \| docker exec -i dm-pg psql -U postgres dm3` |
| Объектное хранилище | команда под таблицей |

```bash
cd /opt/dm3/docker && . scripts/_env.sh
(
  export AWS_ACCESS_KEY_ID="${STORAGE_APP_USER:-dm-app}" AWS_SECRET_ACCESS_KEY="$STORAGE_APP_PASSWORD"
  docker run --rm --network host -v /var/backups/storage/<каталог>:/backup \
    -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY \
    -e AWS_DEFAULT_REGION=us-east-1 -e AWS_REQUEST_CHECKSUM_CALCULATION=when_required \
    --entrypoint sh amazon/aws-cli:2.37.9@sha256:92de75724b6a746951f0e8b915d86bbccd7cb55aff96cd0cb4f7017272160780 \
    -c 'aws configure set default.s3.addressing_style path && exec aws "$@"' aws \
    s3 cp /backup "s3://${STORAGE_BUCKET:-dm-uploads}" --recursive --endpoint-url http://127.0.0.1:9000 \
    --cache-control "public, max-age=31536000, immutable"
)
```

Хранилище восстанавливается ключом `dm-app` командой `cp --recursive`, а не `sync`: `sync` читает список бакета, а права List у ключа приложения нет. Ключ уходит в контейнер по имени переменной, потому что значение в аргументе `docker run` видно в `ps` всем пользователям сервера, а скобки не дают ему заменить ключи выносной копии в оболочке оператора. Тип файла aws-cli берет по расширению, каталог бэкапа метаданных не хранит.

## Масштабирование

API работает в одном экземпляре, и `container_name` фиксирует это на уровне compose. В процессе API живут периодические задания (чистки, дайджесты, напоминания) без выборов лидера, и второй экземпляр выполнил бы их второй раз, а идемпотентны они не все. Снимается ограничение выносом заданий в свой процесс, как сделано с почтой и уведомлениями, либо распределенной блокировкой.

Realtime-пуш второму экземпляру не мешает: очередь пуша эксклюзивная, и каждый экземпляр получает свою на том же обменнике.

## Мониторинг

Адреса Prometheus, Grafana, Jaeger и alertmanager, дашборды и получатель алертов описаны в [MONITORING.md](MONITORING.md), правила алертов лежат в `docker/prometheus/alerts.yml`.

## Бэкапы

Ночные задания ставит `docker/scripts/install-cron.sh`: `backup-postgres.sh`, `backup-storage.sh` (ключом `dm-backup`, который только читает и перечисляет) и `verify-backup.sh`, который проверяет возраст, размер и целостность копий. Срок хранения задает `RETENTION_DAYS`, по умолчанию 30 дней.

Границы хранилища проверяет `docker/scripts/check-storage.sh` в джобе `deployment-smoke`, и новый тег или дайджест SeaweedFS принимается только при ее зеленом прогоне.

## Ссылки

- [Установка](./LOCAL_SETUP.md), локальная разработка
- [Тестирование](./TESTING.md), тесты и CI
- [Конфигурация](../references/CONFIGURATION.md), все настройки и [проверки здоровья](../references/CONFIGURATION.md#health-endpoints)
