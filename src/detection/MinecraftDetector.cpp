#include "MinecraftDetector.h"
#include "../utils/FileUtils.h"
#include "../utils/RegistryUtils.h"
#include <QDir>
#include <QStandardPaths>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>
#include <QDebug>
#include <QLoggingCategory>
#include <QFileInfo>
#include <QDirIterator>

Q_LOGGING_CATEGORY(minecraftDetector, "mystical.minecraft")

MinecraftDetector::MinecraftDetector(QObject *parent)
    : PlatformDetector(parent)
{
}

QString MinecraftDetector::platformVersion() const
{
    QString launcherPath = getMinecraftLauncherPath();
    if (launcherPath.isEmpty()) {
        return QString();
    }
    
    QFileInfo launcherInfo(launcherPath);
    if (launcherInfo.exists()) {
        return launcherInfo.lastModified().toString("yyyy.MM.dd");
    }
    
    return "Unknown";
}

bool MinecraftDetector::isAvailable() const
{
    // Check for Minecraft installation directory
    QString minecraftPath = getMinecraftPath();
    if (!minecraftPath.isEmpty() && directoryExists(minecraftPath)) {
        return true;
    }
    
    // Check for Minecraft Launcher
    return !getMinecraftLauncherPath().isEmpty();
}

QList<GameInfo> MinecraftDetector::detectGames()
{
    emit detectionStarted();
    
    QList<GameInfo> games;
    QString minecraftPath = getMinecraftPath();
    
    if (minecraftPath.isEmpty()) {
        emit error("Minecraft installation not found");
        emit detectionFinished(games);
        return games;
    }
    
    qInfo(minecraftDetector) << "Minecraft found at:" << minecraftPath;
    
    QList<MinecraftInstallation> installations = getMinecraftInstallations();
    
    // Add main Minecraft entry
    GameInfo mainGame;
    mainGame.setTitle("Minecraft");
    mainGame.setPlatform(platform()); // Use enum instead of string
    mainGame.setGameId("minecraft");
    mainGame.setInstallPath(minecraftPath);
    mainGame.setInstalled(true); // Use setInstalled instead of setIsInstalled
    
    // Set up launcher executable - store in launch config or another field
    QString launcherPath = getMinecraftLauncherPath();
    if (!launcherPath.isEmpty()) {
        // Store launcher path in install path or description for now
        // Since setExecutablePath doesn't exist, we'll handle this differently
        mainGame.setDescription("Launcher: " + launcherPath);
    } else {
        // Fallback to Java if available
        QString javaPath = getMinecraftJavaPath();
        if (!javaPath.isEmpty()) {
            mainGame.setDescription("Java: " + javaPath);
        }
    }
    
    // Calculate approximate size
    QDirIterator dirIt(minecraftPath, QDir::Files, QDirIterator::Subdirectories);
    qint64 totalSize = 0;
    int fileCount = 0;
    
    while (dirIt.hasNext() && fileCount < 1000) {
        dirIt.next();
        totalSize += dirIt.fileInfo().size();
        fileCount++;
    }
    
    mainGame.setSizeBytes(totalSize);
    
    games.append(mainGame);
    emit gameFound(mainGame);
    
    // Add individual installations/profiles if multiple exist
    int totalInstalls = installations.size();
    int currentInstall = 0;
    
    for (const auto& installation : installations) {
        reportProgress(currentInstall++, totalInstalls, installation.name);
        
        if (!installation.isInstalled || installation.id == "minecraft") {
            continue; // Skip main entry we already added
        }
        
        GameInfo profileGame;
        profileGame.setTitle(QString("Minecraft - %1").arg(installation.name));
        profileGame.setPlatform(platform()); // Use enum instead of string
        profileGame.setGameId(QString("minecraft_%1").arg(installation.id));
        profileGame.setInstallPath(installation.gameDir.isEmpty() ? minecraftPath : installation.gameDir);
        profileGame.setDescription(QString("Minecraft %1 (%2)").arg(installation.version, installation.type));
        profileGame.setInstalled(true); // Use setInstalled instead of setIsInstalled
        
        if (!launcherPath.isEmpty()) {
            // Add launcher info to description since setExecutablePath doesn't exist
            QString desc = profileGame.description();
            if (!desc.isEmpty()) desc += " - ";
            desc += "Launcher: " + launcherPath;
            profileGame.setDescription(desc);
        }
        
        profileGame.setSizeBytes(totalSize); // Approximate
        
        games.append(profileGame);
        emit gameFound(profileGame);
        
        qDebug(minecraftDetector) << "Found Minecraft profile:" << installation.name 
                                  << "Version:" << installation.version;
    }
    
    qInfo(minecraftDetector) << "Minecraft detection complete. Found" << games.size() << "entries";
    emit detectionFinished(games);
    return games;
}

