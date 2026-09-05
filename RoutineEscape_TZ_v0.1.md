# RoutineEscape — техническое задание

**Версия ТЗ:** 0.1  
**Статус:** Draft / MVP planning  
**Платформа:** Telegram Bot  
**Основной стек:** C# / .NET  
**Название проекта:** RoutineEscape

---

## 1. Идея проекта

RoutineEscape — Telegram-бот для автоматизации повседневной рутины.

Главный принцип продукта:

> Пользователь не должен заранее решать, какую команду выбрать и что именно он создаёт. Он просто отправляет или пересылает сообщение, а бот сам анализирует его и предлагает подходящее действие.

Бот должен превращать обычные сообщения в структурированные сущности:

- задачи;
- события;
- напоминания;
- заметки.

В дальнейшем список типов может расширяться.

Пример:

Пользователь пересылает:

> Ермек, завтра в 15:00 созвон по проекту.

Бот анализирует сообщение и предлагает:

**Похоже на событие 📅**

**Созвон по проекту**  
Завтра, 15:00  
Источник: Иван Иванов

Кнопки:

- `✅ Добавить`
- `✏️ Изменить`
- `🔄 Другой тип`
- `❌ Отмена`

Пользователь не обязан предварительно вводить `/event`, `/task` или другую команду.

---

## 2. Основные цели

### 2.1. Цель продукта

Снизить количество действий, необходимых для сохранения информации из Telegram и превращения её в конкретное действие.

Типичный сценарий должен выглядеть так:

```text
Сообщение / Forward
        ↓
RoutineEscape
        ↓
Автоматический анализ
        ↓
Готовое предложение
        ↓
1 нажатие
        ↓
Сохранено
```

### 2.2. Цель проекта как портфолио

Проект должен демонстрировать:

- C# / .NET;
- ASP.NET Core;
- Telegram Bot API;
- работу с webhook;
- Entity Framework Core;
- PostgreSQL;
- фоновую обработку задач;
- планирование уведомлений;
- интеграцию с LLM;
- работу с внешними API;
- Docker;
- GitHub Actions;
- архитектуру приложения;
- тестирование.

---

## 3. Основной UX-принцип

### 3.1. Создание сущностей без обязательных команд

Создание задачи, события, напоминания или заметки не должно требовать команды.

Пользователь может:

1. написать обычный текст;
2. переслать сообщение;
3. отправить сообщение с датой и временем;
4. в будущем — отправить голосовое сообщение.

RoutineEscape должен самостоятельно:

1. определить предполагаемый тип;
2. извлечь полезные данные;
3. сформировать Draft;
4. показать пользователю результат;
5. попросить только подтверждение или исправление.

### 3.2. Команды используются только для навигации

Допускаются команды:

- `/start`
- `/today`
- `/tasks`
- `/events`
- `/notes`
- `/settings`
- `/help`

Команды не являются обязательными для создания новых сущностей.

---

## 4. Типы сущностей MVP

### 4.1. Task — задача

Пример:

> До пятницы отправить документы.

Результат:

```text
Тип: Task
Title: Отправить документы
Deadline: пятница
```

Возможные поля:

- Title
- Description
- Deadline
- Priority
- Source
- Status
- CreatedAt
- CompletedAt

---

### 4.2. Event — событие

Пример:

> В четверг в 15:30 собеседование в офисе.

Результат:

```text
Тип: Event
Title: Собеседование
Date: четверг
Time: 15:30
Location: офис
```

Поля:

- Title
- Description
- StartDateTime
- EndDateTime
- Location
- Source
- CalendarExternalId
- CreatedAt

---

### 4.3. Reminder — напоминание

Пример:

> Через 40 минут выключить духовку.

Результат:

```text
Тип: Reminder
Title: Выключить духовку
TriggerAt: через 40 минут
```

Поля:

- Title
- Description
- TriggerAt
- Source
- IsTriggered
- CreatedAt

---

### 4.4. Note — заметка

Пример:

> Номер аудитории для защиты — 305.

