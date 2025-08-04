#pragma once

#include "PlatformDetector.h"
#include <QDir>
#include <QJsonObject>

class SteamDetector : public PlatformDetector
{
    Q_OBJECT
    
public:
    explicit SteamDetector(QObject* parent = nullptr);
    
    // PlatformDetector interface
    QString platformName() const override { return "Steam"; }
    GamePlatform platform() const override { return GamePlatform::Steam; }
    QString platformVersion() const override;
    
    bool isAvailable() const override;
    QList<GameInfo> detectGames() override;
    
    bool canLaunchGame(const GameInfo& game) const override;
    QString validateInstallation() const override;
    
    QStringList getInstallationPaths() const override;
    QString getConfigPath() const override;
    QString getLibraryPath() const override;
    
    // Steam-specific features
    bool supportsCoverArt() const override { return true; }
    QString getCoverArtUrl(const GameInfo& game) const override;
    bool supportsPlaytime() const override { return true; }
    
private:
    struct SteamLibraryFolder {
        QString path;
        QString label;
        bool mounted = true;
    };
    
    struct SteamAppManifest {
        QString appId;
        QString name;
        QString installDir;
        QString executable;
        qint64 sizeOnDisk = 0;
        QDateTime lastUpdated;
        bool isInstalled = false;
    };
    
    // Steam detection methods
    QString getSteamInstallPath() const;
    QList<SteamLibraryFolder> getLibraryFolders() const;
    QList<SteamAppManifest> getInstalledApps(const QString& libraryPath) const;
    SteamAppManifest parseAppManifest(const QString& manifestPath) const;
    
    // VDF (Valve Data Format) parsing
    QJsonObject parseVDF(const QString& filePath) const;
    QJsonObject parseVDFContent(const QString& content) const;
    QString parseVDFString(const QString& content, int& pos) const;
    QJsonObject parseVDFObject(const QString& content, int& pos) const;
    
    // Steam-specific utilities
    QString getGameExecutable(const QString& installPath, const QString& appId) const;
    qint64 getPlaytimeMinutes(const QString& appId) const;
    QDateTime getLastPlayedDate(const QString& appId) const;
    QString getSteamGridImageUrl(const QString& appId) const;
    
    // Launch URI generation
    QString createSteamLaunchUri(const QString& appId) const;
    
    // Cached data
    mutable QString m_steamPath;
    mutable QList<SteamLibraryFolder> m_libraryFolders;
};