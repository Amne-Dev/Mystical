
// EpicDetector.cpp
#include "EpicDetector.h"
#include <QStandardPaths>
#include <QFileInfo>
#include <QDir>
#include <QJsonDocument>
#include <QLoggingCategory>

Q_LOGGING_CATEGORY(epicDetector, "mystical.epic")

EpicDetector::EpicDetector(QObject* parent)
    : PlatformDetector(parent)
{
}

QString EpicDetector::platformVersion() const
{
    QString launcherPath = getEpicLauncherPath();
    if (launcherPath.isEmpty()) {
        return QString();
    }
    
    QFileInfo launcherInfo(launcherPath);
    if (launcherInfo.exists()) {
        return launcherInfo.lastModified().toString("yyyy.MM.dd");
    }
    
    return "Unknown";
}

bool EpicDetector::isAvailable() const
{
    return !getEpicLauncherPath().isEmpty();
}

QList<GameInfo> EpicDetector::detectGames()
{
    emit detectionStarted();
    
    QList<GameInfo> games;
    QString epicPath = getEpicInstallPath();
    
    if (epicPath.isEmpty()) {
        emit error("Epic Games Launcher not found");
        emit detectionFinished(games);
        return games;
    }
    
    qInfo(epicDetector) << "Epic Games found at:" << epicPath;
    
    QList<EpicGameManifest> manifests = getInstalledGames();
    if (manifests.isEmpty()) {
        qInfo(epicDetector) << "No Epic Games installations found";
        emit detectionFinished(games);
        return games;
    }
    
    int totalGames = manifests.size();
    int currentGame = 0;
    
    for (const auto& manifest : manifests) {
        reportProgress(currentGame++, totalGames, manifest.displayName);
        
        if (!directoryExists(manifest.installLocation)) {
            qWarning(epicDetector) << "Game install location not found:" << manifest.installLocation;
            continue;
        }
        
        GameInfo game(manifest.displayName, GamePlatform::EpicGames);
        game.setGameId(manifest.appName);
        game.setInstallPath(manifest.installLocation);
        game.setSizeBytes(manifest.installSize);
        game.setInstalled(true);
        
        // Set up launch configuration
        LaunchConfig launchConfig;
        
        // Prefer Epic launcher for better integration
        launchConfig.method = LaunchMethod::URI;
        launchConfig.platformUri = createEpicLaunchUri(manifest.appName);
        
        // Set executable as fallback
        if (!manifest.launchExecutable.isEmpty()) {
            QString executablePath = QDir(manifest.installLocation).filePath(manifest.launchExecutable);
            if (fileExists(executablePath)) {
                launchConfig.executable = executablePath;
                launchConfig.workingDirectory = manifest.installLocation;
            }
        }
        
        game.setLaunchConfig(launchConfig);
        
        // Set cover art URL
        game.setCoverArtUrl(QUrl(getCoverArtUrl(game)));
        
        games.append(game);
        emit gameFound(game);
        
        qDebug(epicDetector) << "Found Epic game:" << manifest.displayName << "ID:" << manifest.appName;
    }
    
    qInfo(epicDetector) << "Epic Games detection complete. Found" << games.size() << "games";
    emit detectionFinished(games);
    return games;
}

bool EpicDetector::canLaunchGame(const GameInfo& game) const
{
    if (game.platform() != GamePlatform::EpicGames) {
        return false;
    }
    
    // Check if Epic launcher is available
    return !getEpicLauncherPath().isEmpty();
}

QString EpicDetector::validateInstallation() const
{
    QString launcherPath = getEpicLauncherPath();
    if (launcherPath.isEmpty()) {
        return "Epic Games Launcher not found";
    }
    
    if (!fileExists(launcherPath)) {
        return "Epic Games Launcher executable not found: " + launcherPath;
    }
    
    QString configPath = getConfigPath();
    if (!directoryExists(configPath)) {
        return "Epic Games config directory not found: " + configPath;
    }
    
    return QString(); // Valid installation
}

QStringList EpicDetector::getInstallationPaths() const
{
    QStringList paths;
    QString epicPath = getEpicInstallPath();
    if (!epicPath.isEmpty()) {
        paths.append(epicPath);
    }
    return paths;
}

QString EpicDetector::getConfigPath() const
{
    QString epicPath = getEpicInstallPath();
    if (epicPath.isEmpty()) {
        return QString();
    }
    
    return QDir(epicPath).filePath("UnrealEngineLauncher/LauncherInstalled.dat");
}

QString EpicDetector::getLibraryPath() const
{
    QString localAppData = QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation);
    return QDir(localAppData).filePath("EpicGamesLauncher/Saved/Config/Windows");
}

QString EpicDetector::getCoverArtUrl(const GameInfo& game) const
{
    if (game.gameId().isEmpty()) {
        return QString();
    }
    
    // Epic Games Store cover art URL (may require catalog item ID)
    return QString("https://cdn1.epicgames.com/offer/%1/wide/854x480.jpg")
           .arg(game.gameId());
}

// Private methods implementation

