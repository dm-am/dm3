# Хостинг с защитой паролем

## Быстрый старт (локально)

### 1. Создание пароля

```bash
# Создаёт файл .htpasswd с пользователем "preview"
docker run --rm httpd htpasswd -nb preview ВашПароль > docker/nginx/.htpasswd
```

### 2. Запуск

```bash
cd docker
docker-compose -f docker-compose.yml -f docker-compose.preview.yml up -d --build
```

### 3. Доступ

Откройте http://localhost и введите логин/пароль.

---

## Хостинг на VPS

### Вариант 1: Oracle Cloud (бесплатно)

1. Зарегистрируйтесь на [Oracle Cloud](https://cloud.oracle.com/free)
2. Создайте VM (Always Free - 2 AMD или 4 ARM ядра)
3. Установите Docker:
   ```bash
   sudo apt update && sudo apt install -y docker.io docker-compose
   sudo usermod -aG docker $USER
   ```
4. Клонируйте репозиторий и запустите

### Вариант 2: Hetzner Cloud (~4€/мес)

1. Создайте сервер CX11 на [Hetzner](https://hetzner.cloud)
2. Используйте образ Docker CE
3. Клонируйте и запустите

### Вариант 3: Cloudflare Tunnel (защита локального сервера)

1. Установите [cloudflared](https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/)
2. Авторизуйтесь: `cloudflared tunnel login`
3. Создайте туннель: `cloudflare tunnel create dm-preview`
4. Добавьте Access Policy в Cloudflare Dashboard

---

## HTTPS (продакшен)

Для HTTPS добавьте Certbot или используйте Cloudflare Proxy.

### С Certbot:

```yaml
# docker-compose.preview.yml
services:
  nginx:
    volumes:
      - ./certbot/conf:/etc/letsencrypt:ro
      - ./certbot/www:/var/www/certbot:ro

  certbot:
    image: certbot/certbot
    volumes:
      - ./certbot/conf:/etc/letsencrypt
      - ./certbot/www:/var/www/certbot
```

---

## Управление пользователями

```bash
# Добавить пользователя
docker run --rm httpd htpasswd -nb user2 password >> docker/nginx/.htpasswd

# Удалить пользователя
sed -i '/^user2:/d' docker/nginx/.htpasswd

# Перезагрузить nginx
docker-compose restart nginx
```
