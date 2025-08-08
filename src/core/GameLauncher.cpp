#include "GameLauncher.h"
#include <QProcess>
#include <QDir>
#include <QFileInfo>
#include <QDesktopServices>
#include <QUrl>
#include <QLoggingCategory>
#include <QStandardPaths>
#include <QTimer>

#ifdef Q_OS_WIN
#include <windows.h>
#include <shellapi.h>
#endif

Q_LOGGING_CATEGORY(gameLauncher, "mystical.launcher")

GameLauncher::GameLauncher(QObject* parent)
    : QObject(parent)
    , m_processMonitor(new QTimer(this))
{
    // Monitor running processes every 5 seconds
    m_processMonitor->setInterval(5000);
    connect(m_processMonitor, &QTimer::timeout, this, &GameLauncher::monitorRunningGames);
    m_processMonitor->start();
}

GameLauncher::~GameLauncher()
{
    // Clean up any running processes
    for (auto it = m_runningGames.begin(); it != m_runningGames.end(); ++it) {
        if (it.value().process && it.value().process->state() == QProcess::Running) {
            it.value().process->kill();
            it.value().process->waitForFinished(3000);
        }
    }
}

bool GameLauncher::launchGame(const GameInfo& game)
{
    if (!game.isValid()) {
        QString error = game.validationError();
        qWarning(gameLauncher) << "Cannot launch invalid game:" << error;
        emit gameLaunchFailed(game, error);
        return false;
    }

    if (isGameRunning(game.gameId(), game.platform())) {
        qInfo(gameLauncher) << "Game already running:" << game.title();
        emit gameAlreadyRunning(game);
        return true;
    }

    qInfo(gameLauncher) << "Launching game:" << game.title() << "Platform:" << game.platformString();

    LaunchConfig config = game.launchConfig();
    bool success = false;

    switch (config.method) {
        case LaunchMethod::DirectExecutable:
            success = launchDirectExecutable(game, config);
            break;
        case LaunchMethod::PlatformLauncher:
            success = launchViaPlatform(game, config);
            break;
        case LaunchMethod::URI:
            success = launchViaURI(game, config);
            break;
        default:
            qWarning(gameLauncher) << "Unknown launch method for game:" << game.title();
            emit gameLaunchFailed(game, "Unknown launch method");
            return false;
    }

    if (success) {
        // Track the running game
        RunningGameInfo runningInfo;
        runningInfo.gameInfo = game;
        runningInfo.startTime = QDateTime::currentDateTime();
        runningInfo.lastSeen = runningInfo.startTime;
        
        QString gameKey = createGameKey(game.gameId(), game.platform());
        m_runningGames[gameKey] = runningInfo;

        emit gameLaunched(game);
        qInfo(gameLauncher) << "Successfully launched game:" << game.title();
    }

    return success;
}

bool GameLauncher::terminateGame(const QString& gameId, GamePlatform platform)
{
    QString gameKey = createGameKey(gameId, platform);
    auto it = m_runningGames.find(gameKey);
    
    if (it == m_runningGames.end()) {
        qWarning(gameLauncher) << "Game not found in running games:" << gameId;
        return false;
    }

    RunningGameInfo& runningInfo = it.value();
    bool success = false;

    if (runningInfo.process) {
        // Direct process control
        runningInfo.process->terminate();
        if (!runningInfo.process->waitForFinished(5000)) {
            runningInfo.process->kill();
            runningInfo.process->waitForFinished(3000);
        }
        success = (runningInfo.process->state() == QProcess::NotRunning);
    } else {
        // Try to terminate by process name/window title
        success = terminateGameByName(runningInfo.gameInfo.title());
    }

    if (success) {
        // Calculate playtime
        qint64 sessionMinutes = runningInfo.startTime.secsTo(QDateTime::currentDateTime()) / 60;
        
        emit gameTerminated(runningInfo.gameInfo, sessionMinutes);
        m_runningGames.erase(it);
        
        qInfo(gameLauncher) << "Terminated game:" << runningInfo.gameInfo.title() 
                           << "Session time:" << sessionMinutes << "minutes";
    }

    return success;
}

bool GameLauncher::isGameRunning(const QString& gameId, GamePlatform platform) const
{
    QString gameKey = createGameKey(gameId, platform);
    return m_runningGames.contains(gameKey);
}

