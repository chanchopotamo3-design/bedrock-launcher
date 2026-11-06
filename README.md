# Bedrock Ultimate Launcher

Launcher gratuito y de código abierto para **Minecraft Bedrock Edition (Windows 10/11)**: gestiona instalaciones, perfiles, add-ons, texturas, shaders, skins, mundos y copias de seguridad, con interfaz inspirada en Ore UI. C# / .NET 8 / WPF.

## Gratis, pero con el juego legítimo
El launcher no cuesta nada y no pide ninguna cuenta. **No incluye ni descarga el juego, ni evita la verificación de licencia.** Bedrock debe estar instalado desde Microsoft Store. Para probarlo gratis usa la **prueba gratuita oficial** de "Minecraft para Windows" (o Minecraft Preview, que es gratuito si ya tienes el juego). El launcher detecta esa instalación y trabaja encima de ella.

## Cómo generar el .exe y el instalador (Windows 10/11)
1. Instala el **.NET 8 SDK**: https://dotnet.microsoft.com/download
2. (Opcional, para el instalador) instala **Inno Setup 6**: https://jrsoftware.org/isdl.php
3. Abre PowerShell en esta carpeta y ejecuta:
   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   ./build.ps1
   ```
4. Resultados:
   - `dist\app\BedrockUltimateLauncher.exe` (autocontenido, no requiere instalar .NET)
   - `dist\BedrockUltimateLauncher-Setup.exe` (instalador)

Alternativa: abre `src/BedrockLauncher/BedrockLauncher.csproj` en Visual Studio 2022 (carga de trabajo "Desarrollo de escritorio .NET") y pulsa F5.

## Estructura
```
src/BedrockLauncher/
  Core/        modelos, rutas, JSON, log, localización, utilidades
  Services/    InstallationService, ContentService, BackupService, Profile/Settings, News/Update
  ViewModels/  MainViewModel (MVVM)
  Views/       MainWindow.xaml
  UI/          convertidores y gestor de temas
  Themes/      Dark.xaml, Light.xaml, Styles.xaml (botones Ore UI)
tests/         pruebas xunit (importación, activar/desactivar, backup/restore)
installer/     script Inno Setup
docs/MANUAL.md manual de uso
```
Datos del launcher: `%LOCALAPPDATA%\BedrockUltimateLauncher` (ajustes, perfiles, copias, logs).

## Qué hace cada cosa (y sus límites reales)
- **Detección**: consulta `Get-AppxPackage` (Stable: `Microsoft.MinecraftUWP`, Preview: `Microsoft.MinecraftWindowsBeta`). Solo lectura.
- **Jugar**: lanza la app instalada vía `shell:appsFolder`.
- **Instalar/actualizar**: abre la ficha oficial en Microsoft Store. Microsoft **no ofrece API pública** para instalar versiones antiguas arbitrarias, así que el launcher no lo simula. La lista de versiones sale de las **notas de versión oficiales** (feedback.minecraft.net).
- **Desinstalar**: hace copia de seguridad primero (si falla, no desinstala) y ejecuta `Remove-AppxPackage`. La licencia de tu cuenta no cambia.
- **Contenido**: importa `.mcpack`, `.mcaddon`, `.mcworld`, `.mctemplate`, `.zip` (con paquetes anidados), valida `manifest.json` (tolera comentarios y comas finales), detecta `min_engine_version` y dependencias, activa/desactiva (mueve la carpeta fuera de `com.mojang`, reversible), elimina, clasifica en Add-ons / Texturas / Shaders / Skins / Mundos / Plantillas.
- **Copias**: zip de `com.mojang` con progreso y cancelación; restauración con copia de seguridad previa; límite de copias configurable.
- **Actualizaciones del launcher**: consulta GitHub Releases (configura la URL en Ajustes) y descarga solo desde dominios permitidos (github.com, githubusercontent.com, microsoft.com), con progreso y cancelación.

## Aviso importante sobre el estado del código
Este código se escribió sin acceso a Windows ni al SDK de .NET, por lo que **no se ha compilado ni ejecutado todavía**. Es posible que `build.ps1` muestre algún error de compilación menor la primera vez; si ocurre, pega el mensaje y se corrige. Tampoco se verificó el ID de sección de la URL de noticias oficiales: es editable en Ajustes.
