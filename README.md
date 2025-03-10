# Catering Service

## Описание

Catering Service — это микросервис для управления процессом доставки питания (кейтеринг). Он предназначен для оркестрации доставки питания для авиарейсов, получая запросы от оркестратора с информацией о рейсе и требуемых заказах питания. Сервис реализует следующие функции:

- **Запросы на доставку питания:** Принимает запросы, содержащие:
  - **AircraftId** — идентификатор самолёта.
  - **Meals** — массив заказов питания, где для каждого типа (например, "Standard", "Vegetarian", "Vegan", "Gluten-Free") указывается количество порций.
- **Распределение заказов:** Обрабатывает запросы, распределяя их между транспортными средствами (кейтеринг-машинами) с учетом глобальных ограничений:
  - Глобальный лимит машин в системе.
  - Максимальное количество машин, одновременно обслуживающих один самолёт.
- **Обработка ошибок:**
  - Возвращает HTTP 400, если отсутствуют обязательные поля или переданы некорректные данные.
  - Возвращает HTTP 500 при возникновении внутренних ошибок сервера.
- **Реальное обновление статусов:** Статусы транспортных средств обновляются в реальном времени с использованием SignalR.
- **Админский веб-интерфейс:** Позволяет:
  - Управлять режимом работы (Mock/Real).
  - Изменять вместимость транспортных средств.
  - Формировать тестовые запросы через удобную форму.
  - Наблюдать обновления статусов транспортных средств в режиме реального времени.

### Инструкция по запуску

**Клонирование репозитория:**

   ```bash
   git clone https://github.com/reaport/catering.git
   cd CateringService

```

Использование API
Эндпоинты
Запрос на доставку питания
URL: POST /request
Описание: Инициирует доставку питания, принимая следующие данные:
FlightId (string, обязательный): Идентификатор рейса.
Meals (array, обязательный): Массив заказов питания. Каждый заказ содержит:
mealType (string): Тип питания (например, "Standard", "Vegetarian", "Vegan", "Gluten-Free").
count (integer, неотрицательный): Количество порций.
Успешный ответ (200):
```json
{
  "Status": "success",
  "Waiting": false
}
```

Ошибки:
400: Если отсутствует обязательное поле или переданы некорректные данные.
```json
{
  "errorCode": 101,
  "message": "Each meal order must have a valid mealType and a non-negative count"
}
```

500: Внутренняя ошибка сервера.


**Получение типов питания**
URL: GET /mealtypes
Описание: Возвращает список доступных типов питания.
Успешный ответ (200):
```json
{
  "mealTypes": ["Standard", "Vegetarian", "Vegan", "Gluten-Free"]
}
```
Ошибки:
500: Внутренняя ошибка сервера.


Админский интерфейс
Админский интерфейс предоставляет следующие возможности:

Переключение режима работы: 
Выбор между Mock и Real режимами взаимодействия с внешними системами.

Изменение вместимости транспортных средств: Возможность обновления вместимости кейтеринг-машин.

Формирование тестового запроса: Удобная форма для ввода AircraftId и заказов питания.

Мониторинг транспортных средств: Таблица, обновляемая в режиме реального времени через SignalR, отображающая ID машин, базовый узел, статус (Busy/Available) и сервисные точки.