Если действие или дата не обнаружены, бот может предложить сохранить сообщение как заметку.

Поля:

- Title
- Content
- Source
- CreatedAt
- Tags

---

## 5. Обработка входящего сообщения

Каждое входящее сообщение проходит единый pipeline.

```text
Telegram Update
      ↓
Message Normalizer
      ↓
Forward Source Extractor
      ↓
Intent Classifier
      ↓
Entity Extractor
      ↓
Date/Time Resolver
      ↓
Draft Builder
      ↓
Confidence Evaluation
      ↓
Preview Message
      ↓
User Confirmation
      ↓
Persistence
      ↓
Scheduler / Integration
```

---

## 6. Определение типа сообщения

Бот должен определять один из типов:

```text
Task
Event
Reminder
Note
Unknown
```

Результат анализа:

```json
{
  "intent": "Event",
  "confidence": 0.94
}
```

### 6.1. Высокая уверенность

Если `confidence >= 0.80`, бот сразу показывает наиболее вероятный Draft.

Пример:

> Завтра в 16:00 собеседование в офисе Kaspi.

Ответ:

```text
📅 Похоже на событие

Собеседование
Завтра, 16:00
Место: офис Kaspi

[✅ Добавить] [✏️ Изменить]
[🔄 Другой тип] [❌ Отмена]
```

### 6.2. Низкая уверенность

Если бот не уверен:

> завтра универ

Ответ:

```text
Не уверен, как лучше сохранить сообщение.

[📅 Событие]
[✅ Задача]
[⏰ Напоминание]
[📝 Заметка]
[❌ Отмена]
```

После выбора бот повторно строит Draft уже для указанного типа.

### 6.3. Никаких автоматических сохранений без подтверждения в MVP

На первой версии RoutineEscape не должен молча создавать сущность.

Любой автоматически распознанный объект сначала показывается пользователю.

Позже можно добавить настройку:

```text
Автоматически сохранять при confidence >= X
```

По умолчанию она выключена.

---

## 7. Пересланные сообщения

Пересылка сообщений — одна из главных функций RoutineEscape.

Пользователь должен иметь возможность просто переслать любое доступное для пересылки Telegram-сообщение в чат с ботом.

Бот должен:

1. определить, что сообщение переслано;
2. сохранить содержимое;
3. извлечь данные об источнике;
4. использовать текст для классификации;
5. показать источник в Draft;
6. сохранить информацию об источнике вместе с сущностью.

---

## 8. Источник пересланного сообщения

Telegram Bot API предоставляет информацию о происхождении пересланного сообщения через `forward_origin`.

Нужно поддерживать четыре варианта источника.

### 8.1. Известный пользователь

Тип:

```text
MessageOriginUser
```

Сохранять при наличии:

- Telegram User ID;
- FirstName;
- LastName;
- Username;
- DisplayName;
- OriginalMessageDate.

Отображение:

```text
Источник: Иван Иванов (@ivan)
```

Если username отсутствует:

```text
Источник: Иван Иванов
```

---

### 8.2. Пользователь со скрытыми данными

Тип:

```text
MessageOriginHiddenUser
```

Telegram не передаёт объект пользователя, но передаёт строку с именем отправителя.

Сохранять:

- SenderDisplayName;
- OriginalMessageDate;
- IsHiddenUser = true.

Пример:

```text
Источник: Иван Иванов
```

В этом случае:

- нельзя предполагать Telegram ID;
- нельзя придумывать username;
- нельзя пытаться связать это имя с другим пользователем;
- имя хранится только как отображаемая строка из Telegram.

---

### 8.3. Чат / группа

Тип:

```text
MessageOriginChat
```

Сохранять:

- ChatId;
- ChatTitle;
- ChatUsername — если доступен;
- AuthorSignature — если доступна;
- OriginalMessageDate.

Отображение:

```text
Источник: Рабочая группа
```

При наличии подписи:

```text
Источник: Рабочая группа · Иван
```

---