bool MinecraftDetector::canLaunchGame(const GameInfo& game) const
{
    if (game.platform() != platform()) { // Compare enum to enum
        return false;
    }
    
    // Check if we have a launcher or Java installation
    QString launcherPath = getMinecraftLauncherPath();
    if (!launcherPath.isEmpty()) {
        return fileExists(launcherPath);
    }
    
    QString javaPath = getMinecraftJavaPath();
    return !javaPath.isEmpty() && fileExists(javaPath);
}

QString MinecraftDetector::validateInstallation() const
{
    QString minecraftPath = getMinecraftPath();
    if (minecraftPath.isEmpty()) {
        return "Minecraft installation directory not found";
    }
    
    if (!directoryExists(minecraftPath)) {
        return "Minecraft directory does not exist: " + minecraftPath;
    }
    
    QString launcherPath = getMinecraftLauncherPath();
    if (launcherPath.isEmpty()) {
        QString javaPath = getMinecraftJavaPath();
        if (javaPath.isEmpty()) {
            return "Neither Minecraft Launcher nor Java installation found";
        }
    }
    
    return QString(); // Valid installation
}

QStringList MinecraftDetector::getInstallationPaths() const
{
    QStringList paths;
    QString minecraftPath = getMinecraftPath();
    if (!minecraftPath.isEmpty()) {
        paths.append(minecraftPath);
    }
    return paths;
}

QString MinecraftDetector::getConfigPath() const
{
    QString minecraftPath = getMinecraftPath();
    if (minecraftPath.isEmpty()) {
        return QString();
    }
    
    return QDir(minecraftPath).filePath("launcher_profiles.json");
}

QString MinecraftDetector::getLibraryPath() const
{
    QString minecraftPath = getMinecraftPath();
    if (minecraftPath.isEmpty()) {
        return QString();
    }
    
    return QDir(minecraftPath).filePath("versions");
}

// Private methods implementation

QString MinecraftDetector::getMinecraftPath() const
{
    if (!m_minecraftPath.isEmpty()) {
        return m_minecraftPath;
    }
    
    // Standard Minecraft directory
    QString appData = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation);
    QString minecraftDir = QDir(appData).filePath("../Roaming/.minecraft");
    
    if (directoryExists(minecraftDir)) {
        m_minecraftPath = QDir(minecraftDir).absolutePath();
        return m_minecraftPath;
    }
    
    // Alternative locations
    QStringList alternativePaths = {
        QStandardPaths::writableLocation(QStandardPaths::HomeLocation) + "/.minecraft",
        QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation) + "/minecraft",
        "C:/Users/" + qgetenv("USERNAME") + "/AppData/Roaming/.minecraft"
    };
    
    for (const QString& path : alternativePaths) {
        if (directoryExists(path)) {
            m_minecraftPath = QDir(path).absolutePath();
            return m_minecraftPath;
        }
    }
    
    return QString();
}

QString MinecraftDetector::getMinecraftLauncherPath() const
{
    if (!m_launcherPath.isEmpty()) {
        return m_launcherPath;
    }
    
    // Try Windows Store version first
    QStringList launcherPaths = {
        // Microsoft Store version
        QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation) + "/../Local/Packages/Microsoft.4297127D64EC6_8wekyb3d8bbwe/LocalCache/Local/game/Minecraft Launcher.exe",
        // Traditional installer version
        "C:/Program Files (x86)/Minecraft Launcher/MinecraftLauncher.exe",
        "C:/Program Files/Minecraft Launcher/MinecraftLauncher.exe",
        // Legacy launcher
        "C:/Program Files (x86)/Minecraft/MinecraftLauncher.exe"
    };
    
    for (const QString& path : launcherPaths) {
        if (fileExists(path)) {
            m_launcherPath = normalizePath(path);
            return m_launcherPath;
        }
    }
    
    return QString();
}

