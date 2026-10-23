# volanchiki

Хакатон Альфа: бэкенд, который по компании и новости собирает персональный инсайт.

## Что нужно

- .NET 8 SDK
- CSV в `read_only/company/`
- аналитика банка (txt) в `read_only/from_alfa/`
- ключ LLM в `.env`

## Запуск

Из корня репо:

```powershell
copy .env.example .env
# в .env вставь LLM_API_KEY

dotnet run --project src/Volanchiki.Api --launch-profile http
```

## Эндпоинты

| Метод | URL | Зачем |
|--------|-----|--------|
| GET | `/api/health` | живость |
| GET | `/api/companies/demo?take=5` | компании для демо |
| GET | `/api/companies/{id}` | профиль |
| GET | `/api/companies?q=...` | поиск |
| POST | `/api/insights/run` | инсайт (долго, 20–90 сек) |

Из PowerShell (не через `curl` — в PS это другой cmdlet):

```powershell
$body = @{
  companyId = "U_21481"
  newsHint  = "Сохранение ключевой ставки ЦБ на уровне 14%"
} | ConvertTo-Json

Invoke-RestMethod -Method Post `
  -Uri http://localhost:5080/api/insights/run `
  -ContentType "application/json; charset=utf-8" `
  -Body $body
```

## Postman

Коллекция лежит в `postman/volanchiki.postman_collection.json`.