### 8.4. Канал

Тип:

```text
MessageOriginChannel
```

Сохранять:

- ChannelId;
- ChannelTitle;
- ChannelUsername — если доступен;
- OriginalMessageId;
- AuthorSignature — если доступна;
- OriginalMessageDate.

Отображение:

```text
Источник: Название канала
```

---

## 9. Модель Source

Предлагаемая общая модель:

```text
MessageSource
-------------------------
Id
SourceType
TelegramUserId?
TelegramChatId?
TelegramMessageId?
Username?
DisplayName?
ChatTitle?
AuthorSignature?
OriginalMessageDate?
IsHiddenUser
RawMetadataJson?
```

`SourceType`:

```text
Direct
ForwardedUser
ForwardedHiddenUser
ForwardedChat
ForwardedChannel
```

Для обычного сообщения пользователя:

```text
SourceType = Direct
```

Источник хранится отдельно от Task/Event/Reminder/Note и может быть связан с созданной сущностью.

---

## 10. Сохранение оригинального сообщения

Для созданной сущности желательно сохранять:

```text
OriginalText
OriginalTelegramMessageId
OriginalChatId
MessageSourceId
```

Это позволит в будущем:

- показывать исходный текст;
- понимать, откуда появилась задача;
- строить историю;
- улучшать повторный AI-анализ;
- реализовать кнопку «Показать оригинал».

Не нужно сохранять лишнюю информацию, которая не требуется для работы продукта.

---

## 11. Извлечение данных

Из текста необходимо пытаться извлечь:

- действие;
- название;
- дату;
- время;
- deadline;
- интервал;
- место;
- человека;
- повторяемость;
- приоритет;
- дополнительные детали.

Пример:

> Скинь Диме документы до среды вечером.

Draft:

```text
Type: Task
Title: Отправить Диме документы
Deadline: среда, вечер
Person: Дима
```

---

## 12. Работа с датой и временем

Бот должен понимать:

- сегодня;
- завтра;
- послезавтра;
- через N минут;
- через N часов;
- через N дней;
- в пятницу;
- в следующую пятницу;
- 12 сентября;
- 12.09;
- в 15:00;
- в 7 вечера;
- утром;
- днём;
- вечером.

### 12.1. Неоднозначная дата

Если дата неоднозначна, бот должен уточнить её кнопками или вопросом.

Пример:

> в пятницу созвон

Если возможна неоднозначность:

```text
Какую пятницу вы имеете в виду?

[11 сентября]
[18 сентября]
```

### 12.2. TimeZone

У каждого пользователя хранится:

```text
TimeZoneId
```

Для первоначальной версии можно использовать `Asia/Almaty` как удобное значение по умолчанию для основной аудитории проекта, но пользователь должен иметь возможность изменить часовой пояс в настройках.

Внутри БД даты событий рекомендуется хранить в UTC, а показывать и интерпретировать относительно TimeZone пользователя.

---

## 13. Редактирование Draft

После автоматического анализа пользователь должен иметь возможность исправить результат.

Кнопка:

```text
✏️ Изменить
```

Для Task:

- название;
- deadline;
- приоритет;
- описание.

Для Event:

- название;
- дата;
- время;
- место.

Для Reminder:

- текст;
- дата/время.

Для Note:

- название;
- текст;
- теги.

MVP может использовать пошаговый Telegram UI.

Mini App на первой версии не требуется.

---

## 14. Повторяющиеся задачи

Этап после базового MVP.

Примеры:

> Каждый понедельник отправлять отчёт.

> 5 числа каждого месяца оплачивать интернет.

> Каждые 3 дня поливать цветы.

Модель:

```text
RecurrenceRule
```

Поддержать минимум:

- daily;
- weekly;
- monthly;
- custom interval.

В дальнейшем можно перейти на стандартное представление recurrence/cron там, где это оправдано.

---

## 15. Уведомления

RoutineEscape должен отправлять уведомления для:

- Reminder;
- Task с Deadline;
- Event.

### Task