QList<RunningGameInfo> GameLauncher::getRunningGames() const
{
    QList<RunningGameInfo> games;
    for (const auto& info : m_runningGames) {
        games.append(info);
    }
    return games;
}

// Private implementation methods

bool GameLauncher::launchDirectExecutable(const GameInfo& game, const LaunchConfig& config)
{
    QFileInfo execInfo(config.executable);
    if (!execInfo.exists()) {
        QString error = QString("Executable not found: %1").arg(config.executable);
        emit gameLaunchFailed(game, error);
        return false;
    }

    if (!execInfo.isExecutable()) {
        QString error = QString("File is not executable: %1").arg(config.executable);
        emit gameLaunchFailed(game, error);
        return false;
    }

    QString workingDir = config.workingDirectory;
    if (workingDir.isEmpty()) {
        workingDir = execInfo.dir().absolutePath();
    }

    QProcess* process = new QProcess(this);
    process->setWorkingDirectory(workingDir);
    process->setProgram(config.executable);
    process->setArguments(config.arguments);

    // Connect process signals
    connect(process, QOverload<int, QProcess::ExitStatus>::of(&QProcess::finished),
            this, [this, game](int exitCode, QProcess::ExitStatus exitStatus) {
                onGameProcessFinished(game, exitCode, exitStatus);
            });

    connect(process, &QProcess::errorOccurred,
            this, [this, game](QProcess::ProcessError error) {
                onGameProcessError(game, error);
            });

    // Start the process
    process->start();
    if (!process->waitForStarted(10000)) {
        QString error = QString("Failed to start process: %1").arg(process->errorString());
        emit gameLaunchFailed(game, error);
        process->deleteLater();
        return false;
    }

    // Store process reference
    QString gameKey = createGameKey(game.gameId(), game.platform());
    if (m_runningGames.contains(gameKey)) {
        m_runningGames[gameKey].process = process;
        m_runningGames[gameKey].processId = process->processId();
    }

    return true;
}

bool GameLauncher::launchViaPlatform(const GameInfo& game, const LaunchConfig& config)
{
    // This would launch through the platform's launcher
    // For now, fall back to URI launch if available
    if (!config.platformUri.isEmpty()) {
        LaunchConfig uriConfig = config;
        uriConfig.method = LaunchMethod::URI;
        return launchViaURI(game, uriConfig);
    }

    QString error = "Platform launcher not implemented";
    emit gameLaunchFailed(game, error);
    return false;
}

bool GameLauncher::launchViaURI(const GameInfo& game, const LaunchConfig& config)
{
    if (config.platformUri.isEmpty()) {
        emit gameLaunchFailed(game, "No platform URI configured");
        return false;
    }

    QUrl uri(config.platformUri);
    if (!uri.isValid()) {
        QString error = QString("Invalid URI: %1").arg(config.platformUri);
        emit gameLaunchFailed(game, error);
        return false;
    }

    bool success = QDesktopServices::openUrl(uri);
    if (!success) {
        QString error = QString("Failed to open URI: %1").arg(config.platformUri);
        emit gameLaunchFailed(game, error);
        return false;
    }

    // For URI launches, we can't directly monitor the process
    // We'll rely on window detection or platform-specific monitoring
    return true;
}

void GameLauncher::onGameProcessFinished(const GameInfo& game, int exitCode, QProcess::ExitStatus exitStatus)
{
    Q_UNUSED(exitCode)
    Q_UNUSED(exitStatus)

    QString gameKey = createGameKey(game.gameId(), game.platform());
    auto it = m_runningGames.find(gameKey);
    
    if (it != m_runningGames.end()) {
        RunningGameInfo& runningInfo = it.value();
        
        // Calculate session time
        qint64 sessionMinutes = runningInfo.startTime.secsTo(QDateTime::currentDateTime()) / 60;
        
        emit gameTerminated(game, sessionMinutes);
        
        // Clean up process
        if (runningInfo.process) {
            runningInfo.process->deleteLater();
        }
        
        m_runningGames.erase(it);
        
        qInfo(gameLauncher) << "Game process finished:" << game.title() 
                           << "Session time:" << sessionMinutes << "minutes";
    }
}

