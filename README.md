# Secure License Platform

Серверная платформа лицензирования и перевода модов Stellaris на `.NET 8`.

Текущее состояние репозитория после этапов 1 и 2:
- существующая лицензионная платформа сохранена и расширена, а не переписана с нуля;
- сервер умеет принимать job-based задания на перевод и возвращать результат архивом;
- перевод выполняется только на сервере через provider abstraction;
- лицензии, квоты, glossary, очередь, аудит и результаты контролируются сервером;
- WPF-клиент получил мастер перевода модов, загрузку результата и безопасную сборку отдельного сабмода.

## Архитектура

```text
WPF Client
    |
    | HTTPS + opaque session tokens
    v
ASP.NET Core API
    |-- licensing/session endpoints
    |-- translation analyze/jobs endpoints
    |-- quotas / glossary / languages
    |
    +--> PostgreSQL
    +--> Redis (rate limit + queue storage)
    +--> translation storage

ASP.NET Core Admin
    |-- лицензии / устройства / релизы
    |-- переводы, queue status, quotas, provider state, glossary

Worker Service
    |-- session maintenance
    |-- telemetry cleanup
    |-- translation job processing
    |-- stale jobs / snapshot / temp cleanup
```

## Изменённые проекты

- `src/Platform.Domain`
- `src/Platform.Application`
- `src/Platform.Infrastructure`
- `src/Platform.Api`
- `src/Platform.Admin`
- `src/Platform.Worker`
- `src/Platform.Client.Core`
- `src/Platform.Client.Wpf`
- `tests/Platform.Server.Tests`
- `tests/Platform.Client.Tests`

## Основные сущности БД

Старые сущности сохранены:
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

Новые translation-сущности:
- `TranslationJob`
- `TranslationJobItem`
- `TranslationSegment`
- `TranslationFile`
- `TranslationProviderSettings`
- `TranslationGlossary`
- `TranslationQuota`
- `TranslationUsage`
- `TranslationAuditEvent`
- `SubmodBuildArtifact`
- `ModAnalysisSnapshot`

Миграции:
- [`20260313084306_AddTranslationPipeline.cs`](/C:/DISCORD/src/Platform.Infrastructure/Persistence/Migrations/20260313084306_AddTranslationPipeline.cs)
- snapshot: [`PlatformDbContextModelSnapshot.cs`](/C:/DISCORD/src/Platform.Infrastructure/Persistence/Migrations/PlatformDbContextModelSnapshot.cs)

## Серверный pipeline перевода

1. Клиент вызывает `POST /api/client/v1/mods/analyze`.
2. Сервер валидирует payload, размеры, sha256 и безопасные пути.
3. Сервер создаёт `ModAnalysisSnapshot`.
4. Клиент вызывает `POST /api/client/v1/translations/jobs`.
5. Сервер проверяет сессию, лицензию, device binding и quota.
6. Сервер создаёт `TranslationJob`, `TranslationFile`, `TranslationSegment`.
7. Job кладётся в очередь Redis с fallback на DB scan.
8. Worker забирает job, защищает placeholders и service tokens.
9. Worker отправляет батчи в `ITranslationProvider` (`YandexTranslateProvider`).
10. Worker восстанавливает токены, валидирует output и собирает translated files.
11. Worker пакует результат в zip и пишет `SubmodBuildArtifact`.
12. Клиент получает статус и скачивает архив результата.

## API endpoints

Существующие licensing/update endpoints:
- `POST /api/client/v1/activate`
- `POST /api/client/v1/refresh`
- `GET /api/client/v1/license/status`
- `POST /api/client/v1/heartbeat`
- `POST /api/client/v1/telemetry`
- `GET /api/client/v1/updates/check`
- `GET /api/client/v1/updates/download/{releaseId}`
- `GET /api/client/v1/system/info`

Новые translation endpoints:
- `POST /api/client/v1/mods/analyze`
- `POST /api/client/v1/translations/jobs`
- `GET /api/client/v1/translations/jobs/{jobId}`
- `GET /api/client/v1/translations/jobs/{jobId}/files`
- `GET /api/client/v1/translations/jobs/{jobId}/download`
- `POST /api/client/v1/translations/jobs/{jobId}/cancel`
- `GET /api/client/v1/languages`
- `GET /api/client/v1/glossaries/active`
- `GET /api/client/v1/quotas/current`

