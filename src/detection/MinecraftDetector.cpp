#include "MinecraftDetector.h"
#include "../utils/FileUtils.h"
#include "../utils/RegistryUtils.h"
#include <QDir>
#include <QStandardPaths>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>
#include <QDebug>

MinecraftDetector::MinecraftDetector(QObject *parent)
    : PlatformDetector(parent)
{
}

MinecraftDetector::~MinecraftDetector() = default;

QString MinecraftDetector::platformName() const
{
    return "Minecraft";
}

QString MinecraftDetector::platformIcon() const
{
    return "qrc:/resources/icons/minecraft-icon.png";
}

bool MinecraftDetector::isInstalled() const
{
    return !getMinecraftInstallPath().isEmpty();
}

QList<GameInfo> MinecraftDetector::detectGames()
{
    QList<GameInfo> games;
    
    // Detect Minecraft Java Edition
    auto javaEdition = detectMinecraftJava();
    if (javaEdition.isValid()) {
        games.append(javaEdition);
    }
    
    // Detect Minecraft Bedrock Edition (Windows 10)
    auto bedrockEdition = detectMinecraftBedrock();
    if (bedrockEdition.isValid()) {
        games.append(bedrockEdition);
    }
    
    // Detect Minecraft Dungeons
    auto dungeons = detectMinecraftDungeons();
    if (dungeons.isValid()) {
        games.append(dungeons);
    }
    
    return games;
}

QString MinecraftDetector::getMinecraftInstallPath() const
{
    // Check for Minecraft Launcher installation
    QStringList possiblePaths = {
        QStandardPaths::writableLocation(QStandardPaths::AppDataLocation) + "/.minecraft",
        QDir::homePath() + "/.minecraft",
        "C:/Users/" + qgetenv("USERNAME") + "/AppData/Roaming/.minecraft"
    };
    
    for (const QString &path : possiblePaths) {
        if (QDir(path).exists()) {
            return path;
        }
    }
    
    // Check registry for Microsoft Store version
#ifdef Q_OS_WIN
    QString registryPath = RegistryUtils::readRegistryValue(
        HKEY_LOCAL_MACHINE,
        "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft.MinecraftUWP_8wekyb3d8bbwe",
        "InstallLocation"
    );
    
    if (!registryPath.isEmpty() && QDir(registryPath).exists()) {
        return registryPath;
    }
#endif
    
    return QString();
}

GameInfo MinecraftDetector::detectMinecraftJava()
{
    QString minecraftPath = getMinecraftInstallPath();
    if (minecraftPath.isEmpty()) {
        return GameInfo();
    }
    
    GameInfo game;
    game.setTitle("Minecraft: Java Edition");
    game.setPlatform(platformName());
    game.setInstallPath(minecraftPath);
    game.setExecutablePath(findMinecraftLauncher());
    game.setIsInstalled(!game.executablePath().isEmpty());
    
    // Try to get version information
    QString versionInfo = getMinecraftVersion(minecraftPath);
    if (!versionInfo.isEmpty()) {
        game.setVersion(versionInfo);
    }
    
    // Set cover art path
    game.setCoverArt("qrc:/resources/images/minecraft-java-cover.png");
    
    // Get playtime if possible (from launcher profiles)
    int playtime = getMinecraftPlaytime(minecraftPath);
    if (playtime > 0) {
        game.setPlaytimeMinutes(playtime);
    }
    
    // Set launch arguments
    game.setLaunchArguments(QStringList());
    
    return game;
}

GameInfo MinecraftDetector::detectMinecraftBedrock()
{
#ifdef Q_OS_WIN
    // Check for Microsoft Store version
    QString appxPath = QStandardPaths::writableLocation(QStandardPaths::AppLocalDataLocation) 
                      + "/../Packages/Microsoft.MinecraftUWP_8wekyb3d8bbwe";
    
    if (!QDir(appxPath).exists()) {
        return GameInfo();
    }
    
    GameInfo game;
    game.setTitle("Minecraft: Bedrock Edition");
    game.setPlatform(platformName());
    game.setInstallPath(appxPath);
    game.setExecutablePath("minecraft://"); // Protocol handler
    game.setIsInstalled(true);
    game.setCoverArt("qrc:/resources/images/minecraft-bedrock-cover.png");
    
    return game;
#else
    return GameInfo();
#endif
}

GameInfo MinecraftDetector::detectMinecraftDungeons()
{
#ifdef Q_OS_WIN
    // Check registry for Minecraft Dungeons
    QString installPath = RegistryUtils::readRegistryValue(
        HKEY_LOCAL_MACHINE,
        "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Microsoft.Lovika_8wekyb3d8bbwe",
        "InstallLocation"
    );
    
    if (installPath.isEmpty() || !QDir(installPath).exists()) {
        return GameInfo();
    }
    
    GameInfo game;
    game.setTitle("Minecraft Dungeons");
    game.setPlatform(platformName());
    game.setInstallPath(installPath);
    game.setExecutablePath("minecraft-dungeons://"); // Protocol handler
    game.setIsInstalled(true);
    game.setCoverArt("qrc:/resources/images/minecraft-dungeons-cover.png");
    
    return game;
#else
    return GameInfo();
#endif
}

