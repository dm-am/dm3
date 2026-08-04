# Загрузка изображений

Правила работы upload-пайплайна для аватаров. Контракты и инварианты.

## Архитектура

**Один source-файл per upload + on-the-fly transforms через [imgproxy](https://imgproxy.net/).**

- Бэк хранит ровно один файл per upload (≤1024 px, EXIF-stripped, в формате uploader'а).
- Thumbnail-варианты генерируются по требованию.
- Format negotiation (AVIF / WebP / JPEG) — через `Accept` header.

## Поддерживаемые форматы

Только JPEG, PNG, WebP. Остальные отклоняются.

Content-type определяется по magic-byte'ам файла, **не** по клиентскому заголовку
и **не** по расширению.

## API-контракт

Аватар возвращается в DTO как вложенный объект:

```jsonc
{
  "picture": {
    "smallUrl":    "...",  // 100×100 square (через transform layer)
    "mediumUrl":   "...",  // 400×400 square (через transform layer)
    "originalUrl": "..."   // ≤1024 px aspect-preserving (прямой CDN link)
  }
}
```

Плоские `*PictureUrl` поля на сущностях запрещены.

UI pick rule:
- Inline avatars (списки, chat) — `smallUrl`
- Cards, post sidebars — `mediumUrl`
- Profile page (большой aspect-preserving) — `originalUrl`

FE собирает `srcset` из small+medium → браузер выбирает по DPR.

## HTTP-кеширование

```
Cache-Control: public, max-age=31536000
Vary: Accept
```

Ключ объекта случайный, не хеш содержимого, и не переписывается: замена аватара = новый ключ.

## Валидация на upload (обязательные шаги)

1. Pre-check размера (≤10 МБ) — до буферизации в память.
2. Magic-byte detection формата.
3. Format whitelist.
4. Identify-only пас (размеры без полного декода).
5. Min/max dimension checks.
6. Decompression-bomb check (pixel area до декода).
7. Decode + resize если больше 1024 px по любой стороне (aspect-preserving).
8. EXIF/IPTC/XMP strip.
9. Re-encode в исходном формате.

Любой fail → HTTP 400 с RU-сообщением для UI.

## Атомарность

Один S3 PUT + один DB INSERT. Failure любого шага → rollback ранее залитого
объекта. Никаких partial-states.

## URL signing

Transform-URL'ы подписываются HMAC-SHA256: иначе anyone мог бы попросить
произвольный transform и сжечь CPU.

## Lifecycle

- Аватар указывается на сущности через FK на Upload.
- При замене старый Upload soft-deleted.
- Source-объект удаляется фоновым worker'ом через grace-period 24h.

## Запрещенные паттерны

- ❌ Доверять `Content-Type` от клиента.
- ❌ Брать расширение из user-filename — только из validated content-type.
- ❌ Сохранять EXIF (риск утечки GPS).
- ❌ Декодировать изображение до проверки размеров.
- ❌ Partial S3 state при failure.
- ❌ Mutable object keys.
- ❌ Пре-генерировать thumbnails в storage (только on-the-fly).
- ❌ Polymorphic FK для target'а upload'а — отдельная типизированная колонка
  per upload type с CHECK constraint, гарантирующим консистентность.
- ❌ Второй путь загрузки в обход валидации. Тип загрузки, для которого сделано
  исключение, обходит и magic-byte detection, и whitelist — а объект попадает в
  публично читаемый бакет, который прокси отдает с origin приложения. Новый тип
  либо проходит этот же конвейер, либо приносит собственную валидацию; тест
  требует, чтобы неохваченных типов не осталось.

## Rate limit

`POST /v1/uploads` — 10 запросов в минуту per user (fallback на IP для guests).
Превышение → HTTP 429 с `Retry-After`.

## Idempotency

Клиент шлет header `Idempotency-Key: <uuid>`. Server кеширует response per
`(userId, key)` на час. Дублирующие retry возвращают тот же ответ.

## Live update

После замены/сброса аватара backend broadcast'ит `UserAvatarChanged` через
SignalR. Открытые вкладки делают refetch — аватары перерендериваются с
новыми immutable URL'ами.

## UX на FE

Drag-drop, clipboard paste, прогресс-бар, client-side compression до 1024 px
перед upload, сброс через `DELETE /v1/users/me/profile/avatar` (идемпотент).

## Observability

OTel spans на upload-пайплайн + Prometheus метрики (success/failure counter,
duration histogram, size histograms). Атрибуты span: upload type, sizes,
idempotency key.

## Seed-данные

Seed-аватары проходят тот же pipeline, что и пользовательские — один
source-файл, никаких прибитых thumbnails.