Системные endpoints:
- `GET /health/live`
- `GET /health/ready`

Dev-only:
- Swagger UI при `ASPNETCORE_ENVIRONMENT=Development`

## DTO примеры

Анализ мода:

```json
{
  "modName": "Amazing Space Mod",
  "modVersion": "1.2.0",
  "originalModReference": "workshop/123456789",
  "sourceLanguage": "en",
  "files": [
    {
      "relativePath": "localisation/english/amazing_l_english.yml",
      "content": "l_english:\ngreeting:0 \"Hello $NAME$\"",
      "sourceLanguage": "en",
      "sha256": "F0A1...",
      "sizeBytes": 42
    }
  ]
}
```

Создание job:

```json
{
  "analysisSnapshotId": null,
  "modName": "Amazing Space Mod",
  "originalModReference": "workshop/123456789",
  "sourceLanguage": "en",
  "targetLanguage": "ru",
  "requestedSubmodName": "[RU] Amazing Space Mod (Auto Translation)",
  "providerCode": "yandex",
  "files": [
    {
      "relativePath": "localisation/english/amazing_l_english.yml",
      "content": "l_english:\ngreeting:0 \"Hello $NAME$\"",
      "sourceLanguage": "en",
      "sha256": "F0A1...",
      "sizeBytes": 42
    }
  ]
}
```

Заголовок идемпотентности:

```http
Idempotency-Key: mod-job-0001
```

Ответ на создание job:

```json
{
  "jobId": "0baf6a12-5d12-45e1-9b8c-4f2d8c2a7715",
  "status": "Queued",
  "requestedUtc": "2026-03-13T09:45:12.1200000+00:00",
  "quota": {
    "maxFilesPerJob": 64,
    "maxSegmentsPerJob": 4000,
    "maxCharactersPerJob": 120000,
    "maxCharactersPerDay": 480000,
    "remainingCharactersToday": 479958,
    "maxConcurrentJobs": 2,
    "activeJobs": 1,
    "maxJobsPerHour": 10,
    "remainingJobsThisHour": 9,
    "maxAnalysisPerHour": 20,
    "remainingAnalysisThisHour": 19
  },
  "message": "Задание поставлено в очередь."
}
```

Статус job:

```json
{
  "jobId": "0baf6a12-5d12-45e1-9b8c-4f2d8c2a7715",
  "status": "Completed",
  "providerCode": "yandex",
  "modName": "Amazing Space Mod",
  "sourceLanguage": "en",
  "targetLanguage": "ru",
  "requestedSubmodName": "[RU] Amazing Space Mod (Auto Translation)",
  "totalFiles": 1,
  "totalSegments": 1,
  "totalCharacters": 12,
  "processedSegments": 1,
  "processedCharacters": 13,
  "retryCount": 0,
  "downloadAvailable": true,
  "failureCode": "",
  "failureReason": "",
  "requestedUtc": "2026-03-13T09:45:12.1200000+00:00",
  "startedUtc": "2026-03-13T09:45:15.0000000+00:00",
  "completedUtc": "2026-03-13T09:45:17.0000000+00:00",
  "cancelRequestedUtc": null,
  "manifestPreview": {
    "submodName": "[RU] Amazing Space Mod (Auto Translation)",
    "descriptorName": "descriptor.mod",
    "targetLanguage": "ru",
    "outputFiles": [
      "localisation/english/amazing_l_english.yml"
    ],
    "notes": [
      "Оригинальный мод не изменяется.",
      "В архив включаются только переведённые localisation-файлы.",
      "Клиент может использовать этот manifest для безопасной сборки отдельного submod."
    ]
  }
}
```

## Качество API и безопасность

Уже реализовано:
- `ProblemDetails` для ошибок API
- `X-Correlation-ID`
- `FluentValidation` для analyze/create job DTO
- route versioning через `/api/client/v1`
- `Idempotency-Key` на создание job
- rate limit для analyze/create
- session-based authorization через opaque bearer tokens
- ограничение по payload size / file count / segment count / character count
- path traversal protection для `localisation/*`
- server-only хранение Yandex credentials
- protected token pipeline для `$NAME$`, `[Root.GetName]`, `§R`, `£energy£`, `%s`, `\n`
- quota enforcement на лицензию и устройство
- structured audit и security incidents
- retry / throttle / circuit breaker для provider layer

