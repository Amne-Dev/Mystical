#pragma once

#include <QString>
#include <QDateTime>
#include <QUrl>
#include <QMetaType>
#include <QJsonObject>

enum class GamePlatform {
    Unknown,
    Steam,
    EpicGames,
    GOG,
    EAApp,
    Minecraft,
    Roblox,
    Custom
};

enum class LaunchMethod {
    DirectExecutable,
    PlatformLauncher,
    URI
};

struct LaunchConfig {
    QString executable;
    QStringList arguments;
    QString workingDirectory;
    LaunchMethod method = LaunchMethod::DirectExecutable;
    QString platformUri;
    
    // Serialization
    QJsonObject toJson() const;
    void fromJson(const QJsonObject& json);
};

class GameInfo
{
public:
    GameInfo();
    GameInfo(const QString& title, GamePlatform platform);
    GameInfo(const GameInfo& other);
    GameInfo& operator=(const GameInfo& other);
    ~GameInfo() = default;
    
    // Basic properties
    QString title() const { return m_title; }
    void setTitle(const QString& title) { m_title = title; }
    
    QString description() const { return m_description; }
    void setDescription(const QString& description) { m_description = description; }
    
    GamePlatform platform() const { return m_platform; }
    void setPlatform(GamePlatform platform) { m_platform = platform; }
    
    QString platformString() const;
    static GamePlatform platformFromString(const QString& str);
    
    // Launch configuration
    LaunchConfig launchConfig() const { return m_launchConfig; }
    void setLaunchConfig(const LaunchConfig& config) { m_launchConfig = config; }
    
    // Identification
    QString gameId() const { return m_gameId; }
    void setGameId(const QString& id) { m_gameId = id; }
    
    QString installPath() const { return m_installPath; }
    void setInstallPath(const QString& path) { m_installPath = path; }
    
    // Visual assets
    QString coverArtPath() const { return m_coverArtPath; }
    void setCoverArtPath(const QString& path) { m_coverArtPath = path; }
    
    QUrl coverArtUrl() const { return m_coverArtUrl; }
    void setCoverArtUrl(const QUrl& url) { m_coverArtUrl = url; }
    
    // Metadata
    QDateTime lastPlayed() const { return m_lastPlayed; }
    void setLastPlayed(const QDateTime& dateTime) { m_lastPlayed = dateTime; }
    
    qint64 playtimeMinutes() const { return m_playtimeMinutes; }
    void setPlaytimeMinutes(qint64 minutes) { m_playtimeMinutes = minutes; }
    
    QDateTime installDate() const { return m_installDate; }
    void setInstallDate(const QDateTime& dateTime) { m_installDate = dateTime; }
    
    qint64 sizeBytes() const { return m_sizeBytes; }
    void setSizeBytes(qint64 bytes) { m_sizeBytes = bytes; }
    
    // Status
    bool isInstalled() const { return m_isInstalled; }
    void setInstalled(bool installed) { m_isInstalled = installed; }
    
    bool isFavorite() const { return m_isFavorite; }
    void setFavorite(bool favorite) { m_isFavorite = favorite; }
    
    // Tags and categories
    QStringList tags() const { return m_tags; }
    void setTags(const QStringList& tags) { m_tags = tags; }
    void addTag(const QString& tag);
    void removeTag(const QString& tag);
    
    // Validation
    bool isValid() const;
    QString validationError() const;
    
    // Serialization
    QJsonObject toJson() const;
    void fromJson(const QJsonObject& json);
    
    // Operators
    bool operator==(const GameInfo& other) const;
    bool operator!=(const GameInfo& other) const { return !(*this == other); }
    
    // Utility
    QString formattedPlaytime() const;
    QString formattedSize() const;
    
private:
    QString m_title;
    QString m_description;
    GamePlatform m_platform = GamePlatform::Unknown;
    LaunchConfig m_launchConfig;
    
    QString m_gameId;
    QString m_installPath;
    
    QString m_coverArtPath;
    QUrl m_coverArtUrl;
    
    QDateTime m_lastPlayed;
    qint64 m_playtimeMinutes = 0;
    QDateTime m_installDate;
    qint64 m_sizeBytes = 0;
    
    bool m_isInstalled = false;
    bool m_isFavorite = false;
    
    QStringList m_tags;
};

Q_DECLARE_METATYPE(GameInfo)
Q_DECLARE_METATYPE(GamePlatform)
Q_DECLARE_METATYPE(LaunchMethod)