QString EpicDetector::getEpicInstallPath() const
{
    if (!m_epicPath.isEmpty()) {
        return m_epicPath;
    }
    
    // Try registry first
    QString regPath = readRegistryValue(
        "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Epic Games\\EpicGamesLauncher",
        "AppDataPath"
    );
    
    if (!regPath.isEmpty() && directoryExists(regPath)) {
        m_epicPath = normalizePath(regPath);
        return m_epicPath;
    }
    
    // Try user-specific registry
    regPath = readRegistryValue(
        "HKEY_CURRENT_USER\\SOFTWARE\\Epic Games\\EpicGamesLauncher",
        "AppDataPath"
    );
    
    if (!regPath.isEmpty() && directoryExists(regPath)) {
        m_epicPath = normalizePath(regPath);
        return m_epicPath;
    }
    
    // Fallback to common installation paths
    QStringList commonPaths = {
        "C:/Program Files (x86)/Epic Games",
        "C:/Program Files/Epic Games",
        QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation) + "/Epic Games"
    };
    
    for (const QString& path : commonPaths) {
        if (directoryExists(path)) {
            m_epicPath = normalizePath(path);
            return m_epicPath;
        }
    }
    
    return QString();
}

QString EpicDetector::getEpicLauncherPath() const
{
    if (!m_launcherPath.isEmpty()) {
        return m_launcherPath;
    }
    
    QString epicPath = getEpicInstallPath();
    if (epicPath.isEmpty()) {
        return QString();
    }
    
    // Common launcher paths
    QStringList launcherPaths = {
        QDir(epicPath).filePath("Launcher/Portal/Binaries/Win64/EpicGamesLauncher.exe"),
        QDir(epicPath).filePath("Launcher/Portal/Binaries/Win32/EpicGamesLauncher.exe"),
        "C:/Program Files (x86)/Epic Games/Launcher/Portal/Binaries/Win64/EpicGamesLauncher.exe",
        "C:/Program Files/Epic Games/Launcher/Portal/Binaries/Win64/EpicGamesLauncher.exe"
    };
    
    for (const QString& path : launcherPaths) {
        if (fileExists(path)) {
            m_launcherPath = normalizePath(path);
            return m_launcherPath;
        }
    }
    
    return QString();
}

QList<EpicDetector::EpicGameManifest> EpicDetector::getInstalledGames() const
{
    QList<EpicGameManifest> manifests;
    
    // Check ProgramData manifests
    QString programDataPath = QStandardPaths::writableLocation(QStandardPaths::GenericConfigLocation);
    QString manifestsPath = QDir(programDataPath).filePath("Epic/UnrealEngineLauncher/LauncherInstalled.dat");
    
    if (fileExists(manifestsPath)) {
        QFile file(manifestsPath);
        if (file.open(QIODevice::ReadOnly)) {
            QJsonDocument doc = QJsonDocument::fromJson(file.readAll());
            QJsonObject root = doc.object();
            QJsonArray installationList = root["InstallationList"].toArray();
            
            for (const QJsonValue& value : installationList) {
                QJsonObject gameObj = value.toObject();
                EpicGameManifest manifest = parseManifestFile("");
                
                manifest.appName = gameObj["AppName"].toString();
                manifest.displayName = gameObj["DisplayName"].toString();
                manifest.installLocation = gameObj["InstallLocation"].toString();
                manifest.launchExecutable = gameObj["LaunchExecutable"].toString();
                manifest.catalogItemId = gameObj["CatalogItemId"].toString();
                
                if (!manifest.appName.isEmpty() && !manifest.installLocation.isEmpty()) {
                    manifests.append(manifest);
                }
            }
        }
    }
    
    return manifests;
}

EpicDetector::EpicGameManifest EpicDetector::parseManifestFile(const QString& manifestPath) const
{
    EpicGameManifest manifest;
    
    if (manifestPath.isEmpty()) {
        return manifest;
    }
    
    QFile file(manifestPath);
    if (!file.open(QIODevice::ReadOnly)) {
        return manifest;
    }
    
    QJsonDocument doc = QJsonDocument::fromJson(file.readAll());
    if (doc.isNull()) {
        return manifest;
    }
    
    QJsonObject obj = doc.object();
    manifest.appName = obj["AppName"].toString();
    manifest.catalogItemId = obj["CatalogItemId"].toString();
    manifest.displayName = obj["DisplayName"].toString();
    manifest.installLocation = obj["InstallLocation"].toString();
    manifest.launchExecutable = obj["LaunchExecutable"].toString();
    manifest.launchCommand = obj["LaunchCommand"].toString();
    manifest.canRunOffline = obj["bCanRunOffline"].toBool();
    manifest.requiresOwnedDLC = obj["bRequiresOwnedDLC"].toBool();
    manifest.installSize = obj["InstallSize"].toVariant().toLongLong();
    
    return manifest;
}

QString EpicDetector::createEpicLaunchUri(const QString& appName) const
{
    return QString("com.epicgames.launcher://apps/%1?action=launch&silent=true").arg(appName);
}

QString EpicDetector::getEpicCoverArt(const QString& catalogItemId, const QString& appName) const
{
    if (!catalogItemId.isEmpty()) {
        return QString("https://cdn1.epicgames.com/offer/%1/wide/854x480.jpg").arg(catalogItemId);
    }
    
    if (!appName.isEmpty()) {
        return QString("https://cdn1.epicgames.com/offer/%1/wide/854x480.jpg").arg(appName);
    }
    
    return QString();
}