Что важно:
- клиент никогда не обращается к Yandex Translate напрямую;
- секрет Yandex берётся только из server config/env var;
- сервер не пишет поверх оригинального мода;
- результат отдаётся отдельным архивом, пригодным для следующего клиентского этапа.

## Очередь и worker

Очередь:
- Redis list: `queue:translations`
- fallback: worker умеет сканировать DB jobs со статусом `Queued`

Статусы jobs:
- `Pending`
- `Queued`
- `Processing`
- `Completed`
- `Failed`
- `CancelRequested`
- `Cancelled`
- `Expired`

Worker задачи:
- обработка translation jobs
- refresh/session cleanup
- telemetry cleanup
- stale snapshot cleanup
- timeout stale processing jobs
- cleanup temp/result storage по retention policy

## Конфигурация

Основные dev-конфиги:
- [`src/Platform.Api/appsettings.json`](/C:/DISCORD/src/Platform.Api/appsettings.json)
- [`src/Platform.Admin/appsettings.json`](/C:/DISCORD/src/Platform.Admin/appsettings.json)
- [`src/Platform.Worker/appsettings.json`](/C:/DISCORD/src/Platform.Worker/appsettings.json)

Пример server-конфига:
- [`deploy/examples/server.translation.sample.json`](/C:/DISCORD/deploy/examples/server.translation.sample.json)

Секрет Yandex:
- не хранить в `appsettings` для production;
- использовать переменную окружения `YANDEX_TRANSLATE_API_KEY`;
- указать `TranslationProviders:Yandex:FolderId`.

## Запуск сервера

### 1. Поднять инфраструктуру

```powershell
docker compose -f deploy/docker-compose.platform.yml up -d
```

### 2. Сгенерировать ключи подписи обновлений

```powershell
pwsh ./scripts/generate-update-signing-key.ps1
```

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

### 7. Прогнать тесты

```powershell
dotnet test SecureLicensePlatform.sln
```

### 8. Выкатить сервер на Ubuntu

Для работающего сервера есть скрипт:
- [`scripts/deploy_server_translation.py`](/C:/DISCORD/scripts/deploy_server_translation.py)

Пример запуска:

```powershell
python .\scripts\deploy_server_translation.py `
  --host 194.116.217.48 `
  --username root `
  --password "<server-password>"
```

Скрипт:
- загружает только серверные проекты и конфиги;
- обновляет env-файлы `api/admin/worker`;
- применяет миграции;
- публикует `API`, `Admin`, `Worker`;
- перезапускает `systemd`-сервисы;
- проверяет `/health/live`, `/Login` и `/api/client/v1/system/info`.

## Seed-данные

Админ:
- логин: `admin`
- пароль: `ChangeThisPassword!`

Demo licenses:
- `SLP-DEMO-0001-0001-0001`
- `SLP-INTERNAL-0001-0001`

Seed также создаёт:
- provider row `yandex`
- базовый glossary `Stellaris Core RU`
- translation quotas для существующих лицензий

## Что уже видно в админке

Новая страница:
- [`Translations.cshtml`](/C:/DISCORD/src/Platform.Admin/Pages/Translations.cshtml)

Что показывает:
- queue status
- translation jobs
- usage по лицензиям
- quotas
- glossary entries
- provider enable/disable

## Клиентский этап 2

Изменённые клиентские проекты:
- `src/Platform.Client.Core`
- `src/Platform.Client.Wpf`
- `tests/Platform.Client.Tests`

Основные новые клиентские модули:
- [`StellarisPathResolver.cs`](/C:/DISCORD/src/Platform.Client.Core/Services/StellarisPathResolver.cs)
- [`StellarisDescriptorParser.cs`](/C:/DISCORD/src/Platform.Client.Core/Services/StellarisDescriptorParser.cs)
- [`StellarisLocalizationParser.cs`](/C:/DISCORD/src/Platform.Client.Core/Services/StellarisLocalizationParser.cs)
- [`StellarisModDiscoveryService.cs`](/C:/DISCORD/src/Platform.Client.Core/Services/StellarisModDiscoveryService.cs)
- [`ClientTranslationApiService.cs`](/C:/DISCORD/src/Platform.Client.Core/Services/ClientTranslationApiService.cs)
- [`SubmodBuildService.cs`](/C:/DISCORD/src/Platform.Client.Core/Services/SubmodBuildService.cs)
- [`ModTranslationViewModel.cs`](/C:/DISCORD/src/Platform.Client.Wpf/ViewModels/ModTranslationViewModel.cs)
- [`MainWindow.xaml`](/C:/DISCORD/src/Platform.Client.Wpf/MainWindow.xaml)

Новые экраны и сценарии клиента:
- `Перевод модов` с каталогом local/workshop модов Stellaris;
- анализ выбранного мода и списка localisation-файлов;
- выбор исходного и целевого языков;
- создание translation job, polling статуса и отмена;
- скачивание результата с сервера;
- preview и сборка отдельного сабмода без изменения оригинала;
- открытие папки мода и папки результата;
- расширенные настройки путей Stellaris/Steam/submod output.

### Конфиг клиента

Пример:
- [`deploy/examples/clientsettings.sample.json`](/C:/DISCORD/deploy/examples/clientsettings.sample.json)

Новые поля:
- `StellarisUserDataPath`
- `SteamRootPath`
- `SubmodOutputRoot`

Клиент по умолчанию работает с сервером:
- `https://194.116.217.48`

