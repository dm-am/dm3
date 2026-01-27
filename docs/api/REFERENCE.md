# API Reference - DM3

**Created:** 2026-01-27
**Updated:** 2026-01-27

## Содержание

- [Введение](#введение)
- [Базовая информация](#базовая-информация)
- [Форматы ответов](#форматы-ответов)
- [Аутентификация](#аутентификация)
  - [OAuth 2.0 Token](#oauth-20-token)
- [Account (Аккаунт)](#account-аккаунт)
  - [Регистрация и активация](#регистрация-и-активация)
  - [Вход и выход](#вход-и-выход)
  - [Управление паролем](#управление-паролем)
  - [Приглашения пользователя](#приглашения-пользователя)
- [Community (Сообщество)](#community-сообщество)
  - [Пользователи](#пользователи)
  - [Опросы](#опросы)
  - [Отзывы о сайте](#отзывы-о-сайте)
- [Forum (Форум)](#forum-форум)
  - [Доски и разделы](#доски-и-разделы)
  - [Темы](#темы)
  - [Комментарии](#комментарии)
- [Game (Игры)](#game-игры)
  - [Управление играми](#управление-играми)
  - [Персонажи](#персонажи)
  - [Комнаты](#комнаты)
  - [Посты](#посты)
  - [Приглашения в игру](#приглашения-в-игру)
- [Messaging (Сообщения)](#messaging-сообщения)
  - [Глобальный чат](#глобальный-чат)
  - [Диалоги](#диалоги)
  - [Сообщения](#сообщения-1)
- [Common (Общее)](#common-общее)
  - [Поиск](#поиск)

---

## Введение

DM3 API - это RESTful API для платформы текстовых ролевых игр. API использует JSON для обмена данными и OAuth 2.0 для аутентификации.

## Базовая информация

- **Base URL:** `https://api.example.com` (замените на актуальный URL)
- **API Version:** v1
- **Формат данных:** JSON
- **Кодировка:** UTF-8
- **Аутентификация:** OAuth 2.0 Bearer Token

### Rate Limiting

API использует rate limiting для предотвращения злоупотреблений:
- **Глобальный лимит:** 100 запросов в минуту на IP
- **Эндпоинты аутентификации:** 5 запросов в минуту на IP
- **HTTP Status Code:** `429 Too Many Requests` при превышении лимита

## Форматы ответов

### Envelope<T>

Единичный объект оборачивается в конверт:

```json
{
  "resource": {
    // объект данных
  },
  "metadata": {
    // дополнительные метаданные (опционально)
  }
}
```

### ListEnvelope<T>

Списки объектов оборачиваются в конверт с пагинацией:

```json
{
  "resources": [
    // массив объектов
  ],
  "paging": {
    "number": 1,
    "size": 10,
    "totalPages": 5,
    "totalEntities": 50
  }
}
```

### Коды ошибок

- `200 OK` - успешный запрос
- `201 Created` - ресурс создан
- `204 No Content` - успешное удаление/операция без содержимого
- `400 Bad Request` - невалидные параметры
- `401 Unauthorized` - требуется аутентификация
- `403 Forbidden` - недостаточно прав
- `404 Not Found` - ресурс не найден
- `409 Conflict` - конфликт состояния
- `410 Gone` - ресурс удален или не существует
- `429 Too Many Requests` - превышен rate limit

### Формат ошибки

```json
{
  "message": "Описание ошибки",
  "errors": {
    "fieldName": ["Ошибка валидации поля"]
  }
}
```

---

## Аутентификация

### OAuth 2.0 Token

#### POST /connect/token

Получение access token и refresh token.

**Rate Limit:** 5 req/min

**Request Body (Password Grant):**

```json
{
  "grant_type": "password",
  "username": "user_login",
  "password": "user_password"
}
```

**Request Body (Refresh Token Grant):**

```json
{
  "grant_type": "refresh_token",
  "refresh_token": "your_refresh_token"
}
```

**Response 200:**

```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "def50200..."
}
```

**Errors:**
- `403` - Неверные учетные данные, неактивный или заблокированный пользователь

---

#### GET /connect/userinfo

Получение информации о текущем пользователе через OAuth 2.0.

**Auth Required:** Yes

**Response 200:**

```json
{
  "sub": "user-id",
  "name": "username",
  "role": "Player"
}
```

---

## Account (Аккаунт)

### Регистрация и активация

#### POST /v1/account

Регистрация нового пользователя.

**Rate Limit:** 5 req/min

**Request Body:**

```json
{
  "login": "username",
  "email": "user@example.com",
  "password": "SecurePassword123",
  "website": ""
}
```

**Note:** Поле `website` - honeypot для защиты от ботов, должно быть пустым.

**Response 201:** `Location: /v1/users/username`

**Errors:**
- `400` - Невалидные данные регистрации

---

#### PUT /v1/account/{token}

Активация зарегистрированного пользователя по токену из email.

**Path Parameters:**
- `token` (Guid) - токен активации

**Response 200:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username",
    "email": "user@example.com",
    "role": "Player",
    "online": true,
    "rating": {
      "quality": 0,
      "quantity": 0
    }
  }
}
```

**Errors:**
- `400` - Невалидный токен
- `410` - Токен истек

---

#### GET /v1/account

Получение информации о текущем пользователе.

**Auth Required:** Yes

**Response 200:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username",
    "email": "user@example.com",
    "role": "Player",
    "settings": { /* настройки */ },
    "features": ["feature1", "feature2"]
  }
}
```

---

### Вход и выход

#### POST /v1/account/login

Вход через логин и пароль (legacy endpoint, рекомендуется использовать OAuth 2.0).

**Rate Limit:** 5 req/min

**Request Body:**

```json
{
  "login": "username",
  "password": "password",
  "website": ""
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username",
    "role": "Player"
  }
}
```

**Errors:**
- `400` - Неверный логин или пароль
- `403` - Пользователь неактивен или заблокирован

---

#### DELETE /v1/account/login

Выход из системы (удаление текущей сессии).

**Auth Required:** Yes

**Response 204:** No Content

---

#### DELETE /v1/account/login/all

Выход со всех устройств (удаление всех сессий пользователя).

**Auth Required:** Yes

**Response 204:** No Content

---

### Управление паролем

#### POST /v1/account/password

Сброс пароля (отправка токена восстановления на email).

**Rate Limit:** 5 req/min

**Request Body:**

```json
{
  "login": "username",
  "email": "user@example.com"
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username"
  }
}
```

**Errors:**
- `400` - Неверные данные аккаунта

---

#### PUT /v1/account/password

Изменение пароля по токену восстановления или старому паролю.

**Request Body:**

```json
{
  "token": "reset-token",
  "newPassword": "NewSecurePassword123"
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username"
  }
}
```

**Errors:**
- `400` - Невалидный токен или пароль

---

#### PUT /v1/account/email

Изменение email пользователя.

**Auth Required:** Yes

**Request Body:**

```json
{
  "email": "newemail@example.com",
  "password": "current_password"
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "user-id",
    "email": "newemail@example.com"
  }
}
```

---

### Приглашения пользователя

#### GET /v1/invitations

Получение всех активных приглашений для текущего пользователя.

**Auth Required:** Yes

**Response 200:**

```json
{
  "resources": [
    {
      "id": "token-id",
      "gameId": "game-id",
      "gameTitle": "Название игры",
      "invitationType": "Player",
      "expiresAt": "2026-02-01T12:00:00Z"
    }
  ]
}
```

---

#### PUT /v1/invitations/{tokenId}/accept

Принятие приглашения.

**Auth Required:** Yes

**Path Parameters:**
- `tokenId` (Guid) - ID токена приглашения

**Response 204:** No Content

**Errors:**
- `404` - Приглашение не найдено
- `410` - Приглашение истекло или уже обработано

---

#### PUT /v1/invitations/{tokenId}/reject

Отклонение приглашения.

**Auth Required:** Yes

**Path Parameters:**
- `tokenId` (Guid) - ID токена приглашения

**Response 204:** No Content

**Errors:**
- `404` - Приглашение не найдено
- `410` - Приглашение истекло или уже обработано

---

## Community (Сообщество)

### Пользователи

#### GET /v1/users

Получение списка активированных пользователей.

**Query Parameters:**
- `q.skip` (int) - количество пропускаемых записей
- `q.size` (int) - размер страницы (default: 10)

**Response 200:**

```json
{
  "resources": [
    {
      "id": "user-id",
      "login": "username",
      "role": "Player",
      "online": true,
      "rating": {
        "quality": 100,
        "quantity": 50
      },
      "lastVisitDate": "2026-01-27T10:00:00Z"
    }
  ],
  "paging": {
    "number": 1,
    "size": 10,
    "totalPages": 5,
    "totalEntities": 50
  }
}
```

---

#### GET /v1/users/by-role/{role}

Получение пользователей по роли.

**Path Parameters:**
- `role` (enum) - роль пользователя: `Guest`, `Player`, `SeniorPlayer`, `Administrator`, `Moderator`, `NannyModerator`, `RegularModerator`, `SeniorModerator`

**Response 200:**

```json
{
  "resources": [
    {
      "id": "user-id",
      "login": "username",
      "role": "Administrator"
    }
  ]
}
```

---

#### GET /v1/users/me

Получение информации о текущем авторизованном пользователе.

**Auth Required:** Yes

**Response 200:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username",
    "email": "user@example.com",
    "role": "Player",
    "settings": {
      "colorSchema": "Modern",
      "nsfwFilter": "Hide",
      "paging": {
        "postsPerPage": 20,
        "commentsPerPage": 20,
        "topicsPerPage": 20,
        "messagesPerPage": 20
      }
    }
  }
}
```

---

#### GET /v1/users/{id}

Получение пользователя по ID.

**Path Parameters:**
- `id` (Guid) - ID пользователя

**Response 200:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username",
    "role": "Player",
    "online": true,
    "rating": {
      "quality": 100,
      "quantity": 50
    }
  }
}
```

**Errors:**
- `410` - Пользователь не найден

---

#### GET /v1/users/by-login/{login}

Получение пользователя по логину.

**Path Parameters:**
- `login` (string) - логин пользователя

**Response 200:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username",
    "role": "Player"
  }
}
```

**Errors:**
- `410` - Пользователь не найден

---

#### GET /v1/users/{login}/details

Получение детальной информации о пользователе.

**Path Parameters:**
- `login` (string) - логин пользователя

**Response 200:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username",
    "role": "Player",
    "info": {
      "aboutText": "Информация о себе",
      "icq": "123456789",
      "skype": "username"
    },
    "status": "Active",
    "registrationDate": "2025-01-01T00:00:00Z",
    "lastVisitDate": "2026-01-27T10:00:00Z",
    "rating": {
      "quality": 100,
      "quantity": 50
    }
  }
}
```

---

#### PATCH /v1/users/{login}/details

Обновление информации о пользователе.

**Auth Required:** Yes

**Path Parameters:**
- `login` (string) - логин пользователя

**Request Body:**

```json
{
  "info": {
    "aboutText": "Новая информация о себе",
    "icq": "987654321",
    "skype": "new_username"
  }
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username",
    "info": {
      "aboutText": "Новая информация о себе"
    }
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Нет прав на изменение
- `410` - Пользователь не найден

---

#### GET /v1/users/{login}/settings

Получение настроек пользователя.

**Path Parameters:**
- `login` (string) - логин пользователя

**Response 200:**

```json
{
  "resource": {
    "colorSchema": "Modern",
    "nsfwFilter": "Hide",
    "paging": {
      "postsPerPage": 20,
      "commentsPerPage": 20,
      "topicsPerPage": 20,
      "messagesPerPage": 20
    }
  }
}
```

---

#### PATCH /v1/users/{login}/settings

Обновление настроек пользователя.

**Auth Required:** Yes

**Path Parameters:**
- `login` (string) - логин пользователя

**Request Body:**

```json
{
  "colorSchema": "Classic",
  "nsfwFilter": "Show",
  "paging": {
    "postsPerPage": 30
  }
}
```

**Response 200:**

```json
{
  "resource": {
    "colorSchema": "Classic",
    "nsfwFilter": "Show",
    "paging": {
      "postsPerPage": 30,
      "commentsPerPage": 20,
      "topicsPerPage": 20,
      "messagesPerPage": 20
    }
  }
}
```

---

### Опросы

#### GET /v1/polls

Получение списка глобальных опросов.

**Query Parameters:**
- `q.skip` (int) - количество пропускаемых записей
- `q.size` (int) - размер страницы

**Response 200:**

```json
{
  "resources": [
    {
      "id": "poll-id",
      "title": "Название опроса",
      "question": "Текст вопроса",
      "options": [
        {
          "id": "option-id-1",
          "text": "Вариант 1",
          "votesCount": 10
        },
        {
          "id": "option-id-2",
          "text": "Вариант 2",
          "votesCount": 5
        }
      ],
      "endDate": "2026-02-01T00:00:00Z",
      "isActive": true
    }
  ],
  "paging": {
    "number": 1,
    "size": 10,
    "totalPages": 2,
    "totalEntities": 15
  }
}
```

---

#### POST /v1/polls/global

Создание нового глобального опроса.

**Auth Required:** Yes (Administrator/Moderator)

**Request Body:**

```json
{
  "title": "Название опроса",
  "question": "Текст вопроса",
  "options": [
    { "text": "Вариант 1" },
    { "text": "Вариант 2" },
    { "text": "Вариант 3" }
  ],
  "endDate": "2026-02-01T00:00:00Z"
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "poll-id",
    "title": "Название опроса",
    "question": "Текст вопроса",
    "options": [
      {
        "id": "option-id-1",
        "text": "Вариант 1",
        "votesCount": 0
      }
    ]
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Нет прав на создание опросов

---

#### GET /v1/polls/{id}

Получение опроса по ID.

**Path Parameters:**
- `id` (Guid) - ID опроса

**Response 200:**

```json
{
  "resource": {
    "id": "poll-id",
    "title": "Название опроса",
    "question": "Текст вопроса",
    "options": [
      {
        "id": "option-id-1",
        "text": "Вариант 1",
        "votesCount": 10
      }
    ],
    "endDate": "2026-02-01T00:00:00Z"
  }
}
```

**Errors:**
- `410` - Опрос не найден

---

#### PATCH /v1/polls/{id}

Обновление опроса.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID опроса

**Request Body:**

```json
{
  "title": "Обновленное название",
  "endDate": "2026-03-01T00:00:00Z"
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "poll-id",
    "title": "Обновленное название",
    "endDate": "2026-03-01T00:00:00Z"
  }
}
```

**Errors:**
- `403` - Нет прав на обновление
- `410` - Опрос не найден

---

#### DELETE /v1/polls/{id}

Удаление опроса (мягкое удаление).

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID опроса

**Response 204:** No Content

**Errors:**
- `403` - Нет прав на удаление
- `410` - Опрос не найден

---

#### POST /v1/polls/{id}/vote

Голосование в опросе.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID опроса

**Query Parameters:**
- `optionId` (Guid) - ID варианта ответа

**Response 200:**

```json
{
  "resource": {
    "id": "poll-id",
    "options": [
      {
        "id": "option-id-1",
        "text": "Вариант 1",
        "votesCount": 11
      }
    ]
  }
}
```

**Errors:**
- `403` - Нельзя голосовать в этом опросе
- `410` - Опрос не найден

---

#### DELETE /v1/polls/{id}/vote

Удаление своего голоса из опроса.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID опроса

**Response 200:**

```json
{
  "resource": {
    "id": "poll-id",
    "options": [
      {
        "id": "option-id-1",
        "text": "Вариант 1",
        "votesCount": 10
      }
    ]
  }
}
```

---

### Отзывы о сайте

#### GET /v1/websitereviews

Получение списка отзывов о сайте.

**Query Parameters:**
- `q.skip` (int) - количество пропускаемых записей
- `q.size` (int) - размер страницы

**Response 200:**

```json
{
  "resources": [
    {
      "id": "review-id",
      "author": {
        "id": "user-id",
        "login": "username"
      },
      "text": "Текст отзыва",
      "createdAt": "2026-01-27T10:00:00Z",
      "isApproved": true
    }
  ],
  "paging": {
    "number": 1,
    "size": 10,
    "totalPages": 3,
    "totalEntities": 25
  }
}
```

---

#### POST /v1/websitereviews

Создание нового отзыва о сайте.

**Auth Required:** Yes

**Request Body:**

```json
{
  "text": "Мой отзыв о сайте"
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "review-id",
    "text": "Мой отзыв о сайте",
    "createdAt": "2026-01-27T10:00:00Z",
    "isApproved": false
  }
}
```

**Errors:**
- `400` - Невалидный текст отзыва
- `403` - Нет прав на создание отзыва

---

#### GET /v1/websitereviews/{id}

Получение отзыва по ID.

**Path Parameters:**
- `id` (Guid) - ID отзыва

**Response 200:**

```json
{
  "resource": {
    "id": "review-id",
    "author": {
      "id": "user-id",
      "login": "username"
    },
    "text": "Текст отзыва",
    "createdAt": "2026-01-27T10:00:00Z"
  }
}
```

**Errors:**
- `410` - Отзыв не найден

---

#### PATCH /v1/websitereviews/{id}

Обновление отзыва.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID отзыва

**Request Body:**

```json
{
  "text": "Обновленный текст отзыва"
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "review-id",
    "text": "Обновленный текст отзыва"
  }
}
```

**Errors:**
- `403` - Нет прав на обновление
- `410` - Отзыв не найден

---

#### DELETE /v1/websitereviews/{id}

Удаление отзыва.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID отзыва

**Response 204:** No Content

**Errors:**
- `410` - Отзыв не найден

---

## Forum (Форум)

### Доски и разделы

#### GET /v1/forum

Получение списка всех досок форума.

**Response 200:**

```json
{
  "resources": [
    {
      "id": "board-id",
      "title": "Общий раздел",
      "description": "Описание раздела",
      "topicsCount": 150,
      "commentsCount": 3500,
      "lastTopic": {
        "id": "topic-id",
        "title": "Последняя тема",
        "lastComment": {
          "id": "comment-id",
          "createdAt": "2026-01-27T09:00:00Z"
        }
      }
    }
  ]
}
```

**Cache:** `public, max-age=60`

---

#### DELETE /v1/forum/comments/unread

Отметить все комментарии на форуме как прочитанные.

**Auth Required:** Yes

**Response 204:** No Content

---

### Темы

#### GET /v1/boards/{id}/topics

Получение списка тем в разделе форума.

**Path Parameters:**
- `id` (string) - ID или код доски

**Query Parameters:**
- `q.skip` (int) - количество пропускаемых записей
- `q.size` (int) - размер страницы
- `q.searchQuery` (string) - поисковый запрос

**Response 200:**

```json
{
  "resources": [
    {
      "id": "topic-id",
      "title": "Название темы",
      "author": {
        "id": "user-id",
        "login": "username"
      },
      "createdAt": "2026-01-20T10:00:00Z",
      "commentsCount": 25,
      "likesCount": 5,
      "isPinned": false,
      "isClosed": false,
      "lastComment": {
        "id": "comment-id",
        "createdAt": "2026-01-27T09:00:00Z",
        "author": {
          "id": "user-id",
          "login": "username"
        }
      }
    }
  ],
  "paging": {
    "number": 1,
    "size": 20,
    "totalPages": 8,
    "totalEntities": 150
  }
}
```

**Errors:**
- `400` - Невалидные параметры поиска
- `410` - Раздел не найден

---

#### POST /v1/boards/{id}/topics

Создание новой темы в разделе.

**Auth Required:** Yes

**Path Parameters:**
- `id` (string) - ID или код доски

**Request Body:**

```json
{
  "title": "Название новой темы",
  "text": "Содержимое темы с [b]BBCode[/b] разметкой",
  "attachedPicture": "url-to-image"
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "topic-id",
    "title": "Название новой темы",
    "text": "Содержимое темы",
    "author": {
      "id": "user-id",
      "login": "username"
    },
    "createdAt": "2026-01-27T10:00:00Z"
  }
}
```

**Errors:**
- `400` - Невалидные параметры темы
- `403` - Нет прав на создание тем в этом разделе
- `410` - Раздел не найден

---

#### GET /v1/topics/{id}

Получение темы по ID.

**Path Parameters:**
- `id` (Guid) - ID темы

**Response 200:**

```json
{
  "resource": {
    "id": "topic-id",
    "title": "Название темы",
    "text": "Содержимое темы",
    "author": {
      "id": "user-id",
      "login": "username",
      "role": "Player"
    },
    "createdAt": "2026-01-20T10:00:00Z",
    "updatedAt": "2026-01-21T12:00:00Z",
    "commentsCount": 25,
    "likesCount": 5,
    "isPinned": false,
    "isClosed": false,
    "attachedPicture": "url-to-image"
  }
}
```

**Errors:**
- `410` - Тема не найдена

---

#### PATCH /v1/topics/{id}

Обновление темы.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID темы

**Request Body:**

```json
{
  "title": "Обновленное название",
  "text": "Обновленное содержимое",
  "isPinned": true,
  "isClosed": false
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "topic-id",
    "title": "Обновленное название",
    "text": "Обновленное содержимое",
    "isPinned": true,
    "isClosed": false,
    "updatedAt": "2026-01-27T10:00:00Z"
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Нет прав на изменение темы
- `410` - Тема не найдена

---

#### DELETE /v1/topics/{id}

Удаление темы.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID темы

**Response 204:** No Content

**Errors:**
- `403` - Нет прав на удаление
- `410` - Тема не найдена

---

#### POST /v1/topics/{id}/likes

Добавление лайка к теме.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID темы

**Response 201:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username"
  }
}
```

**Errors:**
- `403` - Нельзя лайкать эту тему
- `409` - Уже лайкнул эту тему
- `410` - Тема не найдена

---

#### DELETE /v1/topics/{id}/likes

Удаление лайка с темы.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID темы

**Response 204:** No Content

**Errors:**
- `409` - Не лайкал эту тему
- `410` - Тема не найдена

---

#### DELETE /v1/topics/{id}/comments/unread

Отметить все комментарии в теме как прочитанные.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID темы

**Response 204:** No Content

**Errors:**
- `410` - Тема не найдена

---

### Комментарии

#### GET /v1/topics/{id}/comments

Получение списка комментариев в теме.

**Path Parameters:**
- `id` (Guid) - ID темы

**Query Parameters:**
- `q.skip` (int) - количество пропускаемых записей
- `q.size` (int) - размер страницы

**Response 200:**

```json
{
  "resources": [
    {
      "id": "comment-id",
      "text": "Текст комментария с [b]BBCode[/b]",
      "author": {
        "id": "user-id",
        "login": "username",
        "role": "Player"
      },
      "createdAt": "2026-01-27T10:00:00Z",
      "updatedAt": null,
      "likesCount": 3,
      "isNew": false
    }
  ],
  "paging": {
    "number": 1,
    "size": 20,
    "totalPages": 2,
    "totalEntities": 25
  }
}
```

**Errors:**
- `410` - Тема не найдена

---

#### POST /v1/topics/{id}/comments

Создание нового комментария в теме.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID темы

**Request Body:**

```json
{
  "text": "Текст нового комментария с [b]BBCode[/b] разметкой"
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "comment-id",
    "text": "Текст нового комментария",
    "author": {
      "id": "user-id",
      "login": "username"
    },
    "createdAt": "2026-01-27T10:00:00Z",
    "likesCount": 0
  }
}
```

**Errors:**
- `400` - Невалидный текст комментария
- `403` - Нет прав на комментирование
- `410` - Тема не найдена

---

#### GET /v1/forum/comments/{id}

Получение комментария по ID.

**Path Parameters:**
- `id` (Guid) - ID комментария

**Response 200:**

```json
{
  "resource": {
    "id": "comment-id",
    "text": "Текст комментария",
    "author": {
      "id": "user-id",
      "login": "username"
    },
    "createdAt": "2026-01-27T10:00:00Z",
    "likesCount": 3
  }
}
```

**Errors:**
- `410` - Комментарий не найден

---

#### PATCH /v1/forum/comments/{id}

Обновление комментария.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID комментария

**Request Body:**

```json
{
  "text": "Обновленный текст комментария"
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "comment-id",
    "text": "Обновленный текст комментария",
    "updatedAt": "2026-01-27T10:30:00Z"
  }
}
```

**Errors:**
- `400` - Невалидный текст
- `403` - Нет прав на изменение
- `410` - Комментарий не найден

---

#### DELETE /v1/forum/comments/{id}

Удаление комментария.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID комментария

**Response 204:** No Content

**Errors:**
- `403` - Нет прав на удаление
- `410` - Комментарий не найден

---

#### POST /v1/forum/comments/{id}/likes

Добавление лайка к комментарию.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID комментария

**Response 201:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username"
  }
}
```

**Errors:**
- `409` - Уже лайкнул этот комментарий
- `410` - Комментарий не найден

---

#### DELETE /v1/forum/comments/{id}/likes

Удаление лайка с комментария.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID комментария

**Response 204:** No Content

**Errors:**
- `409` - Не лайкал этот комментарий
- `410` - Комментарий не найден

---

## Game (Игры)

### Управление играми

#### GET /v1/games

Получение списка игр.

**Query Parameters:**
- `q.skip` (int) - количество пропускаемых записей
- `q.size` (int) - размер страницы
- `q.status` (enum) - статус игры: `Draft`, `RequiresModeration`, `Active`, `Frozen`, `Finished`, `Closed`
- `q.masterId` (Guid) - ID мастера
- `q.tagIds` (Guid[]) - массив ID тегов

**Response 200:**

```json
{
  "resources": [
    {
      "id": "game-id",
      "title": "Название игры",
      "system": "Система (D&D, GURPS, etc.)",
      "setting": "Сеттинг игры",
      "master": {
        "id": "user-id",
        "login": "master_username"
      },
      "status": "Active",
      "createDate": "2026-01-01T00:00:00Z",
      "releaseDate": "2026-01-15T00:00:00Z",
      "charactersCount": 5,
      "readersCount": 20,
      "postsCount": 150,
      "tags": [
        {
          "id": "tag-id",
          "title": "Фэнтези"
        }
      ]
    }
  ],
  "paging": {
    "number": 1,
    "size": 20,
    "totalPages": 10,
    "totalEntities": 200
  }
}
```

**Cache:** `public, max-age=30`

---

#### GET /v1/games/owned

Получение списка игр, принадлежащих текущему пользователю (как мастер или игрок).

**Auth Required:** Yes

**Response 200:**

```json
{
  "resources": [
    {
      "id": "game-id",
      "title": "Название игры",
      "master": {
        "id": "user-id",
        "login": "username"
      },
      "status": "Active",
      "unreadCharactersCount": 2,
      "unreadPostsCount": 10,
      "unreadCommentsCount": 5
    }
  ]
}
```

---

#### GET /v1/games/popular

Получение списка 10 самых популярных игр по количеству читателей.

**Response 200:**

```json
{
  "resources": [
    {
      "id": "game-id",
      "title": "Популярная игра",
      "readersCount": 100,
      "postsCount": 500
    }
  ]
}
```

**Cache:** `public, max-age=60`

---

#### GET /v1/games/tags

Получение списка всех тегов игр.

**Response 200:**

```json
{
  "resources": [
    {
      "id": "tag-id",
      "title": "Фэнтези",
      "category": "Genre"
    },
    {
      "id": "tag-id-2",
      "title": "Средневековье",
      "category": "Setting"
    }
  ]
}
```

**Cache:** `public, max-age=300`

---

#### GET /v1/games/{id}

Получение краткой информации об игре.

**Path Parameters:**
- `id` (Guid) - ID игры

**Response 200:**

```json
{
  "resource": {
    "id": "game-id",
    "title": "Название игры",
    "system": "D&D 5e",
    "setting": "Forgotten Realms",
    "master": {
      "id": "user-id",
      "login": "master_username"
    },
    "status": "Active",
    "charactersCount": 5,
    "readersCount": 20,
    "postsCount": 150
  }
}
```

**Errors:**
- `410` - Игра не найдена

---

#### GET /v1/games/{id}/details

Получение полной информации об игре.

**Path Parameters:**
- `id` (Guid) - ID игры

**Response 200:**

```json
{
  "resource": {
    "id": "game-id",
    "title": "Название игры",
    "system": "D&D 5e",
    "setting": "Forgotten Realms",
    "info": {
      "shortDescription": "Краткое описание",
      "fullDescription": "Полное описание игры с [b]BBCode[/b]",
      "rules": "Правила игры",
      "nsfwContent": false
    },
    "master": {
      "id": "user-id",
      "login": "master_username"
    },
    "assistant": {
      "id": "assistant-id",
      "login": "assistant_username"
    },
    "status": "Active",
    "createDate": "2026-01-01T00:00:00Z",
    "releaseDate": "2026-01-15T00:00:00Z",
    "attributeSchema": {
      "id": "schema-id",
      "title": "D&D 5e Attributes"
    },
    "tags": [
      {
        "id": "tag-id",
        "title": "Фэнтези"
      }
    ],
    "charactersCount": 5,
    "readersCount": 20,
    "postsCount": 150
  }
}
```

**Errors:**
- `410` - Игра не найдена

---

#### GET /v1/games/{id}/notes

Получение заметок игры (доступно только мастеру и ассистенту).

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID игры

**Response 200:**

```json
{
  "resource": {
    "notes": "Приватные заметки мастера об игре"
  }
}
```

**Errors:**
- `403` - Нет прав на просмотр заметок
- `410` - Игра не найдена

---

#### POST /v1/games

Создание новой игры.

**Auth Required:** Yes

**Request Body:**

```json
{
  "title": "Название новой игры",
  "system": "D&D 5e",
  "setting": "Homebrew",
  "info": {
    "shortDescription": "Краткое описание",
    "fullDescription": "Полное описание",
    "rules": "Правила",
    "nsfwContent": false
  },
  "attributeSchemaId": "schema-id",
  "tagIds": ["tag-id-1", "tag-id-2"]
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "game-id",
    "title": "Название новой игры",
    "status": "Draft",
    "master": {
      "id": "user-id",
      "login": "username"
    },
    "createDate": "2026-01-27T10:00:00Z"
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Нет прав на создание игр

---

#### PATCH /v1/games/{id}/details

Обновление информации об игре.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID игры

**Request Body:**

```json
{
  "title": "Обновленное название",
  "info": {
    "shortDescription": "Новое описание"
  },
  "status": "Active"
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "game-id",
    "title": "Обновленное название",
    "info": {
      "shortDescription": "Новое описание"
    },
    "status": "Active"
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Нет прав на изменение
- `410` - Игра не найдена

---

#### PATCH /v1/games/{id}/notes

Обновление заметок игры.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID игры

**Request Body:**

```json
{
  "notes": "Обновленные заметки"
}
```

**Response 200:**

```json
{
  "resource": {
    "notes": "Обновленные заметки"
  }
}
```

**Errors:**
- `403` - Нет прав на изменение заметок
- `410` - Игра не найдена

---

#### DELETE /v1/games/{id}

Удаление игры.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID игры

**Response 204:** No Content

**Errors:**
- `403` - Нет прав на удаление
- `410` - Игра не найдена

---

#### GET /v1/games/{id}/readers

Получение списка читателей игры.

**Path Parameters:**
- `id` (Guid) - ID игры

**Response 200:**

```json
{
  "resources": [
    {
      "id": "user-id",
      "login": "reader_username",
      "online": true
    }
  ]
}
```

**Errors:**
- `410` - Игра не найдена

---

#### POST /v1/games/{id}/readers

Подписка на игру в качестве читателя.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID игры

**Response 201:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username"
  }
}
```

**Errors:**
- `403` - Нельзя подписаться на эту игру
- `409` - Уже подписан на игру
- `410` - Игра не найдена

---

#### DELETE /v1/games/{id}/readers

Отписка от игры.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID игры

**Response 204:** No Content

**Errors:**
- `409` - Не подписан на игру
- `410` - Игра не найдена

---

#### GET /v1/games/{id}/blacklist

Получение черного списка игры.

**Auth Required:** Yes (только мастер/ассистент)

**Path Parameters:**
- `id` (Guid) - ID игры

**Response 200:**

```json
{
  "resources": [
    {
      "id": "user-id",
      "login": "blocked_username"
    }
  ]
}
```

**Errors:**
- `403` - Нет прав на просмотр черного списка
- `410` - Игра не найдена

---

#### POST /v1/games/{id}/blacklist

Добавление пользователя в черный список игры.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID игры

**Request Body:**

```json
{
  "id": "user-id",
  "login": "username_to_block"
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "user-id",
    "login": "username_to_block"
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Нет прав на управление черным списком
- `409` - Пользователь уже в черном списке
- `410` - Игра не найдена

---

#### DELETE /v1/games/{id}/blacklist/{login}

Удаление пользователя из черного списка.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID игры
- `login` (string) - логин пользователя

**Response 204:** No Content

**Errors:**
- `403` - Нет прав на управление черным списком
- `409` - Пользователя нет в черном списке
- `410` - Игра не найдена

---

#### DELETE /v1/games/{id}/comments/unread

Отметить все комментарии в игре как прочитанные.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID игры

**Response 204:** No Content

**Errors:**
- `410` - Игра не найдена

---

#### DELETE /v1/games/{id}/characters/unread

Отметить все персонажи в игре как прочитанные.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID игры

**Response 204:** No Content

**Errors:**
- `410` - Игра не найдена

---

### Персонажи

#### GET /v1/games/{id}/characters

Получение списка персонажей в игре.

**Path Parameters:**
- `id` (Guid) - ID игры

**Response 200:**

```json
{
  "resources": [
    {
      "id": "character-id",
      "name": "Имя персонажа",
      "race": "Эльф",
      "class": "Воин",
      "status": "Active",
      "author": {
        "id": "user-id",
        "login": "player_username"
      },
      "appearance": {
        "fullDescription": "Описание внешности"
      },
      "isNew": false
    }
  ]
}
```

**Errors:**
- `410` - Игра не найдена

---

#### POST /v1/games/{id}/characters

Создание нового персонажа в игре.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID игры

**Request Body:**

```json
{
  "name": "Имя персонажа",
  "race": "Эльф",
  "class": "Воин",
  "appearance": {
    "fullDescription": "Полное описание внешности персонажа"
  },
  "personality": {
    "alignment": "Lawful Good",
    "traits": "Черты характера"
  },
  "story": {
    "background": "Предыстория персонажа"
  },
  "attributes": [
    {
      "specificationId": "strength-id",
      "value": "18"
    },
    {
      "specificationId": "dexterity-id",
      "value": "14"
    }
  ]
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "character-id",
    "name": "Имя персонажа",
    "status": "Registration",
    "author": {
      "id": "user-id",
      "login": "username"
    }
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Нет прав на создание персонажа
- `410` - Игра не найдена

---

#### GET /v1/characters/{id}

Получение детальной информации о персонаже.

**Path Parameters:**
- `id` (Guid) - ID персонажа

**Response 200:**

```json
{
  "resource": {
    "id": "character-id",
    "name": "Имя персонажа",
    "race": "Эльф",
    "class": "Воин",
    "status": "Active",
    "author": {
      "id": "user-id",
      "login": "player_username"
    },
    "game": {
      "id": "game-id",
      "title": "Название игры"
    },
    "appearance": {
      "fullDescription": "Полное описание внешности",
      "portrait": "url-to-portrait"
    },
    "personality": {
      "alignment": "Lawful Good",
      "traits": "Черты характера"
    },
    "story": {
      "background": "Предыстория"
    },
    "attributes": [
      {
        "specification": {
          "id": "strength-id",
          "name": "Сила"
        },
        "value": "18"
      }
    ]
  }
}
```

**Errors:**
- `410` - Персонаж не найден

---

#### PATCH /v1/characters/{id}

Обновление информации о персонаже.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID персонажа

**Request Body:**

```json
{
  "name": "Новое имя",
  "appearance": {
    "fullDescription": "Обновленное описание"
  },
  "status": "Active",
  "attributes": [
    {
      "specificationId": "strength-id",
      "value": "20"
    }
  ]
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "character-id",
    "name": "Новое имя",
    "status": "Active"
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Нет прав на изменение
- `410` - Персонаж не найден

---

#### DELETE /v1/characters/{id}

Удаление персонажа.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID персонажа

**Response 204:** No Content

**Errors:**
- `403` - Нет прав на удаление
- `410` - Персонаж не найден

---

### Комнаты

#### GET /v1/games/{id}/rooms

Получение списка комнат в игре.

**Path Parameters:**
- `id` (Guid) - ID игры

**Response 200:**

```json
{
  "resources": [
    {
      "id": "room-id",
      "title": "Название комнаты",
      "accessType": "Public",
      "postsCount": 50,
      "unreadPostsCount": 5,
      "pendingPostsCount": 2
    }
  ]
}
```

**Errors:**
- `410` - Игра не найдена

---

#### POST /v1/games/{id}/rooms

Создание новой комнаты в игре.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID игры

**Request Body:**

```json
{
  "title": "Название новой комнаты",
  "accessType": "Public",
  "description": "Описание комнаты",
  "orderNumber": 1
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "room-id",
    "title": "Название новой комнаты",
    "accessType": "Public"
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Нет прав на создание комнат
- `410` - Игра не найдена

---

#### GET /v1/rooms/{id}

Получение информации о комнате.

**Path Parameters:**
- `id` (Guid) - ID комнаты

**Response 200:**

```json
{
  "resource": {
    "id": "room-id",
    "title": "Название комнаты",
    "accessType": "Public",
    "description": "Описание комнаты",
    "postsCount": 50,
    "game": {
      "id": "game-id",
      "title": "Название игры"
    }
  }
}
```

---

#### PATCH /v1/rooms/{id}

Обновление информации о комнате.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID комнаты

**Request Body:**

```json
{
  "title": "Обновленное название",
  "description": "Обновленное описание",
  "accessType": "Private"
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "room-id",
    "title": "Обновленное название",
    "accessType": "Private"
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Нет прав на изменение
- `410` - Комната не найдена

---

### Посты

#### GET /v1/rooms/{id}/posts

Получение списка постов в комнате.

**Path Parameters:**
- `id` (Guid) - ID комнаты

**Query Parameters:**
- `q.skip` (int) - количество пропускаемых записей
- `q.size` (int) - размер страницы

**Response 200:**

```json
{
  "resources": [
    {
      "id": "post-id",
      "text": "Текст поста с [b]BBCode[/b]",
      "author": {
        "id": "user-id",
        "login": "username"
      },
      "character": {
        "id": "character-id",
        "name": "Имя персонажа"
      },
      "createdAt": "2026-01-27T10:00:00Z",
      "commentary": "OOC комментарий",
      "isNew": false
    }
  ],
  "paging": {
    "number": 1,
    "size": 20,
    "totalPages": 3,
    "totalEntities": 50
  }
}
```

**Errors:**
- `410` - Комната не найдена

---

#### POST /v1/rooms/{id}/posts

Создание нового поста в комнате.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID комнаты

**Request Body:**

```json
{
  "characterId": "character-id",
  "text": "Текст поста с [b]BBCode[/b] разметкой",
  "commentary": "OOC комментарий (опционально)"
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "post-id",
    "text": "Текст поста",
    "character": {
      "id": "character-id",
      "name": "Имя персонажа"
    },
    "createdAt": "2026-01-27T10:00:00Z"
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Нет прав на создание постов
- `410` - Комната не найдена

---

#### GET /v1/posts/{id}

Получение поста по ID.

**Path Parameters:**
- `id` (Guid) - ID поста

**Response 200:**

```json
{
  "resource": {
    "id": "post-id",
    "text": "Текст поста",
    "author": {
      "id": "user-id",
      "login": "username"
    },
    "character": {
      "id": "character-id",
      "name": "Имя персонажа"
    },
    "createdAt": "2026-01-27T10:00:00Z",
    "updatedAt": null,
    "commentary": "OOC комментарий"
  }
}
```

---

#### PATCH /v1/posts/{id}

Обновление поста.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID поста

**Request Body:**

```json
{
  "text": "Обновленный текст поста",
  "commentary": "Обновленный комментарий"
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "post-id",
    "text": "Обновленный текст поста",
    "updatedAt": "2026-01-27T10:30:00Z"
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Нет прав на изменение
- `410` - Пост не найден

---

#### DELETE /v1/posts/{id}

Удаление поста.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID поста

**Response 204:** No Content

**Errors:**
- `403` - Нет прав на удаление
- `410` - Пост не найден

---

#### GET /v1/posts/{id}/votes

Получение списка голосований по посту (для механики голосований).

**Path Parameters:**
- `id` (Guid) - ID поста

**Response 200:**

```json
{
  "resources": [
    {
      "id": "vote-id",
      "voter": {
        "id": "user-id",
        "login": "username"
      },
      "character": {
        "id": "character-id",
        "name": "Имя персонажа"
      },
      "decision": "Approve"
    }
  ]
}
```

---

#### POST /v1/posts/{id}/votes

Голосование по посту.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID поста

**Request Body:**

```json
{
  "characterId": "character-id",
  "decision": "Approve"
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "vote-id",
    "decision": "Approve",
    "character": {
      "id": "character-id",
      "name": "Имя персонажа"
    }
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Нет прав на голосование
- `409` - Уже проголосовал
- `410` - Пост не найден

---

### Приглашения в игру

#### GET /v1/games/{gameId}/invitations

Получение всех активных приглашений для игры.

**Auth Required:** Yes (только мастер/ассистент)

**Path Parameters:**
- `gameId` (Guid) - ID игры

**Response 200:**

```json
{
  "resources": [
    {
      "id": "token-id",
      "user": {
        "id": "user-id",
        "login": "invited_username"
      },
      "invitationType": "Player",
      "expiresAt": "2026-02-01T12:00:00Z"
    }
  ]
}
```

---

#### POST /v1/games/{gameId}/invitations/players

Приглашение игрока в игру.

**Auth Required:** Yes

**Path Parameters:**
- `gameId` (Guid) - ID игры

**Request Body:**

```json
{
  "login": "username_to_invite"
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "token-id",
    "user": {
      "id": "user-id",
      "login": "username_to_invite"
    },
    "invitationType": "Player",
    "expiresAt": "2026-02-01T12:00:00Z"
  }
}
```

**Errors:**
- `403` - Нет прав на приглашение игроков
- `404` - Пользователь не найден

---

#### POST /v1/games/{gameId}/invitations/readers

Приглашение читателя в игру.

**Auth Required:** Yes

**Path Parameters:**
- `gameId` (Guid) - ID игры

**Request Body:**

```json
{
  "login": "username_to_invite"
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "token-id",
    "user": {
      "id": "user-id",
      "login": "username_to_invite"
    },
    "invitationType": "Reader",
    "expiresAt": "2026-02-01T12:00:00Z"
  }
}
```

**Errors:**
- `403` - Нет прав на приглашение читателей
- `404` - Пользователь не найден

---

#### DELETE /v1/games/{gameId}/invitations/{tokenId}

Отмена приглашения.

**Auth Required:** Yes

**Path Parameters:**
- `gameId` (Guid) - ID игры
- `tokenId` (Guid) - ID токена приглашения

**Response 204:** No Content

**Errors:**
- `403` - Нет прав на отмену приглашений
- `404` - Приглашение не найдено

---

## Messaging (Сообщения)

### Глобальный чат

#### GET /v1/globalchat/messages

Получение сообщений глобального чата.

**Query Parameters:**
- `q.skip` (int) - количество пропускаемых записей
- `q.size` (int) - размер страницы (default: 50)

**Response 200:**

```json
{
  "resources": [
    {
      "id": "message-id",
      "text": "Текст сообщения",
      "author": {
        "id": "user-id",
        "login": "username",
        "role": "Player"
      },
      "createdAt": "2026-01-27T10:00:00Z",
      "likesCount": 3
    }
  ],
  "paging": {
    "number": 1,
    "size": 50,
    "totalPages": 10,
    "totalEntities": 500
  }
}
```

---

#### POST /v1/globalchat/messages

Отправка сообщения в глобальный чат.

**Auth Required:** Yes

**Request Body:**

```json
{
  "text": "Текст сообщения"
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "message-id",
    "text": "Текст сообщения",
    "author": {
      "id": "user-id",
      "login": "username"
    },
    "createdAt": "2026-01-27T10:00:00Z"
  }
}
```

---

#### GET /v1/globalchat/messages/{id}

Получение сообщения чата по ID.

**Path Parameters:**
- `id` (Guid) - ID сообщения

**Response 200:**

```json
{
  "resource": {
    "id": "message-id",
    "text": "Текст сообщения",
    "author": {
      "id": "user-id",
      "login": "username"
    },
    "createdAt": "2026-01-27T10:00:00Z"
  }
}
```

**Errors:**
- `410` - Сообщение не найдено

---

#### PATCH /v1/globalchat/messages/{id}

Редактирование сообщения чата.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID сообщения

**Request Body:**

```json
{
  "text": "Обновленный текст сообщения"
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "message-id",
    "text": "Обновленный текст сообщения",
    "updatedAt": "2026-01-27T10:30:00Z"
  }
}
```

**Errors:**
- `403` - Нет прав на редактирование
- `410` - Сообщение не найдено

---

#### DELETE /v1/globalchat/messages/{id}

Удаление сообщения чата.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID сообщения

**Response 204:** No Content

**Errors:**
- `403` - Нет прав на удаление
- `410` - Сообщение не найдено

---

#### POST /v1/globalchat/messages/{id}/likes

Лайк сообщения.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID сообщения

**Response 201:**

```json
{
  "resource": {
    "id": "message-id",
    "likesCount": 4
  }
}
```

**Errors:**
- `409` - Уже лайкнули это сообщение
- `410` - Сообщение не найдено

---

#### DELETE /v1/globalchat/messages/{id}/likes

Удаление лайка с сообщения.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID сообщения

**Response 200:**

```json
{
  "resource": {
    "id": "message-id",
    "likesCount": 3
  }
}
```

**Errors:**
- `409` - Не лайкали это сообщение
- `410` - Сообщение не найдено

---

#### GET /v1/globalchat/logs/{date}

Получение истории чата за определенную дату.

**Path Parameters:**
- `date` (DateOnly) - дата в формате YYYY-MM-DD

**Response 200:**

```json
{
  "resources": [
    {
      "id": "message-id",
      "text": "Сообщение",
      "author": {
        "id": "user-id",
        "login": "username"
      },
      "createdAt": "2026-01-27T10:00:00Z"
    }
  ]
}
```

---

#### GET /v1/globalchat/logs/{date}/first

Получение первого сообщения на дату или после нее.

**Path Parameters:**
- `date` (DateOnly) - дата в формате YYYY-MM-DD

**Response 200:**

```json
{
  "resource": {
    "id": "message-id",
    "text": "Первое сообщение",
    "createdAt": "2026-01-27T00:00:01Z"
  }
}
```

**Errors:**
- `404` - Нет сообщений на эту дату или после

---

#### GET /v1/globalchat/logs/{date}/last

Получение последнего сообщения на дату или до нее.

**Path Parameters:**
- `date` (DateOnly) - дата в формате YYYY-MM-DD

**Response 200:**

```json
{
  "resource": {
    "id": "message-id",
    "text": "Последнее сообщение",
    "createdAt": "2026-01-27T23:59:59Z"
  }
}
```

**Errors:**
- `404` - Нет сообщений на эту дату или до

---

#### GET /v1/globalchat/messages/{id}/before

Получение сообщений до указанного (старше).

**Path Parameters:**
- `id` (Guid) - ID опорного сообщения

**Query Parameters:**
- `count` (int) - количество сообщений (default: 50)

**Response 200:**

```json
{
  "resources": [
    {
      "id": "message-id",
      "text": "Старое сообщение",
      "createdAt": "2026-01-27T09:00:00Z"
    }
  ]
}
```

---

#### GET /v1/globalchat/messages/{id}/after

Получение сообщений после указанного (новее).

**Path Parameters:**
- `id` (Guid) - ID опорного сообщения

**Query Parameters:**
- `count` (int) - количество сообщений (default: 50)

**Response 200:**

```json
{
  "resources": [
    {
      "id": "message-id",
      "text": "Новое сообщение",
      "createdAt": "2026-01-27T11:00:00Z"
    }
  ]
}
```

---

#### GET /v1/globalchat/messages/{id}/around

Получение сообщений вокруг указанного.

**Path Parameters:**
- `id` (Guid) - ID опорного сообщения

**Query Parameters:**
- `count` (int) - общее количество сообщений (default: 50)

**Response 200:**

```json
{
  "resources": [
    {
      "id": "message-id",
      "text": "Сообщение",
      "createdAt": "2026-01-27T10:00:00Z"
    }
  ]
}
```

---

### Диалоги

#### GET /v1/conversations

Получение списка диалогов текущего пользователя.

**Auth Required:** Yes

**Query Parameters:**
- `q.skip` (int) - количество пропускаемых записей
- `q.size` (int) - размер страницы

**Response 200:**

```json
{
  "resources": [
    {
      "id": "conversation-id",
      "title": "Название диалога (для групповых)",
      "visavis": {
        "id": "user-id",
        "login": "username"
      },
      "participants": [
        {
          "id": "user-id-1",
          "login": "user1"
        },
        {
          "id": "user-id-2",
          "login": "user2"
        }
      ],
      "lastMessage": {
        "text": "Последнее сообщение",
        "createdAt": "2026-01-27T10:00:00Z"
      },
      "unreadMessagesCount": 3,
      "isGroup": false
    }
  ],
  "paging": {
    "number": 1,
    "size": 20,
    "totalPages": 3,
    "totalEntities": 50
  }
}
```

---

#### GET /v1/conversations/{id}

Получение диалога по ID.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID диалога

**Response 200:**

```json
{
  "resource": {
    "id": "conversation-id",
    "title": "Название диалога",
    "participants": [
      {
        "id": "user-id",
        "login": "username"
      }
    ],
    "isGroup": false,
    "unreadMessagesCount": 3
  }
}
```

**Errors:**
- `410` - Диалог не найден

---

#### GET /v1/conversations/direct/{userId}

Получение приватного диалога с пользователем по ID.

**Auth Required:** Yes

**Path Parameters:**
- `userId` (Guid) - ID пользователя

**Response 200:**

```json
{
  "resource": {
    "id": "conversation-id",
    "visavis": {
      "id": "user-id",
      "login": "username"
    },
    "isGroup": false
  }
}
```

**Errors:**
- `410` - Пользователь не найден

---

#### GET /v1/conversations/direct/{login}

Получение приватного диалога с пользователем по логину.

**Auth Required:** Yes

**Path Parameters:**
- `login` (string) - логин пользователя

**Response 200:**

```json
{
  "resource": {
    "id": "conversation-id",
    "visavis": {
      "id": "user-id",
      "login": "username"
    },
    "isGroup": false
  }
}
```

**Errors:**
- `410` - Пользователь не найден

---

#### POST /v1/conversations

Создание нового группового диалога.

**Auth Required:** Yes

**Request Body:**

```json
{
  "title": "Название группы",
  "participantLogins": ["user1", "user2", "user3"]
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "conversation-id",
    "title": "Название группы",
    "isGroup": true,
    "participants": [
      {
        "id": "user-id-1",
        "login": "user1"
      },
      {
        "id": "user-id-2",
        "login": "user2"
      }
    ]
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `401` - Требуется аутентификация

---

#### PATCH /v1/conversations/{id}

Обновление диалога (название и/или участники для групповых).

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID диалога

**Request Body:**

```json
{
  "title": "Новое название",
  "participantLogins": ["user1", "user2", "user4"]
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "conversation-id",
    "title": "Новое название",
    "participants": [
      {
        "id": "user-id",
        "login": "user4"
      }
    ]
  }
}
```

**Errors:**
- `400` - Невалидные параметры
- `403` - Не участник диалога
- `410` - Диалог не найден

---

#### DELETE /v1/conversations/{id}/messages/unread

Отметить все сообщения в диалоге как прочитанные.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID диалога

**Response 204:** No Content

**Errors:**
- `410` - Диалог не найден

---

### Сообщения

#### GET /v1/conversations/{id}/messages

Получение сообщений в диалоге.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID диалога

**Query Parameters:**
- `q.skip` (int) - количество пропускаемых записей
- `q.size` (int) - размер страницы

**Response 200:**

```json
{
  "resources": [
    {
      "id": "message-id",
      "text": "Текст сообщения",
      "author": {
        "id": "user-id",
        "login": "username"
      },
      "createdAt": "2026-01-27T10:00:00Z",
      "isNew": false,
      "likesCount": 2
    }
  ],
  "paging": {
    "number": 1,
    "size": 50,
    "totalPages": 3,
    "totalEntities": 150
  }
}
```

**Errors:**
- `410` - Диалог не найден

---

#### POST /v1/conversations/{id}/messages

Отправка сообщения в диалог.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID диалога

**Request Body:**

```json
{
  "text": "Текст нового сообщения"
}
```

**Response 201:**

```json
{
  "resource": {
    "id": "message-id",
    "text": "Текст нового сообщения",
    "author": {
      "id": "user-id",
      "login": "username"
    },
    "createdAt": "2026-01-27T10:00:00Z"
  }
}
```

**Errors:**
- `400` - Невалидный текст
- `403` - Нет прав на отправку в этот диалог
- `410` - Диалог не найден

---

#### GET /v1/messages/{id}

Получение сообщения по ID.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID сообщения

**Response 200:**

```json
{
  "resource": {
    "id": "message-id",
    "text": "Текст сообщения",
    "author": {
      "id": "user-id",
      "login": "username"
    },
    "createdAt": "2026-01-27T10:00:00Z",
    "likesCount": 2
  }
}
```

**Errors:**
- `410` - Сообщение не найдено

---

#### PATCH /v1/messages/{id}

Редактирование сообщения.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID сообщения

**Request Body:**

```json
{
  "text": "Обновленный текст сообщения"
}
```

**Response 200:**

```json
{
  "resource": {
    "id": "message-id",
    "text": "Обновленный текст сообщения",
    "updatedAt": "2026-01-27T10:30:00Z"
  }
}
```

**Errors:**
- `400` - Невалидный текст
- `403` - Нет прав на редактирование
- `410` - Сообщение не найдено

---

#### DELETE /v1/messages/{id}

Удаление сообщения.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID сообщения

**Response 204:** No Content

**Errors:**
- `410` - Сообщение не найдено

---

#### POST /v1/messages/{id}/likes

Лайк сообщения.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID сообщения

**Response 200:**

```json
{
  "resource": {
    "id": "message-id",
    "likesCount": 3
  }
}
```

**Errors:**
- `409` - Уже лайкнули это сообщение
- `410` - Сообщение не найдено

---

#### DELETE /v1/messages/{id}/likes

Удаление лайка с сообщения.

**Auth Required:** Yes

**Path Parameters:**
- `id` (Guid) - ID сообщения

**Response 204:** No Content

**Errors:**
- `409` - Не лайкали это сообщение
- `410` - Сообщение не найдено

---

## Common (Общее)

### Поиск

#### GET /v1/search

Глобальный поиск по сайту (через gRPC Search Service).

**Query Parameters:**
- `query` (string) - поисковый запрос
- `q.skip` (int) - количество пропускаемых записей
- `q.size` (int) - размер страницы (default: 10)

**Response 200:**

```json
{
  "resources": [
    {
      "type": "Game",
      "id": "game-id",
      "title": "Название игры",
      "snippet": "Фрагмент с найденным текстом..."
    },
    {
      "type": "Topic",
      "id": "topic-id",
      "title": "Название темы",
      "snippet": "Фрагмент текста..."
    }
  ],
  "paging": {
    "number": 1,
    "size": 10,
    "totalPages": 5,
    "totalEntities": 50
  }
}
```

---

## WebSocket / SignalR

### Realtime Notifications Hub

**Endpoint:** `wss://api.example.com/whatsup`

**Аутентификация:** Bearer Token в query string или headers

**События:**
- `NewPost` - новый пост в игре
- `NewComment` - новый комментарий
- `NewMessage` - новое личное сообщение
- `NewChatMessage` - новое сообщение в глобальном чате
- `CharacterStatusChanged` - изменение статуса персонажа
- `GameStatusChanged` - изменение статуса игры

**Пример подключения (JavaScript):**

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://api.example.com/whatsup", {
        accessTokenFactory: () => yourAccessToken
    })
    .build();

connection.on("NewMessage", (message) => {
    console.log("New message:", message);
});

await connection.start();
```

---

## Дополнительные endpoints

### Health Check

#### GET /_health

Проверка состояния API.

**Response 200:** `Healthy`

---

### Metrics

#### GET /metrics

Метрики Prometheus (требует соответствующих прав доступа).

**Response 200:** Метрики в формате Prometheus

---

## Заключение

Это полная справка по API DM3. Для получения актуальной документации в формате OpenAPI/Swagger посетите:

```
https://api.example.com/swagger
```

Для вопросов и поддержки обращайтесь к документации проекта или в раздел Issues на GitHub.
