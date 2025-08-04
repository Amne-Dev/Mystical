#pragma once

#include "PlatformDetector.h"
#include <QJsonObject>
#include <QSqlDatabase>

class GOGDetector : public PlatformDetector
{
    Q_OBJECT
    
public:
    explicit GOGDetector(QObject* parent = nullptr);
    ~GOGDetector();
    
    // PlatformDetector interface
    QString platformName() const override { return "GOG Galaxy"; }
    GamePlatform platform() const override { return GamePlatform::GOG; }
    QString platformVersion() const override;
    
    bool isAvailable() const override;
    QList<GameInfo> detectGames() override;
    
    bool canLaunchGame(const GameInfo& game) const override;
    QString validateInstallation() const override;
    
    QStringList getInstallationPaths() const override;
    QString getConfigPath() const override;
    QString getLibraryPath() const override;
    
    // GOG-specific features
    bool supportsCoverArt() const override { return true; }
    QString getCoverArtUrl(const GameInfo& game) const override;
    
private:
    struct GOGGameInfo {
        QString gameId;
        QString title;
        QString installPath;
        QString executable;
        QString workingDir;
        QString launchCommand;
        QString version;
        QString buildId;
        qint64 installedSize = 0;
        bool isDLC = false;
        bool isInstalled = false;
    };
    
    // GOG detection methods
    QString getGOGInstallPath() const;
    QString getGOGGalaxyPath() const;
    QString getGOGDatabasePath() const;
    QList<GOGGameInfo> getInstalledGamesFromDatabase() const;
    QList<GOGGameInfo> getInstalledGamesFromRegistry() const;
    
    // Database operations
    bool openGOGDatabase();
    void closeGOGDatabase();
    
    // GOG-specific utilities
    QString createGOGLaunchUri(const QString& gameId) const;
    QString getGOGCoverArt(const QString& gameId) const;
    QString parseGOGGameInfo(const QString& gameInfoPath) const;
    
    // Cached data
    mutable QString m_gogPath;
    mutable QString m_galaxyPath;
    QSqlDatabase m_gogDatabase;
};
