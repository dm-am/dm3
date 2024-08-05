# Установка и запуск для разработчиков
Нужно установить на компьютер Docker for Desktop последней версии. Для пользователей Windows настоятельно рекомендуется переключиться с Hyper-V на WSL2 в качестве движка виртуализации. В разработке мы пользуемся только вторым, поэтому не гарантируем корректную работу на Hyper-V.


## Запуск для локальной работы с апи
В основной директрии выполнить `docker compose -f ./DM/docker-compose.yml up -d --build`

api доступен по `http://localhost:5051/`

Для остановки `docker compose -f ./DM/docker-compose.yml stop`

Для полной остановки с удалением контейнеров `docker compose -f ./DM/docker-compose.yml down`

При первом запуске:
- Открыть в браузере `http://localhost:9001`, войти под `minio:miniokey`
- Перейти на вкладку "Buckets", создать новый бакет под именем `dm-uploads`, в настройках бакета после создания изменить уровень доступа с `Private` на `Public`


## Дополнительно для разработки api 
Установить dotnet SDK 8.0

Запускать из основной директории командой `dotnet run --project ./DM/Web/DM.Web.API/DM.Web.API.csproj`

api доступен по `http://localhost:5000/`


## Работа с почтовой рассылкой

Email-ы отправляются в mailhog, который доступен по адресу `http://localhost:5025`.