QString MinecraftDetector::findMinecraftLauncher() const
{
    QStringList possibleLaunchers = {
        // Official Minecraft Launcher
        "C:/Program Files (x86)/Minecraft Launcher/MinecraftLauncher.exe",
        "C:/Program Files/Minecraft Launcher/MinecraftLauncher.exe",
        
        // Alternative locations
        QStandardPaths::writableLocation(QStandardPaths::AppDataLocation) + "/MinecraftLauncher.exe",
        
        // Legacy launcher
        "C:/Program Files (x86)/Minecraft/MinecraftLauncher.exe"
    };
    
    for (const QString &launcher : possibleLaunchers) {
        if (QFile::exists(launcher)) {
            return launcher;
        }
    }
    
    // Check if launcher is in PATH
    QString pathLauncher = FileUtils::findExecutableInPath("MinecraftLauncher.exe");
    if (!pathLauncher.isEmpty()) {
        return pathLauncher;
    }
    
    return QString();
}

QString MinecraftDetector::getMinecraftVersion(const QString &minecraftPath) const
{
    // Try to read version from launcher_profiles.json
    QString profilesPath = minecraftPath + "/launcher_profiles.json";
    if (!QFile::exists(profilesPath)) {
        return QString();
    }
    
    QFile file(profilesPath);
    if (!file.open(QIODevice::ReadOnly)) {
        return QString();
    }
    
    QJsonParseError error;
    QJsonDocument doc = QJsonDocument::fromJson(file.readAll(), &error);
    file.close();
    
    if (error.error != QJsonParseError::NoError) {
        return QString();
    }
    
    QJsonObject root = doc.object();
    QJsonObject profiles = root["profiles"].toObject();
    
    // Get the latest release version from profiles
    for (auto it = profiles.begin(); it != profiles.end(); ++it) {
        QJsonObject profile = it.value().toObject();
        QString lastVersionId = profile["lastVersionId"].toString();
        
        if (!lastVersionId.isEmpty() && lastVersionId != "latest-release") {
            return lastVersionId;
        }
    }
    
    return "Unknown";
}

int MinecraftDetector::getMinecraftPlaytime(const QString &minecraftPath) const
{
    // Minecraft doesn't store playtime directly, but we could estimate from logs
    // For now, return 0 as playtime tracking would require more complex analysis
    Q_UNUSED(minecraftPath)
    return 0;
}

bool MinecraftDetector::canLaunchGame(const GameInfo &game) const
{
    if (game.platform() != platformName()) {
        return false;
    }
    
    // For Java Edition, check if launcher exists
    if (game.title().contains("Java Edition")) {
        return !findMinecraftLauncher().isEmpty();
    }
    
    // For Bedrock/Dungeons, assume protocol handlers work
    return true;
}

bool MinecraftDetector::launchGame(const GameInfo &game)
{
    if (!canLaunchGame(game)) {
        return false;
    }
    
    QString executable = game.executablePath();
    QStringList arguments = game.launchArguments();
    
    // For Java Edition, launch through the official launcher
    if (game.title().contains("Java Edition")) {
        QString launcher = findMinecraftLauncher();
        if (launcher.isEmpty()) {
            return false;
        }
        
        return FileUtils::launchProcess(launcher, arguments);
    }
    
    // For Bedrock/Dungeons, use protocol handlers
    if (executable.startsWith("minecraft://") || executable.startsWith("minecraft-dungeons://")) {
        return FileUtils::openUrl(executable);
    }
    
    return false;
}

QStringList MinecraftDetector::getSupportedFileExtensions() const
{
    return QStringList() << ".mcworld" << ".mcpack" << ".mcaddon" << ".mctemplate";
}

QString MinecraftDetector::getGameDataPath(const GameInfo &game) const
{
    if (game.title().contains("Java Edition")) {
        return getMinecraftInstallPath();
    }
    
    // For Bedrock Edition
    if (game.title().contains("Bedrock Edition")) {
#ifdef Q_OS_WIN
        return QStandardPaths::writableLocation(QStandardPaths::AppLocalDataLocation) 
               + "/../Packages/Microsoft.MinecraftUWP_8wekyb3d8bbwe/LocalState/games/com.mojang";
#endif
    }
    
    return QString();
}

QStringList MinecraftDetector::getInstalledMods(const GameInfo &game) const
{
    QStringList mods;
    
    if (game.title().contains("Java Edition")) {
        QString modsPath = getMinecraftInstallPath() + "/mods";
        QDir modsDir(modsPath);
        
        if (modsDir.exists()) {
            QStringList modFiles = modsDir.entryList(QStringList() << "*.jar", QDir::Files);
            for (const QString &modFile : modFiles) {
                // Remove .jar extension and add to list
                QString modName = modFile;
                modName.chop(4);
                mods.append(modName);
            }
        }
    }
    
    return mods;
}

bool MinecraftDetector::refreshGameInfo(GameInfo &game) const
{
    if (game.platform() != platformName()) {
        return false;
    }
    
    // Update installation status
    game.setIsInstalled(canLaunchGame(game));
    
    // Update version if possible
    if (game.title().contains("Java Edition")) {
        QString version = getMinecraftVersion(getMinecraftInstallPath());
        if (!version.isEmpty()) {
            game.setVersion(version);
        }
    }
    
    return true;
}