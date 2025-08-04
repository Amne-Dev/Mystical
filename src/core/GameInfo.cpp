#include "GameInfo.h"
#include <QJsonArray>
#include <QFileInfo>
#include <QLocale>

// LaunchConfig implementation
QJsonObject LaunchConfig::toJson() const
{
    QJsonObject obj;
    obj["executable"] = executable;
    obj["arguments"] = QJsonArray::fromStringList(arguments);
    obj["workingDirectory"] = workingDirectory;
    obj["method"] = static_cast<int>(method);
    obj["platformUri"] = platformUri;
    return obj;
}

void LaunchConfig::fromJson(const QJsonObject& json)
{
    executable = json["executable"].toString();
    
    QJsonArray argsArray = json["arguments"].toArray();
    arguments.clear();
    for (const auto& arg : argsArray) {
        arguments.append(arg.toString());
    }
    
    workingDirectory = json["workingDirectory"].toString();
    method = static_cast<LaunchMethod>(json["method"].toInt());
    platformUri = json["platformUri"].toString();
}

// GameInfo implementation
GameInfo::GameInfo()
{
    // Initialize with current timestamp
    m_installDate = QDateTime::currentDateTime();
}

GameInfo::GameInfo(const QString& title, GamePlatform platform)
    : m_title(title), m_platform(platform)
{
    m_installDate = QDateTime::currentDateTime();
}

GameInfo::GameInfo(const GameInfo& other)
    : m_title(other.m_title)
    , m_description(other.m_description)
    , m_platform(other.m_platform)
    , m_launchConfig(other.m_launchConfig)
    , m_gameId(other.m_gameId)
    , m_installPath(other.m_installPath)
    , m_coverArtPath(other.m_coverArtPath)
    , m_coverArtUrl(other.m_coverArtUrl)
    , m_lastPlayed(other.m_lastPlayed)
    , m_playtimeMinutes(other.m_playtimeMinutes)
    , m_installDate(other.m_installDate)
    , m_sizeBytes(other.m_sizeBytes)
    , m_isInstalled(other.m_isInstalled)
    , m_isFavorite(other.m_isFavorite)
    , m_tags(other.m_tags)
{
}

GameInfo& GameInfo::operator=(const GameInfo& other)
{
    if (this != &other) {
        m_title = other.m_title;
        m_description = other.m_description;
        m_platform = other.m_platform;
        m_launchConfig = other.m_launchConfig;
        m_gameId = other.m_gameId;
        m_installPath = other.m_installPath;
        m_coverArtPath = other.m_coverArtPath;
        m_coverArtUrl = other.m_coverArtUrl;
        m_lastPlayed = other.m_lastPlayed;
        m_playtimeMinutes = other.m_playtimeMinutes;
        m_installDate = other.m_installDate;
        m_sizeBytes = other.m_sizeBytes;
        m_isInstalled = other.m_isInstalled;
        m_isFavorite = other.m_isFavorite;
        m_tags = other.m_tags;
    }
    return *this;
}

QString GameInfo::platformString() const
{
    switch (m_platform) {
        case GamePlatform::Steam: return "Steam";
        case GamePlatform::EpicGames: return "Epic Games";
        case GamePlatform::GOG: return "GOG";
        case GamePlatform::EAApp: return "EA App";
        case GamePlatform::Minecraft: return "Minecraft";
        case GamePlatform::Roblox: return "Roblox";
        case GamePlatform::Custom: return "Custom";
        default: return "Unknown";
    }
}

GamePlatform GameInfo::platformFromString(const QString& str)
{
    if (str == "Steam") return GamePlatform::Steam;
    if (str == "Epic Games") return GamePlatform::EpicGames;
    if (str == "GOG") return GamePlatform::GOG;
    if (str == "EA App") return GamePlatform::EAApp;
    if (str == "Minecraft") return GamePlatform::Minecraft;
    if (str == "Roblox") return GamePlatform::Roblox;
    if (str == "Custom") return GamePlatform::Custom;
    return GamePlatform::Unknown;
}

void GameInfo::addTag(const QString& tag)
{
    if (!m_tags.contains(tag)) {
        m_tags.append(tag);
    }
}

void GameInfo::removeTag(const QString& tag)
{
    m_tags.removeAll(tag);
}

bool GameInfo::isValid() const
{
    if (m_title.isEmpty()) {
        return false;
    }
    
    if (m_platform == GamePlatform::Unknown) {
        return false;
    }
    
    // Check if executable exists for direct launch
    if (m_launchConfig.method == LaunchMethod::DirectExecutable) {
        if (m_launchConfig.executable.isEmpty()) {
            return false;
        }
        
        QFileInfo fileInfo(m_launchConfig.executable);
        if (!fileInfo.exists() || !fileInfo.isExecutable()) {
            return false;
        }
    }
    
    return true;
}

