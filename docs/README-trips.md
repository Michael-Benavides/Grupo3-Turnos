# Trips & Scheduling API

Microservicio ASP.NET Core en VB.NET para administrar plantillas de horarios y viajes interprovinciales, calcular tarifas dinámicas e inventariar los 40 asientos de cada viaje. Persiste sus datos en SQL Server (`Trips_DB`) y obtiene rutas mediante `IRoutesClient` (fake configurable o `HttpClient`).

## Levantar con Docker

Configura `SA_PASSWORD` en `.env` (o copia `.env.example` y establece una contraseña fuerte) y, desde la raíz del repositorio, ejecuta:

```powershell
docker compose up --build -d
docker compose ps
```

La API queda en `http://localhost:5001`; Swagger en `/swagger` y salud en `/health`. Compose espera a que SQL Server esté saludable. En `Development`, la API aplica las migraciones al arrancar. La contraseña y el archivo de configuración local no se incluyen en la imagen.

## Endpoints

Los enums se envían y reciben como cadenas. Los identificadores son GUID. Las respuestas de error siguen `ProblemDetails`; reglas de dominio violadas devuelven `422`, conflictos de concurrencia `409` y recursos inexistentes `404`.

### Salud

| Método y ruta | Request | Response |
|---|---|---|
| `GET /health` | — | `200` `{"status":"ok","service":"trips"}` |

### Plantillas de horario

| Método y ruta | Ejemplo de request | Ejemplo de response |
|---|---|---|
| `POST /api/schedules` | `{"routeId":"11111111-1111-1111-1111-111111111111","busId":"22222222-2222-2222-2222-222222222222","shift":"Morning","departureTime":"08:00:00","basePrice":10.00}` | `201` `{"id":"33333333-3333-3333-3333-333333333333","routeId":"11111111-1111-1111-1111-111111111111","busId":"22222222-2222-2222-2222-222222222222","shift":"Morning","departureTime":"08:00:00","basePrice":10.00,"isActive":true}` |
| `GET /api/schedules?routeId={guid}` | — (filtro opcional) | `200` `[{"id":"33333333-3333-3333-3333-333333333333","routeId":"11111111-1111-1111-1111-111111111111","busId":"22222222-2222-2222-2222-222222222222","shift":"Morning","departureTime":"08:00:00","basePrice":10.00,"isActive":true}]` |
| `PUT /api/schedules/{id}` | Mismo JSON que `POST` | `200` objeto de plantilla actualizado |
| `DELETE /api/schedules/{id}` | — | `204` sin contenido; desactiva la plantilla |

### Viajes y tarifas

| Método y ruta | Ejemplo de request | Ejemplo de response |
|---|---|---|
| `POST /api/trips/generate` | `{"fechaDesde":"2026-11-16","fechaHasta":"2026-11-17","routeId":"11111111-1111-1111-1111-111111111111"}` (`routeId` es opcional) | `200` `{"createdCount":2,"skippedCount":0}` |
| `GET /api/trips?routeId={guid}&date=2026-11-16&shift=Morning` | Todos los filtros son opcionales | `200` `[{"id":"44444444-4444-4444-4444-444444444444","templateId":"33333333-3333-3333-3333-333333333333","routeId":"11111111-1111-1111-1111-111111111111","busId":"22222222-2222-2222-2222-222222222222","departureAt":"2026-11-16T08:00:00","shift":"Morning","status":"Scheduled","currentPrice":10.00,"availableSeats":40}]` |
| `GET /api/trips/{id}/price` | — | `200` `{"basePrice":10.00,"appliedMultipliers":[{"type":"Weekend","multiplier":1.10,"condition":"weekend=Saturday,Sunday"}],"finalPrice":11.00}` |

### Asientos

| Método y ruta | Ejemplo de request | Ejemplo de response |
|---|---|---|
| `GET /api/trips/{id}/seats` | — | `200` lista ordenada de 40 asientos, cada uno con `seatNumber` y `status`; por ejemplo `[{"seatNumber":1,"status":"Available"},{"seatNumber":2,"status":"Held"},{"seatNumber":3,"status":"Sold"}]` (la respuesta real contiene los 40) |
| `POST /api/trips/{tripId}/seats/{number}/hold` | `{"userId":"passenger-123","minutes":5}` (`minutes` opcional; predeterminado 5) | `200` desglose de tarifa, por ejemplo `{"basePrice":10.00,"appliedMultipliers":[],"finalPrice":10.00}` |
| `POST /api/trips/{tripId}/seats/{number}/confirm` | `{"userId":"passenger-123"}` | `200` desglose de tarifa; el asiento queda `Sold` |
| `DELETE /api/trips/{tripId}/seats/{number}/hold` | `{"userId":"passenger-123"}` | `200` desglose de tarifa; el asiento vuelve a `Available` |

Un error de dominio se responde, por ejemplo, como `422`:

```json
{
  "status": 422,
  "title": "Regla de negocio violada",
  "detail": "El asiento no está disponible para bloquear."
}
```

Un conflicto concurrente al reservar un asiento responde `409` con ProblemDetails.

## Reglas de negocio

- **Turnos y hora de salida:** `Morning` de 05:00 a 11:59:59; `Afternoon` de 12:00 a 17:59:59; `Night` de 18:00 a 23:59:59. El precio base debe ser mayor que cero.
- **Generación:** crea un viaje por plantilla activa y fecha inclusiva. Repetir la petición no duplica el viaje; cada viaje tiene 40 asientos `Available`.
- **Bloqueos:** el hold dura 5 minutos por defecto (se puede solicitar de 1 a 120). Solo el propietario puede confirmar o liberar. El servicio `SeatHoldCleaner` libera bloqueos vencidos cada 30 segundos.
- **Concurrencia:** `RowVersion` evita que dos solicitudes confirmen un hold sobre el mismo estado del asiento; una colisión devuelve `409`.
- **Tarifa dinámica:** `BasePrice × multiplicadores aplicables`, redondeado a dos decimales y nunca inferior al precio base. Los asientos `Held` y `Sold` cuentan para la ocupación.
- **Ocupación:** más de 70% aplica `×1.15`; más de 90% aplica `×1.30` en lugar del nivel de 70%.
- **Turno y fin de semana:** `Night` aplica `×1.10`; sábado y domingo aplican `×1.10`.
- **Feriados:** aplica `×1.25` cuando la fecha de salida coincide con una fecha ISO `yyyy-MM-dd` en la condición de la regla, por ejemplo `dates=2026-12-25;2027-01-01`. La regla semilla empieza sin fechas configuradas.
- Cada hold, confirmación, liberación y limpieza de bloqueos vencidos recalcula y persiste `CurrentPrice`.
