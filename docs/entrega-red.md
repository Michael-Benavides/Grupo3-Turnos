# Entrega para consumo en red local

## 1️⃣ Levantar la API en la red
```powershell
# Desde la raíz del proyecto
cd src/Trips.Api
# Ejecutar con el perfil "network" (escucha en 0.0.0.0:5003)
dotnet run --launch-profile network```

Esto hará que la API escuche en **todos** los interfaces de red de la máquina en el puerto **5003**.

## 2️⃣ Configuración del firewall (Windows)
Permitir tráfico entrante al puerto 5003:
```powershell
# Ejecutar como Administrador
netsh advfirewall firewall add rule name="Trips API" dir=in action=allow protocol=TCP localport=5003```

## 3️⃣ URLs a compartir con los demás equipos
- **Swagger UI** (documentación interactiva): `http://<IP_DE_TU_MAQUINA>:5003/swagger`
- **Endpoint de viajes**: `http://<IP_DE_TU_MAQUINA>:5003/api/v1/trips`
- **Endpoint de reset (solo Development)**: `http://<IP_DE_TU_MAQUINA>:5003/api/v1/dev/reset`

Reemplaza `<IP_DE_TU_MAQUINA>` por la dirección IP que tu equipo tenga en la red local (puedes verla con `ipconfig`).

## 4️⃣ Notas
- En **Desarrollo** CORS está configurado como `AllowAnyOrigin`, `AllowAnyMethod`, `AllowAnyHeader`.
- En **Otros entornos** se usan los orígenes declarados en `appsettings.json` bajo `Cors:AllowedOrigins`.
- No se usa `UseHttpsRedirection` en desarrollo para permitir llamadas HTTP simples desde otros equipos.