Настройки по умолчанию:

- за 1 день;
- за 1 час.

### Event

Настройки:

- за 1 день;
- за 1 час;
- пользовательская настройка.

### Reminder

Уведомление отправляется в `TriggerAt`.

Кнопки:

```text
[✅ Выполнено]
[⏰ +10 минут]
[⏰ +1 час]
[📅 Перенести]
```

---

## 16. Ежедневный дайджест

Этап после MVP.

Пример:

```text
☀️ Доброе утро!

На сегодня:

🔴 10:00 — отправить документы
📅 14:00 — собеседование
✅ Купить продукты

3 активных пункта.
```

Настройки:

- включён / выключен;
- время;
- TimeZone.

---

## 17. Google Calendar

Интеграция после завершения базового MVP.

Для Event пользователь может:

```text
[✅ Сохранить]
[📅 Сохранить + Google Calendar]
```

После подключения аккаунта можно добавить настройку:

```text
Автоматически синхронизировать события с Google Calendar
```

Нужно хранить:

- GoogleCalendarId;
- ExternalEventId;
- SyncStatus.

OAuth tokens должны храниться безопасно.

---

## 18. Голосовые сообщения

Не входит в первую реализацию MVP.

Будущий pipeline:

```text
Voice Message
     ↓
Download
     ↓
Speech-to-Text
     ↓
Text
     ↓
Standard RoutineEscape Pipeline
```

После транскрипции дальнейшая обработка полностью совпадает с обычным текстом.

---

## 19. AI / LLM

LLM используется для:

- intent detection;
- semantic parsing;
- извлечения сущностей;
- нормализации названия;
- обработки сложных естественных формулировок.

AI не должен напрямую менять БД.

Правильный поток:

```text
AI
 ↓
Structured Draft
 ↓
Validation
 ↓
User Confirmation
 ↓
Application Service
 ↓
Database
```

### 19.1. Structured Output

AI должен возвращать строго структурированный ответ.

Пример:

```json
{
  "intent": "event",
  "confidence": 0.93,
  "title": "Собеседование",
  "description": null,
  "date_expression": "завтра",
  "time_expression": "15:00",
  "location": "офис",
  "person": null
}
```

Затем приложение самостоятельно:

1. валидирует JSON;
2. преобразует дату;
3. проверяет поля;
4. формирует Draft.

---

## 20. AI fallback и rule-based обработка

Не нужно отправлять абсолютно каждое сообщение в LLM без необходимости.

Простые конструкции можно предварительно определять обычной логикой:

- `через 10 минут`;
- `завтра`;
- `в 15:00`;
- `каждый понедельник`.

При этом для MVP допускается сначала реализовать единый AI parser, а затем вынести очевидные случаи в rule-based preprocessor.

Ключевое архитектурное требование:

```text
IMessageInterpreter
```

Реализация интерпретатора должна быть заменяемой.

Примеры:

```text
LlmMessageInterpreter
RuleBasedMessageInterpreter
CompositeMessageInterpreter
```

---

## 21. Технологический стек

### Backend

```text
C#
.NET
ASP.NET Core
```

### Telegram

```text
Telegram Bot API
Telegram.Bot
Webhook
Inline Keyboard
```

### Data

```text
PostgreSQL
Entity Framework Core
```

### Background jobs

Рассмотреть:

```text
Quartz.NET
```

или отдельный `BackgroundService`.

Для MVP предпочтительно не усложнять инфраструктуру без необходимости.

### AI

Абстракция над выбранным LLM-провайдером.

Не привязывать бизнес-логику напрямую к конкретному API.

### Deployment

```text
Docker
docker-compose
```

### CI

```text
GitHub Actions
```

---

## 22. Предлагаемая структура solution

```text
RoutineEscape.sln

src/
  RoutineEscape.Bot/
  RoutineEscape.Application/
  RoutineEscape.Domain/
  RoutineEscape.Infrastructure/
  RoutineEscape.AI/

tests/
  RoutineEscape.UnitTests/
  RoutineEscape.IntegrationTests/
```

