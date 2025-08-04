#pragma once

#include <QObject>
#include <QList>
#include <QString>
#include <QFuture>
#include "../core/GameInfo.h"

class PlatformDetector : public QObject
{
    Q_OBJECT
    
public:
    explicit PlatformDetector(QObject* parent = nullptr);
    virtual ~PlatformDetector() = default;
    
    // Platform identification
    virtual QString platformName() const = 0;
    virtual GamePlatform platform() const = 0;
    virtual QString platformVersion() const { return QString(); }
    
    // Detection capabilities
    virtual bool isAvailable() const = 0;
    virtual bool requiresInstallation() const { return true; }
    
    // Synchronous detection
    virtual QList<GameInfo> detectGames() = 0;
    
    // Asynchronous detection
    virtual QFuture<QList<GameInfo>> detectGamesAsync();
    
    // Validation and testing
    virtual bool canLaunchGame(const GameInfo& game) const;
    virtual QString validateInstallation() const { return QString(); }
    
    // Platform-specific paths
    virtual QStringList getInstallationPaths() const { return QStringList(); }
    virtual QString getConfigPath() const { return QString(); }
    virtual QString getLibraryPath() const { return QString(); }
    
    // Metadata and cover art
    virtual bool supportsCoverArt() const { return false; }
    virtual QString getCoverArtUrl(const GameInfo& game) const { Q_UNUSED(game); return QString(); }
    virtual bool supportsPlaytime() const { return false; }
    
signals:
    void detectionStarted();
    void detectionProgress(int current, int total, const QString& currentGame);
    void detectionFinished(const QList<GameInfo>& games);
    void gameFound(const GameInfo& game);
    void error(const QString& message);
    
protected:
    // Helper methods for subclasses
    QString normalizePath(const QString& path) const;
    bool fileExists(const QString& path) const;
    bool directoryExists(const QString& path) const;
    QStringList findExecutables(const QString& directory, const QStringList& patterns) const;
    
    // Registry helpers (Windows)
    QString readRegistryValue(const QString& key, const QString& valueName) const;
    QStringList readRegistrySubKeys(const QString& key) const;
    
    // Common game detection patterns
    GameInfo createGameFromExecutable(const QString& executablePath, 
                                     const QString& gameName = QString()) const;
    
    // Progress reporting
    void reportProgress(int current, int total, const QString& currentGame = QString());
    void reportError(const QString& message);
    
private:
    int m_currentProgress = 0;
    int m_totalProgress = 0;
};