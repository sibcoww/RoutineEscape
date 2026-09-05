# RoutineEscape

RoutineEscape — Telegram-бот на C#/.NET, который превращает обычные и пересланные сообщения в задачи, события, напоминания и заметки. Проект развивается по этапам из [технического задания](RoutineEscape_TZ_v0.1.md).

## Текущий статус

Выполнены этапы 1–9: созданы предметная модель, PostgreSQL-хранилище, Telegram transport, извлечение источника, AI Draft flow и преобразование русских выражений даты/времени с учётом часового пояса пользователя.

## Структура

| Проект | Назначение |
| --- | --- |
| `RoutineEscape.Domain` | Предметная модель и бизнес-правила без внешних зависимостей |
| `RoutineEscape.Application` | Сценарии приложения и абстракции портов |
| `RoutineEscape.Infrastructure` | Хранилище и другие инфраструктурные адаптеры |
| `RoutineEscape.AI` | Адаптер интерпретации сообщений с помощью LLM |
| `RoutineEscape.Bot` | ASP.NET Core host и Telegram transport |
| `RoutineEscape.UnitTests` | Быстрые модульные тесты Domain и Application |
| `RoutineEscape.IntegrationTests` | Интеграционные тесты host и внешних адаптеров |

Направление основных зависимостей:

```text
Bot ──> Application ──> Domain
 │          ▲
 ├──> Infrastructure ──> Domain
 └──> AI ──────────────> Domain
```

## Требования

- .NET SDK 10.0.400 или совместимый feature band

## Сборка и тесты

```powershell
dotnet restore RoutineEscape.slnx
dotnet build RoutineEscape.slnx --no-restore
dotnet test RoutineEscape.slnx --no-build
```

Запуск web-host в режиме разработки:

```powershell
dotnet run --project RoutineEscape.Bot
```

После запуска диагностический endpoint доступен по пути `/health`.

## Локальная PostgreSQL

Скопируйте `.env.example` в `.env`, при необходимости измените локальные значения и запустите:

```powershell
docker compose up -d postgres
dotnet ef database update --project RoutineEscape.Infrastructure
```

Строка подключения для design-time команд читается из `ROUTINEESCAPE_CONNECTION_STRING`. Если переменная не задана, используется конфигурация локального контейнера из примера.

Полный PostgreSQL integration test включается отдельной тестовой строкой подключения:

```powershell
$env:ROUTINEESCAPE_TEST_CONNECTION_STRING = $env:ROUTINEESCAPE_CONNECTION_STRING
dotnet test RoutineEscape.IntegrationTests
```

Без этой переменной PostgreSQL-тест явно помечается как пропущенный; остальные тесты используют изолированное in-memory хранилище.

## Telegram

Для локальной разработки достаточно задать токен и запустить приложение. В окружении `Development` бот автоматически удаляет зарегистрированный webhook и получает updates через long polling:

```powershell
dotnet user-secrets set "Telegram:BotToken" "<bot-token>" --project RoutineEscape.Bot
dotnet run --project RoutineEscape.Bot
```

HTTPS tunnel и регистрация webhook для локального запуска не нужны. `/start` возвращает приветствие, а обычный текст открывает выбор типа Draft.

Для AI-распознавания задайте API-ключ и модель OpenAI-compatible провайдера:

```powershell
dotnet user-secrets set "AI:ApiKey" "<api-key>" --project RoutineEscape.Bot
dotnet user-secrets set "AI:Model" "<model>" --project RoutineEscape.Bot
```

Endpoint по умолчанию — `https://api.openai.com/v1/chat/completions`; другой endpoint можно задать ключом `AI:Endpoint`. Если AI не настроен или временно недоступен, бот продолжает работать с ручным выбором типа.

Текстовое сообщение сохраняется как временный Draft и получает кнопки выбора: задача, событие, напоминание, заметка или отмена. Для этого локальная PostgreSQL должна быть запущена и доступна по строке подключения.

Для будущего production-режима задайте `Telegram__UseLongPolling=false` и `Telegram__WebhookSecret`. Webhook принимает `POST /api/telegram/webhook` и проверяет заголовок `X-Telegram-Bot-Api-Secret-Token`. Токен и webhook secret не должны храниться в `appsettings.json` или Git.

## Конфигурация

Секреты нельзя добавлять в Git. Значения в `.env.example` предназначены только для локальной разработки; реальные пароли и будущие токены передаются через переменные окружения или secret store.