### Demo flow клиента

1. Активировать клиент лицензией через уже существующую сессию.
2. Открыть раздел `Перевод модов`.
3. Нажать `Обновить список` и выбрать мод из локального каталога или Steam Workshop.
4. Проверить найденные localisation-файлы и квоту.
5. Нажать `Анализировать мод`.
6. Задать язык, имя сабмода и при необходимости оставить `dry-run`.
7. Нажать `Запустить перевод`.
8. Дождаться статуса `Completed` и скачать архив результата.
9. Нажать `Собрать сабмод` для записи отдельного перевода в каталог модов.
10. Открыть папку сабмода и подключить его в launcher Stellaris.

### Безопасность клиента

- клиент не хранит ключи Yandex и не обращается к провайдеру напрямую;
- все операции перевода идут только через API сервера;
- сабмод создаётся отдельно и не перезаписывает оригинальный мод по умолчанию;
- есть `dry-run`, backup и безопасная запись файлов;
- клиент использует существующую модель лицензий, сессий и обновлений.

## Серверные тесты

Тестовый проект:
- [`Platform.Server.Tests.csproj`](/C:/DISCORD/tests/Platform.Server.Tests/Platform.Server.Tests.csproj)

Что покрыто сейчас:
- safe path handling
- protected token preservation
- localisation parsing
- quota enforcement
- ProblemDetails mapping
- translation job lifecycle до готового архива

## Клиентские тесты

Тестовый проект:
- [`Platform.Client.Tests.csproj`](/C:/DISCORD/tests/Platform.Client.Tests/Platform.Client.Tests.csproj)

Что покрыто сейчас:
- parsing `descriptor.mod`
- parsing Stellaris localisation `.yml`
- dry-run сборка сабмода
- безопасная запись и backup при повторной сборке

## Важное ограничение для production

Серверная часть уже развёрнута и готова принимать analyze/job запросы, но для реального перевода на боевом сервере нужно задать рабочие Yandex credentials:
- `YANDEX_TRANSLATE_API_KEY`
- `TranslationProviders__Yandex__FolderId`
- `TranslationProviders__Yandex__Enabled=true`

Пока эти параметры не заданы, создание translation job будет корректно отвечать ошибкой `provider_unavailable`.

## TODO следующего этапа

- улучшить визуальную полировку WPF-клиента и отдельный экран диагностики переводчика;
- добавить preview diff по строкам до записи сабмода;
- добавить более глубокий разбор Stellaris launcher metadata;
- выпустить полноценный установщик клиента вместо ручного запуска exe;
- настроить боевой Yandex provider на production и прогнать полный e2e-перевод.