void GameLauncher::onGameProcessError(const GameInfo& game, QProcess::ProcessError error)
{
    QString errorString;
    switch (error) {
        case QProcess::FailedToStart:
            errorString = "Failed to start process";
            break;
        case QProcess::Crashed:
            errorString = "Process crashed";
            break;
        case QProcess::Timedout:
            errorString = "Process timed out";
            break;
        case QProcess::WriteError:
            errorString = "Write error";
            break;
        case QProcess::ReadError:
            errorString = "Read error";
            break;
        case QProcess::UnknownError:
        default:
            errorString = "Unknown process error";
            break;
    }

    qWarning(gameLauncher) << "Game process error:" << game.title() << errorString;
    emit gameLaunchFailed(game, errorString);

    // Clean up
    QString gameKey = createGameKey(game.gameId(), game.platform());
    auto it = m_runningGames.find(gameKey);
    if (it != m_runningGames.end()) {
        if (it.value().process) {
            it.value().process->deleteLater();
        }
        m_runningGames.erase(it);
    }
}

void GameLauncher::monitorRunningGames()
{
    QMutableHashIterator<QString, RunningGameInfo> it(m_runningGames);
    
    while (it.hasNext()) {
        it.next();
        RunningGameInfo& runningInfo = it.value();
        
        bool isStillRunning = false;
        
        if (runningInfo.process) {
            // Check if process is still running
            isStillRunning = (runningInfo.process->state() == QProcess::Running);
        } else {
            // Check by process name or window title
            isStillRunning = isProcessRunning(runningInfo.gameInfo.title());
        }
        
        if (isStillRunning) {
            runningInfo.lastSeen = QDateTime::currentDateTime();
        } else {
            // Game has stopped running
            qint64 sessionMinutes = runningInfo.startTime.secsTo(runningInfo.lastSeen) / 60;
            emit gameTerminated(runningInfo.gameInfo, sessionMinutes);
            
            if (runningInfo.process) {
                runningInfo.process->deleteLater();
            }
            
            it.remove();
        }
    }
}

QString GameLauncher::createGameKey(const QString& gameId, GamePlatform platform) const
{
    return QString("%1_%2").arg(static_cast<int>(platform)).arg(gameId);
}

bool GameLauncher::terminateGameByName(const QString& gameName) const
{
#ifdef Q_OS_WIN
    // Windows-specific process termination
    // This is a simplified implementation
    Q_UNUSED(gameName)
    return false;
#else
    Q_UNUSED(gameName)
    return false;
#endif
}

bool GameLauncher::isProcessRunning(const QString& processName) const
{
#ifdef Q_OS_WIN
    // Windows-specific process detection
    // This would enumerate running processes and check for matches
    Q_UNUSED(processName)
    return false;
#else
    Q_UNUSED(processName)
    return false;
#endif
}

void GameLauncher::terminateAllGames()
{
    qInfo(gameLauncher) << "Terminating all running games...";
    
    QList<QString> gameKeys = m_runningGames.keys();
    for (const QString& gameKey : gameKeys) {
        const RunningGameInfo& runningInfo = m_runningGames[gameKey];
        
        if (runningInfo.process && runningInfo.process->state() == QProcess::Running) {
            runningInfo.process->terminate();
            if (!runningInfo.process->waitForFinished(3000)) {
                runningInfo.process->kill();
                runningInfo.process->waitForFinished(1000);
            }
        } else {
            // Try to terminate by process name
            terminateGameByName(runningInfo.gameInfo.title());
        }
        
        // Calculate session time
        qint64 sessionMinutes = runningInfo.startTime.secsTo(QDateTime::currentDateTime()) / 60;
        emit gameTerminated(runningInfo.gameInfo, sessionMinutes);
    }
    
    // Clear all running games
    for (auto it = m_runningGames.begin(); it != m_runningGames.end(); ++it) {
        if (it.value().process) {
            it.value().process->deleteLater();
        }
    }
    m_runningGames.clear();
    
    emit runningGamesChanged();
    qInfo(gameLauncher) << "All games terminated";
}

void GameLauncher::refreshRunningGames()
{
    qInfo(gameLauncher) << "Refreshing running games list...";
    
    // Force a check of all running games
    monitorRunningGames();
    
    emit runningGamesChanged();
    qInfo(gameLauncher) << "Running games refreshed. Currently running:" << m_runningGames.size();
}