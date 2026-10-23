# Деплой на свой сервер (Ubuntu/Debian) без Docker

Схема: **nginx** отдаёт собранный фронтенд и проксирует `/api` на **.NET** на `127.0.0.1:5080`. Один домен, без CORS-проблем.

Подразумевается, что у домена уже есть A-запись на IP сервера.

## 1. Один раз на сервере

```bash
sudo apt update
sudo apt install -y nginx git
# .NET 8 runtime + sdk (для publish на сервере)
sudo apt install -y dotnet-sdk-8.0      # если пакета нет: https://learn.microsoft.com/dotnet/core/install/linux
# Node.js 20+ для сборки фронта
curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash - && sudo apt install -y nodejs
```

## 2. Код и сборка

```bash
sudo mkdir -p /opt/volanchiki && sudo chown $USER /opt/volanchiki
git clone https://github.com/ukolovamargarita/volanchiki.git /opt/volanchiki/repo
cd /opt/volanchiki/repo

# бэкенд
dotnet publish src/Volanchiki.Api/Volanchiki.Api.csproj -c Release -o /opt/volanchiki/publish

# фронтенд
cd frontend && npm ci && npm run build
sudo mkdir -p /var/www/volanchiki && sudo cp -r dist/* /var/www/volanchiki/
```

Если `global.json` требует SDK 9, а у вас 8: временно удалите `global.json` или поставьте нужный SDK.

## 3. Настройки и данные

```bash
cp /opt/volanchiki/repo/.env.example /opt/volanchiki/.env
nano /opt/volanchiki/.env        # впишите LLM_API_KEY
# данные кейса рядом с приложением
cp -r /opt/volanchiki/repo/read_only /opt/volanchiki/read_only
```

Приложение ищет `.env` и папки данных относительно рабочей директории `/opt/volanchiki`.

## 4. Сервис бэкенда

```bash
sudo cp /opt/volanchiki/repo/deploy/volanchiki.service /etc/systemd/system/
sudo chown -R www-data /opt/volanchiki
sudo systemctl daemon-reload
sudo systemctl enable --now volanchiki
sudo systemctl status volanchiki         # должен быть active (running)
curl http://127.0.0.1:5080/api/health    # dataLoaded: true, llmConfigured: true
```

Логи: `journalctl -u volanchiki -f`. Первый старт читает 68 МБ CSV — подождите несколько секунд.

## 5. nginx

```bash
sudo cp /opt/volanchiki/repo/deploy/nginx.conf /etc/nginx/sites-available/volanchiki
sudo sed -i 's/DOMAIN/ваш.домен.ру/' /etc/nginx/sites-available/volanchiki
sudo ln -s /etc/nginx/sites-available/volanchiki /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

HTTPS (по желанию, но лучше): `sudo apt install -y certbot python3-certbot-nginx && sudo certbot --nginx -d ваш.домен.ру`.

## 6. Обновление после новых коммитов

```bash
cd /opt/volanchiki/repo && git pull
dotnet publish src/Volanchiki.Api/Volanchiki.Api.csproj -c Release -o /opt/volanchiki/publish
(cd frontend && npm ci && npm run build && sudo cp -r dist/* /var/www/volanchiki/)
sudo systemctl restart volanchiki
```

## Если что-то не работает

| Симптом | Причина |
|---|---|
| Белая страница | фронт не скопирован в `/var/www/volanchiki` или в nginx неверный `root` |
| В интерфейсе «API недоступен» | не запущен сервис: `systemctl status volanchiki` |
| 502 от nginx | бэкенд упал или слушает другой порт |
| «LLM-ключ не задан» | пустой `LLM_API_KEY` в `/opt/volanchiki/.env`; после правки `systemctl restart volanchiki` |
| 504 на «Получить инсайт» | в nginx нет `proxy_read_timeout 180s` |
| `dataLoaded: false` | не найдена папка `read_only` относительно `/opt/volanchiki` |

## Где хостить, если нет своего сервера
GitHub Pages отдаёт только статику, бэкенд на .NET там не запустится. Поэтому нужен ваш сервер (или любой VPS/PaaS).
