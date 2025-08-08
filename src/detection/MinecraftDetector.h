#pragma once

#include "PlatformDetector.h"
#include <QJsonObject>

class MinecraftDetector : public PlatformDetector
{
    Q_OBJECT
    
public:
    explicit MinecraftDetector(QObject* parent = nullptr);
    virtual ~MinecraftDetector(); // Make sure this matches the cpp implementation
    
    // PlatformDetector interface
    QString platformName() const override; // Remove the implementation from header
    GamePlatform platform() const override { return GamePlatform::Minecraft; }
    QString platformVersion() const override;
    
    bool isAvailable() const override;
    QList<GameInfo> detectGames() override;
    
    bool canLaunchGame(const GameInfo& game) const override;
    QString validateInstallation() const override;
    
    QStringList getInstallationPaths() const override;
    QString getConfigPath() const override;
    QString getLibraryPath() const override;
    
    // Minecraft-specific features
    bool supportsCoverArt() const override { return false; }
    
private:
    struct MinecraftInstallation {
        QString id;
        QString name;
        QString type; // release, snapshot, beta, alpha
        QString gameDir;
        QString javaPath;
        QString launcherPath;
        QString version;
        bool isInstalled = false;
        bool isLegacy = false;
    };
    
    // Minecraft detection methods
    QString getMinecraftPath() const;
    QString getMinecraftLauncherPath() const;
    QString getMinecraftJavaPath() const;
    QList<MinecraftInstallation> getMinecraftInstallations() const;
    MinecraftInstallation parseProfile(const QJsonObject& profile, const QString& profileId) const;
    
    // Version detection
    QString getLatestVersion() const;
    QStringList getInstalledVersions() const;
    
    // Launch utilities
    QString createMinecraftLaunchCommand(const MinecraftInstallation& installation) const;
    
    // Cached data
    mutable QString m_minecraftPath;
    mutable QString m_launcherPath;
    mutable QString m_javaPath;
};