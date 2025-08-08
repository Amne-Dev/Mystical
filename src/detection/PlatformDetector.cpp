#include "PlatformDetector.h"
#include "../utils/FileUtils.h"
#include "../utils/RegistryUtils.h"
#include <QDir>
#include <QFileInfo>
#include <QProcess>
#include <QUrl>
#include <QStandardPaths>
#include <QtConcurrent>
#include <QLoggingCategory>

Q_LOGGING_CATEGORY(platformDetector, "mystical.platform")

PlatformDetector::PlatformDetector(QObject* parent)
    : QObject(parent)
{
}

QFuture<QList<GameInfo>> PlatformDetector::detectGamesAsync()
{
    return QtConcurrent::run([this]() {
        return detectGames();
    });
}

bool PlatformDetector::canLaunchGame(const GameInfo& game) const
{
    // Default implementation - check if game is installed
    // Since executablePath() doesn't exist, we'll just check if it's marked as installed
    return game.isInstalled();
}

QString PlatformDetector::normalizePath(const QString& path) const
{
    return QDir::toNativeSeparators(QDir::cleanPath(path));
}

bool PlatformDetector::fileExists(const QString& path) const
{
    return QFileInfo::exists(path) && QFileInfo(path).isFile();
}

bool PlatformDetector::directoryExists(const QString& path) const
{
    return QFileInfo::exists(path) && QFileInfo(path).isDir();
}

QStringList PlatformDetector::findExecutables(const QString& directory, const QStringList& patterns) const
{
    QStringList executables;
    
    if (!directoryExists(directory)) {
        return executables;
    }
    
    QDir dir(directory);
    QStringList nameFilters;
    
    for (const QString& pattern : patterns) {
        nameFilters << pattern;
    }
    
    QStringList files = dir.entryList(nameFilters, QDir::Files | QDir::Executable);
    
    for (const QString& file : files) {
        executables.append(dir.absoluteFilePath(file));
    }
    
    return executables;
}

QString PlatformDetector::readRegistryValue(const QString& key, const QString& valueName) const
{
#ifdef Q_OS_WIN
    // If RegistryUtils doesn't have these methods, implement basic registry reading here
    // or return empty string for now
    Q_UNUSED(key)
    Q_UNUSED(valueName)
    // TODO: Implement registry reading or use your actual RegistryUtils API
    return QString();
#else
    Q_UNUSED(key)
    Q_UNUSED(valueName)
    return QString();
#endif
}

QStringList PlatformDetector::readRegistrySubKeys(const QString& key) const
{
#ifdef Q_OS_WIN
    // If RegistryUtils doesn't have these methods, implement basic registry reading here
    // or return empty list for now
    Q_UNUSED(key)
    // TODO: Implement registry reading or use your actual RegistryUtils API
    return QStringList();
#else
    Q_UNUSED(key)
    return QStringList();
#endif
}

GameInfo PlatformDetector::createGameFromExecutable(const QString& executablePath, 
                                                   const QString& gameName) const
{
    QFileInfo execInfo(executablePath);
    if (!execInfo.exists() || !execInfo.isFile()) {
        return GameInfo();
    }
    
    QString name = gameName;
    if (name.isEmpty()) {
        name = execInfo.baseName();
        // Capitalize first letter and replace underscores/dashes with spaces
        name = name.replace('_', ' ').replace('-', ' ');
        if (!name.isEmpty()) {
            name[0] = name[0].toUpper();
        }
    }
    
    GameInfo game;
    game.setTitle(name);
    // Store executable path in description since setExecutablePath doesn't exist
    game.setDescription("Executable: " + executablePath);
    game.setInstallPath(execInfo.dir().absolutePath());
    game.setInstalled(true); // Use setInstalled instead of setIsInstalled
    game.setPlatform(platform()); // Use enum instead of string
    
    return game;
}

void PlatformDetector::reportProgress(int current, int total, const QString& currentGame)
{
    m_currentProgress = current;
    m_totalProgress = total;
    
    emit detectionProgress(current, total, currentGame);
    
    if (!currentGame.isEmpty()) {
        qDebug(platformDetector) << "Detection progress:" << current << "/" << total << "-" << currentGame;
    }
}

void PlatformDetector::reportError(const QString& message)
{
    qWarning(platformDetector) << "Platform detection error:" << message;
    emit error(message);
}