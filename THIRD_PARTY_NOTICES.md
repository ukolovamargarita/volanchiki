# Сторонние компоненты

| Компонент | Версия | Назначение | Лицензия |
|---|---|---|---|
| ASP.NET Core / .NET | 8 | бэкенд | MIT |
| CsvHelper | 33.1.0 | чтение CSV | MS-PL / Apache-2.0 |
| DotNetEnv | 3.2.0 | чтение .env | MIT |
| React, React DOM | 18.3.1 | фронтенд | MIT |
| Vite | 5.4.11 | сборка фронтенда | MIT |
| TypeScript | 5.6.3 | типизация | Apache-2.0 |
| Vitest / Testing Library | см. package-lock | тесты фронтенда | MIT |
| xUnit / Microsoft.NET.Test.Sdk | см. csproj тестов | тесты бэкенда | Apache-2.0 / MIT |

## Модели LLM (шлюз хакатона, Приложение 3)

| Роль в коде | Идентификатор модели |
|---|---|
| Light (`LLM_MODEL_LIGHT`) | `google/gemma-4-26b-a4b-it` |
| Reasoning (`LLM_MODEL_REASONING`) | `z-ai/glm-5.3-flash` |
| Default (`LLM_MODEL_DEFAULT`) | `qwen/qwen3.6-35b-a3b` |
| Research (`LLM_MODEL_RESEARCH`) | `deepseek/deepseek-v4.1-flash` |

Выбор роли — `Llm/LlmClient.cs` (`LlmRole`). Доступ только через выданный организаторами OpenAI-совместимый шлюз.

## Данные и контент
- `read_only/company/*.csv` — обезличенные данные клиентов, выданы организаторами.
- `read_only/from_alfa/*.txt` — аналитика ЦМИ Альфа-Банка, выдана организаторами.
- Внешние новости — публичные RSS: ТАСС, РИА Экономика, Lenta Экономика, Интерфакс (`News:Feeds` в appsettings). Заголовки/аннотации кэшируются в памяти. Права на контент — у правообладателей лент; использование — агрегация для демо хакатона.
- Синтетические темы: `frontend/src/events.ts` и seed-новости бэкенда (офлайн-демо).
- UI «Альфа-Бизнес» — **смоделированная** точка входа интернет-банка для прототипа хакатона, не официальный продукт банка.

## Шрифты и изображения
Системные шрифты; внешних изображений нет.
