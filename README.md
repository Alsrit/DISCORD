# Secure License Platform

Безопасная платформа лицензирования и обновления Windows-приложений на базе `.NET 8`, `ASP.NET Core`, `PostgreSQL`, `Redis` и `WPF`.

Текущая реализация даёт рабочий фундамент для реального продукта:
- онлайн-активация лицензий;
- привязка лицензии к устройству;
- отзыв лицензии и устройства;
- серверная проверка состояния лицензии;
- сессии клиента с access/refresh токенами;
- телеметрия, аудит и журнал безопасности;
- публикация подписанных обновлений;
- русскоязычная админ-панель;
- русскоязычный WPF-клиент с тёмной темой;
- миграции EF Core и seed-данные.

Старые Python-файлы в `app/`, `clients/`, `deploy/docker-compose/` сохранены как legacy-артефакты и не используются новой .NET-платформой.

## Архитектурная схема

```text
WPF Client (.NET 8, DPAPI, pinning, проверка подписи обновлений)
    |
    | HTTPS + certificate pinning + opaque bearer token
    v
ASP.NET Core API
    |-- сервис лицензирования
    |-- сервис обновлений
    |-- сервис телеметрии
    |-- аудит и security incidents
    |
    +--> PostgreSQL
    +--> Redis
    +--> release storage

ASP.NET Core Admin
    |-- cookie auth
    |-- роли: Administrator / Operator / Auditor
    |-- лицензии / устройства / релизы / телеметрия / безопасность

Worker Service
    |-- истечение refresh-сессий
    |-- очистка старой телеметрии
    |-- периодический seed / service maintenance
```

## Список модулей

```text
src/
  Platform.Domain/          доменные сущности и enum
  Platform.Application/     DTO, интерфейсы сервисов, контракты
  Platform.Infrastructure/  EF Core, PostgreSQL, Redis, криптография, сервисы
  Platform.Api/             клиентский API
  Platform.Admin/           веб-админка на Razor Pages
  Platform.Worker/          фоновые задачи
  Platform.Client.Core/     безопасное хранилище, API-клиент, обновления
  Platform.Client.Wpf/      русский WPF-клиент
deploy/
  docker-compose.platform.yml
  examples/clientsettings.sample.json
  keys/.gitkeep
scripts/
  generate-update-signing-key.ps1
```

## Основные сущности БД

- `License`
- `Device`
- `ClientSession`
- `LicenseActivation`
- `LicenseAuditEvent`
- `TelemetryEvent`
- `SecurityIncident`
- `UpdateChannelDefinition`
- `ApplicationRelease`
- `ReleaseArtifact`
- `AdminUser`

Миграции лежат в [`src/Platform.Infrastructure/Persistence/Migrations`](C:/DISCORD/src/Platform.Infrastructure/Persistence/Migrations).

## API endpoints

### Клиентские

- `POST /api/client/v1/activate`
- `POST /api/client/v1/refresh`
- `GET /api/client/v1/license/status`
- `POST /api/client/v1/heartbeat`
- `POST /api/client/v1/telemetry`
- `GET /api/client/v1/updates/check`
- `GET /api/client/v1/updates/download/{releaseId}`
- `GET /api/client/v1/system/info`

### Системные

- `GET /health/live`
- `GET /health/ready`

## Сценарий активации

1. Пользователь устанавливает клиент.
2. Клиент генерирует `installation id`, собирает отпечаток устройства и шифрует локальные токены через `DPAPI`.
3. Пользователь вводит лицензионный ключ.
4. Клиент отправляет запрос активации на сервер по `HTTPS`.
5. Сервер ищет лицензию по хешу ключа, проверяет срок действия, отзыв и лимит устройств.
6. Сервер создаёт или обновляет `Device`.
7. Сервер выдаёт `access token` и `refresh token`.
8. Сервер пишет `LicenseActivation`, аудит и security events.
9. Клиент хранит токены локально в защищённом виде.

## Сценарий обновления

1. Администратор публикует новый релиз через админ-панель.
2. Сервер сохраняет пакет, вычисляет `SHA-256` и подписывает манифест релиза приватным ключом.
3. Клиент запрашивает `updates/check`.
4. Клиент скачивает пакет только с доверенного API.
5. Клиент проверяет:
   - hash файла;
   - подпись манифеста;
   - соответствие канала обновлений.
6. Клиент устанавливает только `MSIX/MSIXBundle`.
7. При ошибке проверки обновление не ставится.

## Что уже реализовано по безопасности