QString MinecraftDetector::getMinecraftJavaPath() const
{
    if (!m_javaPath.isEmpty()) {
        return m_javaPath;
    }
    
    // Try bundled Java first
    QString minecraftPath = getMinecraftPath();
    if (!minecraftPath.isEmpty()) {
        QString bundledJava = QDir(minecraftPath).filePath("runtime/java-runtime-gamma/windows/java-runtime-gamma/bin/java.exe");
        if (fileExists(bundledJava)) {
            m_javaPath = bundledJava;
            return m_javaPath;
        }
    }
    
    // Try system Java
    QStringList javaPaths = {
        "C:/Program Files/Java/jre1.8.0_301/bin/java.exe",
        "C:/Program Files (x86)/Java/jre1.8.0_301/bin/java.exe",
        "C:/Program Files/Eclipse Adoptium/jdk-17.0.2.8-hotspot/bin/java.exe"
    };
    
    for (const QString& path : javaPaths) {
        if (fileExists(path)) {
            m_javaPath = normalizePath(path);
            return m_javaPath;
        }
    }
    
    // Try PATH
    QString javaFromPath = "java"; // This would need proper PATH resolution
    m_javaPath = javaFromPath;
    
    return m_javaPath;
}

QList<MinecraftDetector::MinecraftInstallation> MinecraftDetector::getMinecraftInstallations() const
{
    QList<MinecraftInstallation> installations;
    
    QString configPath = getConfigPath();
    if (!fileExists(configPath)) {
        qDebug(minecraftDetector) << "Minecraft profiles file not found:" << configPath;
        return installations;
    }
    
    QFile file(configPath);
    if (!file.open(QIODevice::ReadOnly)) {
        qWarning(minecraftDetector) << "Failed to open Minecraft profiles file:" << configPath;
        return installations;
    }
    
    QJsonDocument doc = QJsonDocument::fromJson(file.readAll());
    if (doc.isNull()) {
        qWarning(minecraftDetector) << "Invalid JSON in Minecraft profiles file";
        return installations;
    }
    
    QJsonObject root = doc.object();
    QJsonObject profiles = root["profiles"].toObject();
    
    for (auto it = profiles.begin(); it != profiles.end(); ++it) {
        QString profileId = it.key();
        QJsonObject profile = it.value().toObject();
        
        MinecraftInstallation installation = parseProfile(profile, profileId);
        if (!installation.id.isEmpty()) {
            installations.append(installation);
        }
    }
    
    return installations;
}

MinecraftDetector::MinecraftInstallation MinecraftDetector::parseProfile(const QJsonObject& profile, const QString& profileId) const
{
    MinecraftInstallation installation;
    
    installation.id = profileId;
    installation.name = profile["name"].toString();
    installation.type = profile["type"].toString();
    installation.version = profile["lastVersionId"].toString();
    installation.gameDir = profile["gameDir"].toString();
    installation.javaPath = profile["javaDir"].toString();
    
    // Determine if installation is available
    QString versionsPath = getLibraryPath();
    if (!versionsPath.isEmpty() && !installation.version.isEmpty()) {
        QString versionPath = QDir(versionsPath).filePath(installation.version);
        installation.isInstalled = directoryExists(versionPath);
    }
    
    // Set default game directory if not specified
    if (installation.gameDir.isEmpty()) {
        installation.gameDir = getMinecraftPath();
    }
    
    return installation;
}

QString MinecraftDetector::getLatestVersion() const
{
    QString versionsPath = getLibraryPath();
    if (versionsPath.isEmpty()) {
        return QString();
    }
    
    QDir versionsDir(versionsPath);
    QStringList versions = versionsDir.entryList(QDir::Dirs | QDir::NoDotAndDotDot);
    
    // Simple heuristic: return the "latest" release if it exists
    if (versions.contains("latest-release")) {
        return "latest-release";
    }
    
    // Otherwise return the first version (could be improved with proper version sorting)
    return versions.isEmpty() ? QString() : versions.first();
}

QStringList MinecraftDetector::getInstalledVersions() const
{
    QString versionsPath = getLibraryPath();
    if (versionsPath.isEmpty()) {
        return QStringList();
    }
    
    QDir versionsDir(versionsPath);
    return versionsDir.entryList(QDir::Dirs | QDir::NoDotAndDotDot);
}

QString MinecraftDetector::createMinecraftLaunchCommand(const MinecraftInstallation& installation) const
{
    // This is a very simplified version - real Minecraft launching requires
    // parsing version JSON files, resolving libraries, etc.
    QString gameDir = installation.gameDir.isEmpty() ? getMinecraftPath() : installation.gameDir;
    QString version = installation.version.isEmpty() ? getLatestVersion() : installation.version;
    
    QStringList args;
    args << "-Xmx2G" << "-Xms1G";
    args << QString("-Djava.library.path=%1/versions/%2/natives").arg(gameDir, version);
    args << "-cp" << QString("%1/versions/%2/%2.jar").arg(gameDir, version);
    args << "net.minecraft.client.main.Main";
    args << "--username" << "Player";
    args << "--version" << version;
    args << "--gameDir" << gameDir;
    args << "--assetsDir" << QString("%1/assets").arg(gameDir);
    
    return args.join(" ");
}