QString GameInfo::validationError() const
{
    if (m_title.isEmpty()) {
        return "Game title is required";
    }
    
    if (m_platform == GamePlatform::Unknown) {
        return "Platform must be specified";
    }
    
    if (m_launchConfig.method == LaunchMethod::DirectExecutable) {
        if (m_launchConfig.executable.isEmpty()) {
            return "Executable path is required for direct launch";
        }
        
        QFileInfo fileInfo(m_launchConfig.executable);
        if (!fileInfo.exists()) {
            return "Executable file does not exist: " + m_launchConfig.executable;
        }
        
        if (!fileInfo.isExecutable()) {
            return "File is not executable: " + m_launchConfig.executable;
        }
    }
    
    return QString();
}

QJsonObject GameInfo::toJson() const
{
    QJsonObject obj;
    
    // Basic properties
    obj["title"] = m_title;
    obj["description"] = m_description;
    obj["platform"] = platformString();
    obj["launchConfig"] = m_launchConfig.toJson();
    
    // Identification
    obj["gameId"] = m_gameId;
    obj["installPath"] = m_installPath;
    
    // Visual assets
    obj["coverArtPath"] = m_coverArtPath;
    obj["coverArtUrl"] = m_coverArtUrl.toString();
    
    // Metadata
    obj["lastPlayed"] = m_lastPlayed.toString(Qt::ISODate);
    obj["playtimeMinutes"] = static_cast<double>(m_playtimeMinutes);
    obj["installDate"] = m_installDate.toString(Qt::ISODate);
    obj["sizeBytes"] = static_cast<double>(m_sizeBytes);
    
    // Status
    obj["isInstalled"] = m_isInstalled;
    obj["isFavorite"] = m_isFavorite;
    
    // Tags
    obj["tags"] = QJsonArray::fromStringList(m_tags);
    
    return obj;
}

void GameInfo::fromJson(const QJsonObject& json)
{
    // Basic properties
    m_title = json["title"].toString();
    m_description = json["description"].toString();
    m_platform = platformFromString(json["platform"].toString());
    
    if (json.contains("launchConfig")) {
        m_launchConfig.fromJson(json["launchConfig"].toObject());
    }
    
    // Identification
    m_gameId = json["gameId"].toString();
    m_installPath = json["installPath"].toString();
    
    // Visual assets
    m_coverArtPath = json["coverArtPath"].toString();
    m_coverArtUrl = QUrl(json["coverArtUrl"].toString());
    
    // Metadata
    m_lastPlayed = QDateTime::fromString(json["lastPlayed"].toString(), Qt::ISODate);
    m_playtimeMinutes = static_cast<qint64>(json["playtimeMinutes"].toDouble());
    m_installDate = QDateTime::fromString(json["installDate"].toString(), Qt::ISODate);
    m_sizeBytes = static_cast<qint64>(json["sizeBytes"].toDouble());
    
    // Status
    m_isInstalled = json["isInstalled"].toBool();
    m_isFavorite = json["isFavorite"].toBool();
    
    // Tags
    QJsonArray tagsArray = json["tags"].toArray();
    m_tags.clear();
    for (const auto& tag : tagsArray) {
        m_tags.append(tag.toString());
    }
}

bool GameInfo::operator==(const GameInfo& other) const
{
    return m_gameId == other.m_gameId && 
           m_platform == other.m_platform &&
           m_title == other.m_title;
}

QString GameInfo::formattedPlaytime() const
{
    if (m_playtimeMinutes == 0) {
        return "Never played";
    }
    
    qint64 hours = m_playtimeMinutes / 60;
    qint64 minutes = m_playtimeMinutes % 60;
    
    if (hours == 0) {
        return QString("%1 min").arg(minutes);
    } else if (hours < 24) {
        return QString("%1h %2m").arg(hours).arg(minutes);
    } else {
        qint64 days = hours / 24;
        hours = hours % 24;
        return QString("%1d %2h").arg(days).arg(hours);
    }
}

QString GameInfo::formattedSize() const
{
    if (m_sizeBytes == 0) {
        return "Unknown";
    }
    
    QLocale locale;
    const qint64 kb = 1024;
    const qint64 mb = kb * 1024;
    const qint64 gb = mb * 1024;
    
    if (m_sizeBytes >= gb) {
        return QString("%1 GB").arg(locale.toString(m_sizeBytes / (double)gb, 'f', 1));
    } else if (m_sizeBytes >= mb) {
        return QString("%1 MB").arg(locale.toString(m_sizeBytes / (double)mb, 'f', 1));
    } else if (m_sizeBytes >= kb) {
        return QString("%1 KB").arg(locale.toString(m_sizeBytes / (double)kb, 'f', 0));
    } else {
        return QString("%1 bytes").arg(locale.toString(m_sizeBytes));
    }
}