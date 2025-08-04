#include "SteamDetector.h"
#include <QStandardPaths>
#include <QFileInfo>
#include <QDir>
#include <QTextStream>
#include <QJsonDocument>
#include <QRegularExpression>
#include <QLoggingCategory>

Q_LOGGING_CATEGORY(steamDetector, "mystical.steam")

SteamDetector::SteamDetector(QObject* parent)
    : PlatformDetector(parent)
{
}

QString SteamDetector::platformVersion() const
{
    QString steamPath = getSteamInstallPath();
    if (steamPath.isEmpty()) {
        return QString();
    }
    
    // Try to read Steam version from package/steam_client_win32
    QString versionFile = QDir(steamPath).filePath("package/steam_client_win32");
    QFileInfo versionInfo(versionFile);
    if (versionInfo.exists()) {
        return versionInfo.lastModified().toString("yyyy.MM.dd");
    }
    
    return "Unknown";
}

bool SteamDetector::isAvailable() const
{
    return !getSteamInstallPath().isEmpty();
}

QList<GameInfo> SteamDetector::detectGames()
{
    emit detectionStarted();
    
    QList<GameInfo> games;
    QString steamPath = getSteamInstallPath();
    
    if (steamPath.isEmpty()) {
        emit error("Steam installation not found");
        emit detectionFinished(games);
        return games;
    }
    
    qInfo(steamDetector) << "Steam found at:" << steamPath;
    
    QList<SteamLibraryFolder> libraryFolders = getLibraryFolders();
    if (libraryFolders.isEmpty()) {
        emit error("No Steam library folders found");
        emit detectionFinished(games);
        return games;
    }
    
    int totalLibraries = libraryFolders.size();
    int currentLibrary = 0;
    
    for (const auto& folder : libraryFolders) {
        reportProgress(currentLibrary++, totalLibraries, 
                      QString("Scanning %1").arg(folder.label));
        
        if (!folder.mounted || !directoryExists(folder.path)) {
            qWarning(steamDetector) << "Library folder not accessible:" << folder.path;
            continue;
        }
        
        QList<SteamAppManifest> apps = getInstalledApps(folder.path);
        
        for (const auto& app : apps) {
            if (!app.isInstalled) {
                continue;
            }
            
            GameInfo game(app.name, GamePlatform::Steam);
            game.setGameId(app.appId);
            game.setInstallPath(QDir(folder.path).filePath("steamapps/common/" + app.installDir));
            game.setSizeBytes(app.sizeOnDisk);
            game.setInstallDate(app.lastUpdated);
            game.setInstalled(true);
            
            // Set up launch configuration
            LaunchConfig launchConfig;
            launchConfig.method = LaunchMethod::URI;
            launchConfig.platformUri = createSteamLaunchUri(app.appId);
            
            // Also set direct executable as fallback
            QString executablePath = getGameExecutable(game.installPath(), app.appId);
            if (!executablePath.isEmpty()) {
                launchConfig.executable = executablePath;
                launchConfig.workingDirectory = game.installPath();
            }
            
            game.setLaunchConfig(launchConfig);
            
            // Get playtime data
            game.setPlaytimeMinutes(getPlaytimeMinutes(app.appId));
            game.setLastPlayed(getLastPlayedDate(app.appId));
            
            // Set cover art URL
            game.setCoverArtUrl(QUrl(getCoverArtUrl(game)));
            
            games.append(game);
            emit gameFound(game);
            
            qDebug(steamDetector) << "Found Steam game:" << app.name << "ID:" << app.appId;
        }
    }
    
    qInfo(steamDetector) << "Steam detection complete. Found" << games.size() << "games";
    emit detectionFinished(games);
    return games;
}

bool SteamDetector::canLaunchGame(const GameInfo& game) const
{
    if (game.platform() != GamePlatform::Steam) {
        return false;
    }
    
    // Check if Steam is running or can be started
    QString steamPath = getSteamInstallPath();
    if (steamPath.isEmpty()) {
        return false;
    }
    
    QString steamExe = QDir(steamPath).filePath("steam.exe");
    return fileExists(steamExe);
}

QString SteamDetector::validateInstallation() const
{
    QString steamPath = getSteamInstallPath();
    if (steamPath.isEmpty()) {
        return "Steam not found in registry or standard locations";
    }
    
    QString steamExe = QDir(steamPath).filePath("steam.exe");
    if (!fileExists(steamExe)) {
        return "Steam executable not found: " + steamExe;
    }
    
    QList<SteamLibraryFolder> folders = getLibraryFolders();
    if (folders.isEmpty()) {
        return "No Steam library folders configured";
    }
    
    return QString(); // Valid installation
}

QStringList SteamDetector::getInstallationPaths() const
{
    QStringList paths;
    QString steamPath = getSteamInstallPath();
    if (!steamPath.isEmpty()) {
        paths.append(steamPath);
    }
    return paths;
}

QString SteamDetector::getConfigPath() const
{
    QString steamPath = getSteamInstallPath();
    if (steamPath.isEmpty()) {
        return QString();
    }
    
    return QDir(steamPath).filePath("config");
}

QString SteamDetector::getLibraryPath() const
{
    QString steamPath = getSteamInstallPath();
    if (steamPath.isEmpty()) {
        return QString();
    }
    
    return QDir(steamPath).filePath("steamapps");
}

QString SteamDetector::getCoverArtUrl(const GameInfo& game) const
{
    if (game.gameId().isEmpty()) {
        return QString();
    }
    
    // Steam grid image URL format
    return QString("https://steamcdn-a.akamaihd.net/steam/apps/%1/library_600x900.jpg")
           .arg(game.gameId());
}

// Private methods implementation

