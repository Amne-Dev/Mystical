#pragma once

#include <QObject>
#include <QProcess>
#include <QDateTime>
#include <QHash>
#include <QTimer>
#include "GameInfo.h"

struct RunningGameInfo {
    GameInfo gameInfo;
    QProcess* process = nullptr;
    qint64 processId = 0;
    QDateTime startTime;
    QDateTime lastSeen;
    QString windowTitle;
};

class GameLauncher : public QObject
{
    Q_OBJECT
    Q_PROPERTY(int runningGameCount READ runningGameCount NOTIFY runningGamesChanged)
    
public:
    explicit GameLauncher(QObject* parent = nullptr);
    ~GameLauncher();
    
    // Game launching
    Q_INVOKABLE bool launchGame(const GameInfo& game);
    Q_INVOKABLE bool terminateGame(const QString& gameId, GamePlatform platform);
    
    // Game monitoring
    Q_INVOKABLE bool isGameRunning(const QString& gameId, GamePlatform platform) const;
    QList<RunningGameInfo> getRunningGames() const;
    int runningGameCount() const { return m_runningGames.size(); }
    
    // Platform-specific launch methods
    bool launchSteamGame(const QString& appId);
    bool launchEpicGame(const QString& appName);
    bool launchGOGGame(const QString& gameId);
    bool launchMinecraft();
    bool launchRoblox(const QString& placeId = QString());
    
public slots:
    void terminateAllGames();
    void refreshRunningGames();
    
signals:
    void gameLaunched(const GameInfo& game);
    void gameTerminated(const GameInfo& game, qint64 sessionMinutes);
    void gameLaunchFailed(const GameInfo& game, const QString& error);
    void gameAlreadyRunning(const GameInfo& game);
    void runningGamesChanged();
    
private slots:
    void onGameProcessFinished(const GameInfo& game, int exitCode, QProcess::ExitStatus exitStatus);
    void onGameProcessError(const GameInfo& game, QProcess::ProcessError error);
    void monitorRunningGames();
    
private:
    // Launch method implementations
    bool launchDirectExecutable(const GameInfo& game, const LaunchConfig& config);
    bool launchViaPlatform(const GameInfo& game, const LaunchConfig& config);
    bool launchViaURI(const GameInfo& game, const LaunchConfig& config);
    
    // Process monitoring
    bool isProcessRunning(const QString& processName) const;
    bool terminateGameByName(const QString& gameName) const;
    QString createGameKey(const QString& gameId, GamePlatform platform) const;
    
    // Platform-specific helpers
    QString getSteamExecutablePath() const;
    QString getEpicLauncherPath() const;
    QString getGOGGalaxyPath() const;
    QString getMinecraftLauncherPath() const;
    
    // Member variables
    QHash<QString, RunningGameInfo> m_runningGames;
    QTimer* m_processMonitor;
    
    // Launch configuration
    int m_processStartTimeout = 10000; // 10 seconds
    int m_processMonitorInterval = 5000; // 5 seconds
};