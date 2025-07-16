# 🖼️ Screentone Processor – Web API + GUI

Una aplicación de procesamiento de imágenes que permite aplicar **texturas tipo screentone** sobre regiones segmentadas por color, con dos modos de uso: una interfaz web basada en FastAPI y una GUI local opcional con Tkinter.

---

## 🚀 Aplicación Web (FastAPI)

Permite aplicar screentones e inspeccionar colores a través de una interfaz web disponible en:

```
http://localhost:8000/static/index.html
```

> ⚠️ Requiere el uso de una `api_key` (por ejemplo: `123`) para habilitar las funciones.

### ✅ Características principales

- API REST desarrollada con **FastAPI**
- Detección de regiones por color (espacio HSV)
- Aplicación dinámica de patrones (rayas, puntos, grillas)
- Generación de imágenes procesadas
- Interfaz web estática para pruebas visuales

---

## ▶️ Instrucciones de Uso

### 1. Instalar dependencias

```bash
pip install -r requirements.txt
```

### 2. Iniciar el servidor

```bash
uvicorn app.main:app --reload
```

### 3. Acceder a la interfaz

Abrir en navegador:

```
http://localhost:8000/static/index.html
```

Subir una imagen, elegir acción, y procesar.

---

## 📁 Estructura del Proyecto

```
TPI-LAB-3/
├── app/
│   ├── api/v1/
│   │   ├── color_inspector_api.py      # Endpoint de inspección de color
│   │   └── screentone_api.py           # Endpoint de screentone
│   ├── core/
│   │   ├── exceptions.py               # Manejo de errores personalizados
│   │   └── security.py                 # Validación de API key
│   ├── services/
│   │   ├── color_inspector.py         # Lógica de inspección de color
│   │   ├── color_ranges.py            # Rangos HSV para segmentación
│   │   ├── patterns.py                # Generación de texturas screentone
│   │   └── screentone.py              # Procesamiento principal
│   ├── config.py                      # Configuración global
│   └── main.py                        # Punto de entrada de FastAPI
│
├── static/
│   └── index.html                     # Interfaz web
├── uploads/                           # Carpeta para imágenes subidas
├── outputs/                           # Carpeta de resultados generados
├── gui_tkinter/                       # GUI opcional en Tkinter (ver más abajo)
├── requirements.txt
└── README.md
```

---

## 🧪 Endpoints Principales

- `POST /api/v1/screentone/apply`  
  Procesa una imagen cargada y aplica screentone según colores HSV.

- `POST /api/v1/color-inspector/check`  
  Devuelve el nombre del color más cercano para un píxel seleccionado.

---

## 🖥️ GUI Local Adicional (Tkinter)

Además de la versión web, se incluye una **interfaz de escritorio opcional** para inspección y prueba rápida.

### Funcionalidades:

- Abrir imágenes locales
- Aplicar screentone
- Inspeccionar colores
- Interfaz gráfica sencilla con menús

### Ejecutar GUI:

```bash
python -m gui_tkinter.main
```

---

## 📦 Dependencias

Listado completo (`requirements.txt`):

```
fastapi==0.110.2
matplotlib==3.10.1
numpy==2.2.5
opencv-python==4.11.0.86
pillow==11.2.1
webcolors==24.11.1
uvicorn==0.34.2
python-multipart==0.0.20
pydantic_settings==2.0.0
```

---

## 👨‍💻 Autor

Trabajo Práctico Final – Procesamiento de Imágenes  
IFTS N 18
Autores: 

Agustina Ferrer Deheza
Cristian Gimenez
Ignat Krukovskiy
Jazmín Pineda Chipatecua
Juan Esteban Gordon

---