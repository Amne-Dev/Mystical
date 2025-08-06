#pragma once

#include "PlatformDetector.h"
#include <QJsonObject>
#include <QJsonArray>

class EpicDetector : public PlatformDetector
{
    Q_OBJECT
    
public:
    explicit EpicDetector(QObject* parent = nullptr);
    
    // PlatformDetector interface
    QString platformName() const override { return "Epic Games"; }
    GamePlatform platform() const override { return GamePlatform::EpicGames; }
    QString platformVersion() const override;
    
    bool isAvailable() const override;
    QList<GameInfo> detectGames() override;
    
    bool canLaunchGame(const GameInfo& game) const override;
    QString validateInstallation() const override;
    
    QStringList getInstallationPaths() const override;
    QString getConfigPath() const override;
    QString getLibraryPath() const override;
    
    // Epic-specific features
    bool supportsCoverArt() const override { return true; }
    QString getCoverArtUrl(const GameInfo& game) const override;
    
private:
    struct EpicGameManifest {
        QString appName;
        QString catalogItemId;
        QString displayName;
        QString installLocation;
        QString launchExecutable;
        QString launchCommand;
        QStringList catalogNamespace;
        QStringList catalogCategories;
        bool canRunOffline = false;
        bool requiresOwnedDLC = false;
        qint64 installSize = 0;
    };
    
    // Epic detection methods
    QString getEpicInstallPath() const;
    QString getEpicLauncherPath() const;
    QList<EpicGameManifest> getInstalledGames() const;
    EpicGameManifest parseManifestFile(const QString& manifestPath) const;
    
    // Epic-specific utilities
    QString createEpicLaunchUri(const QString& appName) const;
    QString getEpicCoverArt(const QString& catalogItemId, const QString& appName) const;
    
    // Cached data
    mutable QString m_epicPath;
    mutable QString m_launcherPath;
};