- лицензионные ключи на сервере хранятся только в виде хеша;
- access/refresh токены не хранятся в открытом виде на сервере;
- локально токены шифруются через `DPAPI`;
- есть `certificate pinning` на клиенте;
- есть rate limit через Redis с fallback на память;
- отзыв лицензии отзывает и связанные устройства/сессии;
- refresh token ротационный;
- журнал безопасности вынесен в отдельную сущность `SecurityIncident`;
- обновления подписываются отдельно от TLS;
- UI админки полностью на русском языке;
- продакшн-конфиги вынесены в `appsettings.Production.json`.

## Быстрый старт локально

### 1. Поднять инфраструктуру

```powershell
docker compose -f deploy/docker-compose.platform.yml up -d
```

Это поднимет:
- PostgreSQL на `localhost:5432`
- Redis на `localhost:6379`

### 2. Сгенерировать ключи подписи обновлений

```powershell
pwsh ./scripts/generate-update-signing-key.ps1
```

Ключи будут созданы в `deploy/keys/`.

### 3. Применить миграции

```powershell
dotnet ef database update `
  --project src/Platform.Infrastructure/Platform.Infrastructure.csproj `
  --startup-project src/Platform.Api/Platform.Api.csproj
```

### 4. Запустить API

```powershell
dotnet run --project src/Platform.Api/Platform.Api.csproj
```

### 5. Запустить админку

```powershell
dotnet run --project src/Platform.Admin/Platform.Admin.csproj
```

### 6. Запустить worker

```powershell
dotnet run --project src/Platform.Worker/Platform.Worker.csproj
```

### 7. Запустить клиент

```powershell
dotnet run --project src/Platform.Client.Wpf/Platform.Client.Wpf.csproj
```

## Seed-данные

По умолчанию сидируются:

### Администратор

- логин: `admin`
- пароль: `ChangeThisPassword!`

### Демонстрационные лицензии

- `SLP-DEMO-0001-0001-0001`
- `SLP-INTERNAL-0001-0001`

Настройки seed лежат в:
- [`src/Platform.Api/appsettings.json`](C:/DISCORD/src/Platform.Api/appsettings.json)
- [`src/Platform.Admin/appsettings.json`](C:/DISCORD/src/Platform.Admin/appsettings.json)
- [`src/Platform.Worker/appsettings.json`](C:/DISCORD/src/Platform.Worker/appsettings.json)

## Сборка решения

```powershell
dotnet build SecureLicensePlatform.sln
```

## Сборка Windows-клиента

### Debug

```powershell
dotnet build src/Platform.Client.Wpf/Platform.Client.Wpf.csproj
```

### Release publish

```powershell
dotnet publish src/Platform.Client.Wpf/Platform.Client.Wpf.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false
```

Результат будет в:
- `src/Platform.Client.Wpf/bin/Release/net8.0-windows/win-x64/publish`

## Подготовка MSIX

Текущий код клиента уже умеет:
- проверять подпись релиза;
- скачивать только доверенный пакет;
- устанавливать `MSIX/MSIXBundle`.

Для полноценного distributable-пакета нужен один из двух путей:

1. `Windows Application Packaging Project (.wapproj)` в Visual Studio.
2. `MSIX Packaging Tool` поверх publish-папки.

Рекомендуемый production-процесс:

1. Собрать `Release` publish.
2. Упаковать в `MSIX`.
3. Подписать корпоративным сертификатом.
4. Передать пакет в админ-панель через страницу публикации релиза.
5. Раздать публичный ключ проверки обновлений клиентам.

## Публикация обновления

1. Собери новый `MSIX/MSIXBundle`.
2. Подпиши пакет стандартной Windows-подписью.
3. Открой админ-панель.
4. Перейди на страницу `Обновления`.
5. Укажи:
   - версию;
   - канал (`stable` / `beta` / `internal`);
   - минимальную поддерживаемую версию;
   - обязательность;
   - описание;
   - файл пакета.
6. Сохрани публикацию.

Сервер:
- вычислит `SHA-256`;
- подпишет манифест релиза;
- запишет релиз и артефакт в БД;
- начнёт отдавать обновление клиентам по каналу.

## Пример клиентских настроек

См. файл:
- [`deploy/examples/clientsettings.sample.json`](C:/DISCORD/deploy/examples/clientsettings.sample.json)

Реальный файл клиента создаётся автоматически в:

`%LocalAppData%\SecureLicensePlatform\clientsettings.json`

## Проверка сборки

На текущий момент проверено:

```powershell
dotnet build SecureLicensePlatform.sln
```

Сборка проходит успешно.

## Что стоит сделать следующим шагом

- вынести настройки в переменные окружения и Secret Manager;
- добавить реальную метрику/трейсинг через OpenTelemetry exporter;
- добавить полноценный `wapproj` для автоматической MSIX-сборки;
- вынести release storage в S3/MinIO/NAS;
- добавить фоновые push-уведомления клиентам;
- добавить разграничение по tenant/партнёрам;
- добавить автоматические тесты API и UI.