### RoutineEscape.Bot

- webhook;
- Telegram handlers;
- callback handlers;
- Telegram UI;
- middleware.

### RoutineEscape.Application

- use cases;
- services;
- DTO;
- interfaces;
- orchestration.

### RoutineEscape.Domain

- User;
- Task;
- Event;
- Reminder;
- Note;
- MessageSource;
- enums;
- domain rules.

### RoutineEscape.Infrastructure

- EF Core;
- PostgreSQL;
- repositories;
- migrations;
- scheduler;
- external integrations.

### RoutineEscape.AI

- prompt building;
- structured response;
- parser;
- provider adapters.

---

## 23. Основные сущности БД

### AppUser

```text
Id
TelegramUserId
Username
FirstName
LastName
TimeZoneId
Language
CreatedAt
UpdatedAt
```

### TaskItem

```text
Id
UserId
Title
Description
DeadlineUtc?
Priority
Status
SourceId?
OriginalText?
CreatedAt
CompletedAt?
```

### CalendarEvent

```text
Id
UserId
Title
Description
StartUtc
EndUtc?
Location?
SourceId?
ExternalCalendarId?
CreatedAt
```

### Reminder

```text
Id
UserId
Title
Description?
TriggerAtUtc
Status
SourceId?
CreatedAt
TriggeredAt?
```

### Note

```text
Id
UserId
Title?
Content
SourceId?
CreatedAt
UpdatedAt
```

### MessageSource

```text
Id
SourceType
TelegramUserId?
TelegramChatId?
TelegramMessageId?
Username?
DisplayName?
ChatTitle?
AuthorSignature?
OriginalMessageDateUtc?
IsHiddenUser
RawMetadataJson?
CreatedAt
```

### Draft

Draft может храниться в БД или во временном хранилище.

```text
Id
UserId
TelegramMessageId
Intent
Confidence
PayloadJson
Status
ExpiresAt
CreatedAt
```

---

## 24. Состояние Draft

```text
Pending
Confirmed
Cancelled
Expired
```

Draft нужен, чтобы callback-кнопки не содержали всю информацию сущности.

CallbackData должна содержать только безопасный короткий идентификатор действия / Draft.

---

## 25. Telegram UI

### Кнопки после анализа

```text
[✅ Добавить]
[✏️ Изменить]

[🔄 Другой тип]
[❌ Отмена]
```

### Задача после создания

```text
✅ Задача создана

Отправить документы
До: 11 сентября, 18:00
Источник: Иван Иванов

[✅ Выполнено]
[✏️ Изменить]
[🗑 Удалить]
```

### Событие после создания

```text
📅 Событие добавлено

Собеседование
11 сентября, 15:00
Офис

[✏️ Изменить]
[🗑 Удалить]
```

---

## 26. `/start`

Пример первого сообщения:

```text
👋 RoutineEscape помогает разгружать голову.

Просто напиши или перешли мне сообщение.

Я сам предложу, сохранить его как:
✅ задачу,
📅 событие,
⏰ напоминание
или 📝 заметку.

Не нужно заранее выбирать команду.
```

После этого настройка TimeZone.

---

## 27. Ошибки

Пользователь не должен видеть stack trace или технический текст.

Пример:

```text
Не получилось разобрать сообщение.

Можешь выбрать тип вручную:

[✅ Задача]
[📅 Событие]
[⏰ Напоминание]
[📝 Заметка]
```

Если LLM недоступен, бот не должен полностью переставать работать.

Должны продолжать работать:

- просмотр существующих сущностей;
- выполнение задач;
- удаление;
- перенос напоминаний;
- настройки.

---

## 28. Безопасность

Обязательно:

- Telegram Bot Token только через environment variables / secrets;
- API keys только через secrets;
- никаких ключей в Git;
- логирование без полного содержимого приватных сообщений по умолчанию;
- проверка UserId при любом callback;
- пользователь может работать только со своими сущностями;
- защита webhook;
- валидация AI response;
- ограничение длины входящих данных;
- rate limiting для дорогих AI-запросов.