QString SteamDetector::getSteamInstallPath() const
{
    if (!m_steamPath.isEmpty()) {
        return m_steamPath;
    }
    
    // Try registry first (most reliable)
    QString regPath = readRegistryValue(
        "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Valve\\Steam",
        "InstallPath"
    );
    
    if (!regPath.isEmpty() && directoryExists(regPath)) {
        m_steamPath = normalizePath(regPath);
        return m_steamPath;
    }
    
    // Try 32-bit registry
    regPath = readRegistryValue(
        "HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam",
        "InstallPath"
    );
    
    if (!regPath.isEmpty() && directoryExists(regPath)) {
        m_steamPath = normalizePath(regPath);
        return m_steamPath;
    }
    
    // Fallback to common installation paths
    QStringList commonPaths = {
        "C:/Program Files (x86)/Steam",
        "C:/Program Files/Steam",
        QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation) + "/Steam"
    };
    
    for (const QString& path : commonPaths) {
        if (directoryExists(path)) {
            QString steamExe = QDir(path).filePath("steam.exe");
            if (fileExists(steamExe)) {
                m_steamPath = normalizePath(path);
                return m_steamPath;
            }
        }
    }
    
    return QString();
}

QList<SteamDetector::SteamLibraryFolder> SteamDetector::getLibraryFolders() const
{
    if (!m_libraryFolders.isEmpty()) {
        return m_libraryFolders;
    }
    
    QString steamPath = getSteamInstallPath();
    if (steamPath.isEmpty()) {
        return m_libraryFolders;
    }
    
    QString libraryFoldersPath = QDir(steamPath).filePath("steamapps/libraryfolders.vdf");
    if (!fileExists(libraryFoldersPath)) {
        qWarning(steamDetector) << "libraryfolders.vdf not found:" << libraryFoldersPath;
        return m_libraryFolders;
    }
    
    QJsonObject libraryData = parseVDF(libraryFoldersPath);
    if (libraryData.isEmpty()) {
        qWarning(steamDetector) << "Failed to parse libraryfolders.vdf";
        return m_libraryFolders;
    }
    
    // Parse library folders from VDF
    for (auto it = libraryData.begin(); it != libraryData.end(); ++it) {
        if (it.key().contains(QRegularExpression("^\\d+$"))) { // Numeric keys
            QJsonObject folderObj = it.value().toObject();
            SteamLibraryFolder folder;
            folder.path = normalizePath(folderObj["path"].toString());
            folder.label = folderObj["label"].toString();
            folder.mounted = folderObj["mounted"].toString() == "1";
            
            if (!folder.path.isEmpty()) {
                m_libraryFolders.append(folder);
            }
        }
    }
    
    // Always include default Steam library
    QString defaultLibrary = QDir(steamPath).filePath("steamapps");
    bool hasDefault = false;
    for (const auto& folder : m_libraryFolders) {
        if (folder.path == defaultLibrary) {
            hasDefault = true;
            break;
        }
    }
    
    if (!hasDefault && directoryExists(defaultLibrary)) {
        SteamLibraryFolder defaultFolder;
        defaultFolder.path = defaultLibrary;
        defaultFolder.label = "Steam";
        defaultFolder.mounted = true;
        m_libraryFolders.prepend(defaultFolder);
    }
    
    return m_libraryFolders;
}

QList<SteamDetector::SteamAppManifest> SteamDetector::getInstalledApps(const QString& libraryPath) const
{
    QList<SteamAppManifest> apps;
    
    QDir steamappsDir(libraryPath);
    if (!steamappsDir.exists()) {
        return apps;
    }
    
    // Look for appmanifest_*.acf files
    QStringList manifestFiles = steamappsDir.entryList(
        QStringList("appmanifest_*.acf"),
        QDir::Files
    );
    
    for (const QString& manifestFile : manifestFiles) {
        QString manifestPath = steamappsDir.filePath(manifestFile);
        SteamAppManifest manifest = parseAppManifest(manifestPath);
        
        if (!manifest.appId.isEmpty() && !manifest.name.isEmpty()) {
            apps.append(manifest);
        }
    }
    
    return apps;
}

// Simplified VDF parser implementation
QJsonObject SteamDetector::parseVDF(const QString& filePath) const
{
    QFile file(filePath);
    if (!file.open(QIODevice::ReadOnly | QIODevice::Text)) {
        return QJsonObject();
    }
    
    QTextStream stream(&file);
    stream.setEncoding(QStringConverter::Utf8);
    QString content = stream.readAll();
    
    return parseVDFContent(content);
}

QJsonObject SteamDetector::parseVDFContent(const QString& content) const
{
    int pos = 0;
    return parseVDFObject(content, pos);
}

// Additional implementation methods would continue here...
// For brevity, I'll include key methods that show the pattern

QString SteamDetector::createSteamLaunchUri(const QString& appId) const
{
    return QString("steam://rungameid/%1").arg(appId);
}

QString SteamDetector::getGameExecutable(const QString& installPath, const QString& appId) const
{
    Q_UNUSED(appId) // Could be used for app-specific logic
    
    if (!directoryExists(installPath)) {
        return QString();
    }
    
    // Common executable patterns
    QStringList executablePatterns = {
        "*.exe",
        "bin/*.exe",
        "Binaries/*.exe"
    };
    
    QStringList executables = findExecutables(installPath, executablePatterns);
    
    if (!executables.isEmpty()) {
        // Prefer executables that don't contain "uninstall", "setup", etc.
        for (const QString& exe : executables) {
            QString baseName = QFileInfo(exe).baseName().toLower();
            if (!baseName.contains("uninstall") && 
                !baseName.contains("setup") && 
                !baseName.contains("redist")) {
                return exe;
            }
        }
        
        // Fallback to first executable
        return executables.first();
    }
    
    return QString();
}