using System.ComponentModel;
namespace BedrockLauncher.Core;

/// <summary>Localización ES/EN en caliente. Se enlaza en XAML como {Binding L[clave]}.</summary>
public class Localizer : INotifyPropertyChanged
{
    public static Localizer Instance { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    string _lang = "es";
    public string this[string key] => Table.TryGetValue(key, out var v) ? (_lang == "en" ? v.en : v.es) : key;
    public string Get(string key, params object[] args) { var s = this[key]; return args.Length == 0 ? s : string.Format(s, args); }
    public void SetLanguage(string lang) { _lang = lang == "en" ? "en" : "es"; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]")); }

    static readonly Dictionary<string, (string es, string en)> Table = new()
    {
        ["nav_home"] = ("Inicio", "Home"), ["nav_versions"] = ("Versiones", "Versions"), ["nav_content"] = ("Contenido", "Content"),
        ["nav_backups"] = ("Copias", "Backups"), ["nav_profiles"] = ("Perfiles", "Profiles"), ["nav_settings"] = ("Ajustes", "Settings"),
        ["play"] = ("JUGAR", "PLAY"), ["selected_version"] = ("Versión seleccionada", "Selected version"),
        ["latest"] = ("Última versión oficial", "Latest official version"), ["status"] = ("Estado", "Status"),
        ["installed"] = ("Instalado", "Installed"), ["not_installed"] = ("No instalado", "Not installed"),
        ["quick"] = ("Acceso rápido", "Quick access"), ["q_addons"] = ("Add-ons", "Add-ons"), ["q_textures"] = ("Texturas", "Textures"),
        ["q_skins"] = ("Skins", "Skins"), ["news"] = ("Novedades", "News"), ["search"] = ("Buscar contenido…", "Search content…"),
        ["import"] = ("Importar…", "Import…"), ["refresh"] = ("Actualizar", "Refresh"), ["open_folder"] = ("Abrir carpeta", "Open folder"),
        ["on"] = ("Activado", "Enabled"), ["off"] = ("Desactivado", "Disabled"), ["delete"] = ("Eliminar", "Delete"),
        ["cat_all"] = ("Todo", "All"), ["cat_addons"] = ("Add-ons", "Add-ons"), ["cat_textures"] = ("Texturas", "Textures"),
        ["cat_shaders"] = ("Shaders", "Shaders"), ["cat_skins"] = ("Skins", "Skins"), ["cat_worlds"] = ("Mundos", "Worlds"), ["cat_templates"] = ("Plantillas", "Templates"),
        ["create_backup"] = ("Crear copia", "Create backup"), ["restore"] = ("Restaurar", "Restore"),
        ["install_update"] = ("Instalar / actualizar (Microsoft Store)", "Install / update (Microsoft Store)"),
        ["uninstall"] = ("Desinstalar", "Uninstall"), ["installed_ver"] = ("Versión instalada", "Installed version"),
        ["installed_on"] = ("Instalada el", "Installed on"), ["size"] = ("Datos", "Data"),
        ["official_releases"] = ("Notas de versión oficiales (estable y Preview)", "Official release notes (stable & Preview)"),
        ["open"] = ("Abrir", "Open"), ["profiles_title"] = ("Perfiles", "Profiles"), ["new_profile"] = ("Nuevo perfil", "New profile"),
        ["create"] = ("Crear", "Create"), ["use"] = ("Usar", "Use"), ["active"] = ("ACTIVO", "ACTIVE"),
        ["language"] = ("Idioma", "Language"), ["theme"] = ("Tema", "Theme"), ["dark"] = ("Oscuro", "Dark"), ["light"] = ("Claro", "Light"),
        ["accent"] = ("Color de acento (#RRGGBB)", "Accent color (#RRGGBB)"), ["apply"] = ("Aplicar", "Apply"),
        ["background"] = ("Fondo personalizado", "Custom background"), ["choose"] = ("Elegir…", "Choose…"), ["clear"] = ("Quitar", "Remove"),
        ["backup_folder"] = ("Carpeta de copias (vacío = por defecto)", "Backup folder (empty = default)"), ["max_backups"] = ("Máximo de copias", "Max backups"),
        ["auto_backup"] = ("Copia automática antes de importar/restaurar", "Auto backup before import/restore"),
        ["animations"] = ("Animaciones", "Animations"), ["software_render"] = ("Render por software (menos GPU, requiere reiniciar)", "Software rendering (less GPU, restart required)"),
        ["save"] = ("Guardar ajustes", "Save settings"), ["check_updates"] = ("Buscar actualizaciones del launcher", "Check launcher updates"),
        ["update_url"] = ("URL de releases del launcher (API de GitHub)", "Launcher releases URL (GitHub API)"),
        ["news_url"] = ("URL de novedades oficiales", "Official news URL"), ["storage"] = ("Almacenamiento", "Storage"),
        ["clean_temp"] = ("Limpiar temporales", "Clean temp files"), ["cancel"] = ("Cancelar", "Cancel"), ["download"] = ("Descargar actualización", "Download update"),
        ["backup_for"] = ("Copias de seguridad", "Backups"), ["release"] = ("Estable", "Stable"), ["preview"] = ("Preview", "Preview"),
        ["profile_name"] = ("Nombre del perfil", "Profile name"), ["no_items"] = ("No hay elementos.", "Nothing here."),
        ["msg_not_installed"] = ("Minecraft Bedrock no está instalado. Instálalo (o su prueba gratuita oficial) desde Microsoft Store.", "Minecraft Bedrock is not installed. Install it (or the official free trial) from the Microsoft Store."),
        ["msg_game_running"] = ("Cierra Minecraft antes de continuar.", "Close Minecraft before continuing."),
        ["msg_imported"] = ("Importado: {0}", "Imported: {0}"), ["msg_backup_done"] = ("Copia creada: {0}", "Backup created: {0}"),
        ["msg_restored"] = ("Copia restaurada.", "Backup restored."), ["msg_deleted"] = ("Eliminado.", "Deleted."),
        ["msg_confirm_delete"] = ("¿Eliminar «{0}» definitivamente?", "Delete “{0}” permanently?"),
        ["msg_confirm_restore"] = ("Se restaurará «{0}» sobre tus datos actuales (se hará una copia de seguridad antes). ¿Continuar?", "“{0}” will be restored over your current data (a safety backup is made first). Continue?"),
        ["msg_confirm_uninstall"] = ("Se creará una copia de tus datos y se desinstalará {0}. Tu licencia/cuenta no se ve afectada. ¿Continuar?", "Your data will be backed up and {0} will be uninstalled. Your license/account is not affected. Continue?"),
        ["msg_saved"] = ("Ajustes guardados.", "Settings saved."), ["msg_no_updates"] = ("El launcher está actualizado.", "Launcher is up to date."),
        ["msg_update_avail"] = ("Nueva versión disponible: {0}", "New version available: {0}"), ["msg_cancelled"] = ("Operación cancelada.", "Operation cancelled."),
        ["msg_launching"] = ("Iniciando Minecraft…", "Launching Minecraft…"), ["msg_no_url"] = ("Configura la URL en Ajustes.", "Set the URL in Settings."),
        ["msg_news_fail"] = ("No se pudieron cargar las novedades oficiales.", "Could not load official news."),
        ["msg_cleaned"] = ("Temporales eliminados.", "Temp files removed."), ["msg_profile_exists"] = ("Escribe un nombre para el perfil.", "Enter a profile name."),
        ["msg_downloaded"] = ("Descarga completa. Se abrió la carpeta.", "Download complete. Folder opened."),
        ["busy_import"] = ("Importando…", "Importing…"), ["busy_backup"] = ("Creando copia de seguridad…", "Creating backup…"),
        ["busy_restore"] = ("Restaurando…", "Restoring…"), ["busy_uninstall"] = ("Desinstalando…", "Uninstalling…"), ["busy_download"] = ("Descargando…", "Downloading…"),
    };
}