---

## 29. Логирование

Логировать:

- UpdateId;
- тип update;
- UserId;
- выбранный intent;
- confidence;
- результат обработки;
- ошибки;
- длительность AI-вызова.

Не логировать по умолчанию:

- Bot Token;
- API keys;
- OAuth tokens;
- полный текст личного сообщения в production-логах.

---

## 30. Тестирование

### Unit tests

Проверить:

- date/time resolver;
- intent result validation;
- Draft Builder;
- MessageOrigin → MessageSource mapping;
- permissions;
- Task/Event/Reminder validation;
- callback parsing.

### Integration tests

Проверить:

- PostgreSQL persistence;
- EF Core;
- Telegram update processing;
- scheduler;
- AI structured response parsing.

### Особо важные тесты Forward Source

Нужны тесты для:

1. известного User;
2. HiddenUser;
3. Chat;
4. Channel;
5. обычного непересланного сообщения;
6. отсутствующих необязательных полей.

---

# 31. Этапы разработки для ИИ / Codex

Каждый этап выполняется отдельно.

После каждого этапа:

1. проект должен собираться;
2. существующие тесты должны проходить;
3. новые функции должны иметь тесты там, где это разумно;
4. не начинать следующий этап до завершения текущего;
5. не переписывать уже работающую архитектуру без необходимости.

---

## Этап 1. Создание solution

Создать структуру:

```text
RoutineEscape.Bot
RoutineEscape.Application
RoutineEscape.Domain
RoutineEscape.Infrastructure
RoutineEscape.AI
RoutineEscape.UnitTests
RoutineEscape.IntegrationTests
```

Настроить зависимости между проектами.

Добавить базовый `.gitignore`.

Добавить `README.md`.

Критерий готовности:

```text
dotnet build
dotnet test
```

проходят успешно.

---

## Этап 2. Domain model

Реализовать:

- AppUser;
- TaskItem;
- CalendarEvent;
- Reminder;
- Note;
- MessageSource;
- Draft;
- enums.

Без Telegram API и БД.

Добавить unit tests базовых правил.

---

## Этап 3. PostgreSQL + EF Core

Реализовать:

- DbContext;
- entity configurations;
- migrations;
- PostgreSQL;
- repositories / persistence abstractions.

Добавить docker-compose для локальной PostgreSQL.

---

## Этап 4. Telegram Bot skeleton

Подключить Telegram.Bot.

Реализовать:

- `/start`;
- webhook endpoint;
- update dispatcher;
- text message handler;
- callback query handler;
- базовую обработку ошибок.

На этом этапе любое сообщение можно временно отвечать:

```text
Сообщение получено.
```

---

## Этап 5. Forward Source Extractor

Реализовать обработку:

```text
MessageOriginUser
MessageOriginHiddenUser
MessageOriginChat
MessageOriginChannel
```

Создать:

```text
IMessageSourceExtractor
TelegramMessageSourceExtractor
```

Добавить unit tests на каждый тип.

Бот должен уметь показать:

```text
Источник: ...
```

для пересланного сообщения.

---

## Этап 6. Draft flow без AI

На любое текстовое сообщение создавать временный Draft.

Пока intent можно задавать `Unknown`.

Показать кнопки:

```text
[✅ Задача]
[📅 Событие]
[⏰ Напоминание]
[📝 Заметка]
[❌ Отмена]
```

После выбора создать соответствующую сущность.

Это позволит полностью проверить UX до AI.

---

## Этап 7. AI Interpreter

Создать интерфейс:

```text
IMessageInterpreter
```

Реализовать LLM adapter.

AI должен возвращать structured result:

```text
Intent
Confidence
Title
Description
DateExpression
TimeExpression
Location
Person
```

Строго валидировать результат.

---

## Этап 8. Автоматический Draft

Подключить AI к стандартному message pipeline.

Если confidence высокий:

- сразу показать предполагаемый тип.

Если низкий:

- предложить типы вручную.

Ничего не сохранять без подтверждения.

---

## Этап 9. Date/Time Resolver

Реализовать преобразование:

```text
завтра
через 2 часа
в пятницу
12 сентября
в 7 вечера
```

в конкретный `DateTimeOffset`.

Добавить TimeZone пользователя.

Покрыть unit tests.

---

## Этап 10. Task flow

Реализовать полностью:

- создание;
- просмотр;
- завершение;
- редактирование;
- удаление;
- deadline.

---

## Этап 11. Reminder flow

Реализовать:

- TriggerAt;
- background scheduler;
- отправку уведомления;
- `+10 минут`;
- `+1 час`;
- перенос;
- отмену.

---

## Этап 12. Event flow

Реализовать:

- дата;
- время;
- location;
- редактирование;
- удаление;
- уведомления.

---

## Этап 13. Note flow

Реализовать:

- сохранение;
- просмотр;
- редактирование;
- удаление.

---

## Этап 14. Навигация

Добавить:

```text
/today
/tasks
/events
/notes
/settings
/help
```

Пагинацию для длинных списков.

---

## Этап 15. Повторяющиеся задачи

Добавить recurrence.

Не смешивать реализацию recurrence с базовыми напоминаниями до завершения MVP.

---

## Этап 16. Google Calendar

Добавить OAuth и Google Calendar integration.

Сначала ручная кнопка синхронизации.

Автоматическую синхронизацию добавить позже.

---

## Этап 17. Docker + deployment

Создать:

- Dockerfile;
- docker-compose;
- production configuration;
- health endpoint.

---

## Этап 18. GitHub Actions

Pipeline:

```text
restore
build
test
```

Опционально:

```text
docker build
```

---

## Этап 19. Полировка

Добавить:

- README;
- архитектурную схему;
- screenshots;
- GIF demo;
- topics;
- нормальное описание GitHub repository.

---

# 32. MVP Definition of Done

MVP считается готовым, когда пользователь может:

1. запустить `/start`;
2. отправить обычное сообщение;
3. переслать сообщение от другого человека / чата / канала;
4. увидеть корректно распознанный источник;
5. получить предполагаемый тип;
6. изменить тип;
7. подтвердить Draft;
8. создать Task;
9. создать Event;
10. создать Reminder;
11. создать Note;
12. получить напоминание в нужное время;
13. посмотреть свои задачи и события;
14. завершить задачу;
15. удалить сущность;
16. изменить настройки TimeZone.

Дополнительно:

- данные хранятся в PostgreSQL;
- проект запускается через Docker;
- тесты проходят;
- secrets отсутствуют в репозитории;
- GitHub Actions выполняет build/test.

---

# 33. Не входит в первый MVP

Не реализовывать до готовности основной версии:

- Telegram Mini App;
- голосовые сообщения;
- OCR изображений;
- распознавание чеков;
- финансовый учёт;
- совместные задачи;
- команды для групп;
- сложная ролевая модель;
- веб-панель;
- полноценный собственный календарь;
- мобильное приложение.

---

# 34. Возможности после MVP

После стабильного MVP можно добавить:

- Google Calendar;
- голосовой ввод;
- обработку изображений;
- расходы;
- контакты / follow-up;
- повторяющиеся задачи;
- утренний и вечерний дайджест;
- привычки;
- автоматические категории;
- поиск по сохранённым данным;
- AI summary дня;
- анализ входящих сообщений;
- Mini App с календарём и списком задач.

---

# 35. Главный продуктовый критерий

При проектировании любой новой функции задавать вопрос:

> Можно ли выполнить это действие, не заставляя пользователя сначала выбирать категорию или команду?

Если можно определить намерение автоматически — RoutineEscape должен сначала предложить результат, а не заставлять пользователя заполнять форму.

RoutineEscape должен быть не Telegram-интерфейсом к Todo List, а системой, которая сама превращает входящую информацию в понятные действия.
