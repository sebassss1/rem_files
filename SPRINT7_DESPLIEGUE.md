# SPRINT 7 - DESPLIEGUE Y CI/CD

**OBJETIVO**: Despliegue del servidor e implantación de un sistema de CI / CD.
**RESULTADOS DE APRENDIZAJE**: **AADD-RA4**: Desarrolla aplicaciones que gestionan la información almacenada en bases de datos objeto relacionales y orientadas a objetos valorando sus características y utilizando los mecanismos de acceso incorporados.

---

## 1. Integración y Entrega Continua (CI / CD)

El proyecto cuenta con un sistema de CI/CD automatizado implementado a través de **GitHub Actions**. El objetivo de este sistema es garantizar que el código se compile, se prube y se distribuya automáticamente con cada nuevo desarrollo.

### Flujo de Trabajo (Workflow)

El archivo `.github/workflows/build-docker-server.yml` define las siguientes etapas:

1. **Test-Server**: 
   - Se clona el repositorio.
   - Instala y configura el entorno `.NET 9.0`.
   - Restaura (restore) y compila (build) el código del servidor (`BasisServer.sln`).
   - Ejecuta las pruebas unitarias (test). Si una prueba falla, el flujo se detiene y avisa del error, manteniendo el despliegue estable.
   
2. **Server-Build-And-Push** (Si los tests son exitosos):
   - Inicia sesión en **GitHub Container Registry (GHCR)**.
   - Extrae los metadatos y crea los tags (etiquetas de versión) a la imagen basándose en si se ha pusheado a las ramas `developer` o `long-term-support`.
   - Construye (build) la imagen oficial de Docker del servidor (`Dockerfile`).
   - Envía (push) la imagen en GHCR, de forma que el servidor queda empaquetado y listo para descargar (pull) en cualquier servidor VPS en la nube que cumpla los requisitos.

---

## 2. Docker y Despliegues

Para el despliegue físico del servidor, el proyecto emplea **Docker** y **Docker Compose**, simplificando considerablemente el proceso de puesta en producción y garantizando que el entorno de desarrollo sea idéntico al de producción.

### Dockerfile (Multi-Stage Build)
Ubicado en `Basis Server/Docker/Dockerfile`. Funciona en dos etapas para hacer la imagen más liviana y segura:
1. **Etapa "Build"**: Utiliza el SDK completo de .NET 9.0 para compilar la aplicación en modo `Release` (y descargar todas las dependencias).
2. **Etapa Final**: Usa la minúscula `runtime-bookworm` (exclusiva para ejecutar), copia los binarios de la etapa build, e instala `curl` para los *healthchecks*. 

### Docker Compose
Ubicado en la raíz (`docker-compose.yml`), orquesta los servicios. Facilita los puertos y los directorios persistentes.
Levanta la imagen publicada (`basis-server:latest` desde GHCR) o la compila localmente de forma predeterminada usando:
- **Port mapping**: 
   - UDP 4296 (Tráfico de Juego)
   - TCP 10666 (Health Check para comprobar estado operativo)
   - TCP 1234 (Métricas con Prometheus)
- **Persistencia**: Especifica el área de volumen en la máquina local (`./data:/app/data`) para la base de datos (vital para cumplir el RA4).

---

## 3. AADD-RA4: Base de Datos Orientada a Objetos y Persistencia

El requisito **AADD-RA4** está resuelto mediante la implementación de **LiteDB**, una base de datos NoSQL documental y de grado embebido (similar a MongoDB pero escrita íntegramente de manera local) que nos permite persistir los datos respetando la orientación a objetos y minimizando el procesamiento de conversiones ORM complejas. 

* **Configuración del Acceso a Datos**: En `BasisDatabase.cs` se ha configurado la instancia de `LiteDatabase` para cargar el archivo `basis_data.db`. Una particularidad destacada es el cifrado embebido usando contraseña para cumplir con los estándares de seguridad de servidor, a la par que registrar el `ConcurrentDictionary<string, object>` para LiteDB como persistencia pura JSON/BSON orientado a objeto.
* **Persistencia Segura en Docker**: Al estar el servidor contenerizado, el estado de la aplicación no puede dejarse en la máquina virtual Docker, ya que si Docker se reiniciara o actualizara la imagen, la base de datos se borraría. Es por esto que se ha expuesto el volumen de base de datos dentro del `docker-compose.yml`:
  ```yml
  volumes:
    - ./data:/app/data
  ```
