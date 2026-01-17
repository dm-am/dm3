# Preview Hosting

## Текущий доступ

**URL:** https://progress-encyclopedia-accurate-nissan.trycloudflare.com

**Basic Auth (nginx):**
- Логин: `preview`
- Пароль: `dm2026preview`

## Тестовые аккаунты

| Логин | Пароль | Роль |
|-------|--------|------|
| SolohinLex | `Lex2026Admin!` | Админ |
| Rayzen | `Rayzen2026!` | Пользователь |
| Min0tavr | `Min0tavr2026!` | Пользователь |
| user0000000000000000 | `Admin2026!` | Админ |

## Запуск локально

### 1. Запуск контейнеров
```bash
cd docker
docker-compose -f docker-compose.yml -f docker-compose.preview.yml up -d --build
```

### 2. Запуск Cloudflare Tunnel
```bash
# Windows (полный путь)
"/c/Program Files (x86)/cloudflared/cloudflared.exe" tunnel --url http://localhost:80

# Или если в PATH
cloudflared tunnel --url http://localhost:80
```

Туннель выдаст временный URL вида `https://xxx-xxx-xxx.trycloudflare.com`

### 3. Остановка
```bash
# Остановить туннель: Ctrl+C в терминале

# Остановить контейнеры
cd docker
docker-compose -f docker-compose.yml -f docker-compose.preview.yml down
```

## Управление паролями

### Изменить пароль nginx basic auth
```bash
docker run --rm httpd htpasswd -nb preview НОВЫЙ_ПАРОЛЬ > docker/nginx/.htpasswd
docker exec dm-nginx nginx -s reload
```

### Добавить пользователя в БД
```bash
# Генерация хеша пароля (Node.js)
node -e "
const crypto = require('crypto');
const saltBytes = crypto.randomBytes(75);
const salt = saltBytes.toString('base64');
const password = 'ВАШ_ПАРОЛЬ';
const buffer = Buffer.concat([Buffer.from(password, 'utf8'), saltBytes]);
const hash = crypto.createHash('sha256').update(buffer).digest().toString('base64');
console.log('Salt: ' + salt);
console.log('Hash: ' + hash);
"

# Вставка в БД
docker exec dm-pg psql -U postgres -d dm3.5 -c "
INSERT INTO \"Users\" (
  \"UserId\", \"Login\", \"Email\", \"RegistrationDate\", \"LastVisitDate\",
  \"Role\", \"AccessPolicy\", \"Salt\", \"PasswordHash\",
  \"RatingDisabled\", \"QualityRating\", \"QuantityRating\",
  \"Activated\", \"CanMerge\", \"IsRemoved\", \"IsHonorary\", \"Gender\"
) VALUES (
  gen_random_uuid(), 'ЛОГИН', 'email@test.com', NOW(), NOW(),
  1, 0, 'SALT_СЮДА', 'HASH_СЮДА',
  false, 0, 0, true, false, false, false, 0
);
"
```

### Роли пользователей
- `1` = RegularUser
- `5` = Admin

## Файлы конфигурации

- `docker/docker-compose.preview.yml` — compose для preview
- `docker/nginx/nginx.conf` — конфиг nginx reverse proxy
- `docker/nginx/.htpasswd` — пароли basic auth
- `frontend/DM.Web.Modern.Temp/Dockerfile` — сборка фронтенда

## Примечания

- Туннель Cloudflare временный, URL меняется при перезапуске
- Для постоянного URL нужен аккаунт Cloudflare и named tunnel
- Basic auth защищает весь сайт, включая API