Esto asegura que el archivo `basis_data.db` se cree, se lea o se escriba en el disco duro físico del servidor (`/data`) independientemente del estado de la imagen de Docker, manteniendo toda la información almacenada en base de objetos permanentemente.

---

## Guía de Pruebas: Generación de Capturas de Pantalla

Para tu informe de entrega final de sprint, puedes seguir estos pasos para recopilar evidencias visuales ("capturas").

### A) Muestra del sistema CI/CD en funcionamiento (GitHub)
1. Ve al repositorio de tu proyecto en **GitHub**, en el navegador de internet.
2. Pulsa en la pestaña **"Actions"**.
3. Selecciona la acción **"Docker; Build & Push Server Image"** de las últimas que hayan finalizado correctamente (estará en verde).
4. **📸 Haz una captura del diagrama que valida que los tests pasaron, y el contenedor fue empujado (Push).**

### B) Muestra del Servidor Desplegado en AWS EC2

Para hacer un despliegue real en la nube usando **Amazon Web Services (AWS)** en lugar de tu propio ordenador, seguiremos el estándar de la industria usando una máquina virtual (EC2) y ejecutando nuestra imagen Docker allí.

#### Pasos para desplegar en AWS EC2:

1. **Crear la Instancia (Servidor) en AWS:**
   - Entra en tu consola de AWS y ve al servicio **EC2**.
   - Haz clic en **"Lanzar instancia"** (Launch instance).
   - Ponle un nombre (ej: `Basis-Server`).
   - En "Imágenes de SO", selecciona **Ubuntu Server 24.04 LTS** (la capa gratuita sirve).
   - En "Tipo de instancia", deja `t2.micro` (Capa gratuita).
   - En "Par de claves" (Key pair), crea unas nuevas claves (formato `.pem`) y descárgalas para poder conectarte.
   - En "Configuraciones de red", permite el tráfico SSH (puerto 22), y añade reglas personalizadas para permitir el puerto **4296 UDP** (Juego) y **10666 TCP** (Health Check).
   - Pulsa en **"Lanzar instancia"**.

2. **Conectarse a la Instancia:**
   - Una vez en estado "En ejecución", selecciónala y pulsa **"Conectar"** (arriba a la derecha). 
   - Puedes usar la propia consola web de AWS (EC2 Instance Connect) o conectarte por SSH desde tu terminal usando la clave descargada.

3. **Instalar Docker en Ubuntu (AWS):**
   Dentro del terminal de tu servidor EC2 recién creado, ejecuta estos comandos uno a uno:
   ```bash
   sudo apt update
   sudo apt install docker.io docker-compose -y
   sudo systemctl start docker
   sudo systemctl enable docker
   ```

4. **Clonar tu Repositorio y Arrancar el Servidor:**
   Como tu proyecto ya tiene el `docker-compose.yml` preparado, solo tienes que traerlo al servidor:
   ```bash
   git clone <TU_ENLACE_AL_REPOSITORIO_GITHUB> basis-server
   cd basis-server
   sudo docker-compose up -d --build
   ```
   *(El flag `-d` hace que se quede ejecutando en segundo plano, para que no se apague si cierras la consola).*

5. **Pruebas y Capturas para el Informe (AWS):**
   - Vuelve a la consola de EC2 de AWS y copia la **Dirección IPv4 pública** de tu instancia (ej: `54.123.45.67`).
   - Abre tu navegador y ve a: `http://<TU_IP_PÚBLICA_DE_AWS>:10666/health`
   - **📸 Haz una captura del navegador** respondiendo "Healthy" con la IP de AWS arriba. Esto demuestra el despliegue exitoso en la nube.
   - En la consola de EC2, ejecuta `ls data/` para ver que se ha creado el archivo `basis_data.db`.
   - **📸 Haz una captura del terminal de AWS** mostrando ese archivo para justificar la persistencia y el RA4 en un entorno de producción real.
