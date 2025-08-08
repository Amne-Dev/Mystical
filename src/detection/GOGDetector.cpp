#include "GOGDetector.h"
#include <QStandardPaths>
#include <QFileInfo>
#include <QDir>
#include <QJsonDocument>
#include <QSqlQuery>
#include <QSqlError>
#include <QLoggingCategory>
#include <QDirIterator>

Q_LOGGING_CATEGORY(gogDetector, "mystical.gog")

GOGDetector::GOGDetector(QObject* parent)
    : PlatformDetector(parent)
{
}

GOGDetector::~GOGDetector()
{
    closeGOGDatabase();
}

QString GOGDetector::platformVersion() const
{
    QString galaxyPath = getGOGGalaxyPath();
    if (galaxyPath.isEmpty()) {
        return QString();
    }
    
    QFileInfo galaxyInfo(galaxyPath);
    if (galaxyInfo.exists()) {
        return galaxyInfo.lastModified().toString("yyyy.MM.dd");
    }
    
    return "Unknown";
}

bool GOGDetector::isAvailable() const
{
    // Check for either GOG Galaxy or classic GOG installations
    return !getGOGGalaxyPath().isEmpty() || !getInstalledGamesFromRegistry().isEmpty();
}

QList<GameInfo> GOGDetector::detectGames()
{
    emit detectionStarted();
    
    QList<GameInfo> games;
    
    // Try GOG Galaxy database first
    QList<GOGGameInfo> gogGames = getInstalledGamesFromDatabase();
    
    // Fallback to registry for classic GOG games
    if (gogGames.isEmpty()) {
        gogGames = getInstalledGamesFromRegistry();
    }
    
    if (gogGames.isEmpty()) {
        qInfo(gogDetector) << "No GOG games found";
        emit detectionFinished(games);
        return games;
    }
    
    qInfo(gogDetector) << "Found" << gogGames.size() << "GOG games";
    
    int totalGames = gogGames.size();
    int currentGame = 0;
    
    for (const auto& gogGame : gogGames) {
        reportProgress(currentGame++, totalGames, gogGame.title);
        
        if (!gogGame.isInstalled || !directoryExists(gogGame.installPath)) {
            continue;
        }
        
        GameInfo game(gogGame.title, GamePlatform::GOG);
        game.setGameId(gogGame.gameId);
        game.setInstallPath(gogGame.installPath);
        game.setSizeBytes(gogGame.installedSize);
        game.setInstalled(true);
        
        // Set up launch configuration
        LaunchConfig launchConfig;
        
        // Prefer GOG Galaxy if available
        if (!getGOGGalaxyPath().isEmpty()) {
            launchConfig.method = LaunchMethod::URI;
            launchConfig.platformUri = createGOGLaunchUri(gogGame.gameId);
        } else {
            launchConfig.method = LaunchMethod::DirectExecutable;
        }
        
        // Set executable path
        if (!gogGame.executable.isEmpty()) {
            QString executablePath = gogGame.executable;
            if (!QFileInfo(executablePath).isAbsolute()) {
                executablePath = QDir(gogGame.installPath).filePath(executablePath);
            }
            
            if (fileExists(executablePath)) {
                launchConfig.executable = executablePath;
                launchConfig.workingDirectory = gogGame.workingDir.isEmpty() 
                                              ? gogGame.installPath 
                                              : gogGame.workingDir;
                
                // Parse launch command arguments if available
                if (!gogGame.launchCommand.isEmpty()) {
                    QStringList parts = gogGame.launchCommand.split(" ", Qt::SkipEmptyParts);
                    if (parts.size() > 1) {
                        parts.removeFirst(); // Remove executable path
                        launchConfig.arguments = parts;
                    }
                }
            }
        }
        
        game.setLaunchConfig(launchConfig);
        
        // Set cover art URL
        game.setCoverArtUrl(QUrl(getCoverArtUrl(game)));
        
        games.append(game);
        emit gameFound(game);
        
        qDebug(gogDetector) << "Found GOG game:" << gogGame.title << "ID:" << gogGame.gameId;
    }
    
    qInfo(gogDetector) << "GOG detection complete. Found" << games.size() << "games";
    emit detectionFinished(games);
    return games;
}

bool GOGDetector::canLaunchGame(const GameInfo& game) const
{
    if (game.platform() != GamePlatform::GOG) {
        return false;
    }
    
    // Check launch method availability
    const LaunchConfig& config = game.launchConfig();
    
    if (config.method == LaunchMethod::URI) {
        return !getGOGGalaxyPath().isEmpty();
    } else if (config.method == LaunchMethod::DirectExecutable) {
        return fileExists(config.executable);
    }
    
    return false;
}

QString GOGDetector::validateInstallation() const
{
    QString galaxyPath = getGOGGalaxyPath();
    if (!galaxyPath.isEmpty()) {
        if (!fileExists(galaxyPath)) {
            return "GOG Galaxy executable not found: " + galaxyPath;
        }
        return QString(); // Valid Galaxy installation
    }
    
    // Check for classic GOG games in registry
    QList<GOGGameInfo> registryGames = getInstalledGamesFromRegistry();
    if (registryGames.isEmpty()) {
        return "No GOG installation found (neither Galaxy nor classic games)";
    }
    
    return QString(); // Valid classic GOG installation
}

QStringList GOGDetector::getInstallationPaths() const
{
    QStringList paths;
    QString galaxyPath = getGOGGalaxyPath();
    if (!galaxyPath.isEmpty()) {
        paths.append(QFileInfo(galaxyPath).dir().absolutePath());
    }
    return paths;
}

QString GOGDetector::getConfigPath() const
{
    QString localAppData = QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation);
    return QDir(localAppData).filePath("GOG.com/Galaxy");
}

QString GOGDetector::getLibraryPath() const
{
    return getGOGDatabasePath();
}

QString GOGDetector::getCoverArtUrl(const GameInfo& game) const
{
    if (game.gameId().isEmpty()) {
        return QString();
    }
    
    // GOG cover art URL format
    return QString("https://images.gog-statics.com/games/%1/box_art.jpg")
           .arg(game.gameId());
}

// Private methods implementation

QString GOGDetector::getGOGInstallPath() const
{
    if (!m_gogPath.isEmpty()) {
        return m_gogPath;
    }
    
    // Try registry for GOG install path
    QString regPath = readRegistryValue(
        "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\GOG.com\\GOGPACKAGER",
        "PATH"
    );
    
    if (!regPath.isEmpty() && directoryExists(regPath)) {
        m_gogPath = normalizePath(regPath);
        return m_gogPath;
    }
    
    // Fallback to common installation paths
    QStringList commonPaths = {
        "C:/GOG Games",
        "C:/Program Files (x86)/GOG.com",
        "C:/Program Files/GOG.com"
    };
    
    for (const QString& path : commonPaths) {
        if (directoryExists(path)) {
            m_gogPath = normalizePath(path);
            return m_gogPath;
        }
    }
    
    return QString();
}

QString GOGDetector::getGOGGalaxyPath() const
{
    if (!m_galaxyPath.isEmpty()) {
        return m_galaxyPath;
    }
    
    // Try registry for GOG Galaxy
    QString regPath = readRegistryValue(
        "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\GOG.com\\GalaxyClient\\paths",
        "client"
    );
    
    if (!regPath.isEmpty()) {
        QString galaxyExe = QDir(regPath).filePath("GalaxyClient.exe");
        if (fileExists(galaxyExe)) {
            m_galaxyPath = galaxyExe;
            return m_galaxyPath;
        }
    }
    
    // Try common Galaxy installation paths
    QStringList galaxyPaths = {
        "C:/Program Files (x86)/GOG Galaxy/GalaxyClient.exe",
        "C:/Program Files/GOG Galaxy/GalaxyClient.exe"
    };
    
    for (const QString& path : galaxyPaths) {
        if (fileExists(path)) {
            m_galaxyPath = path;
            return m_galaxyPath;
        }
    }
    
    return QString();
}

QString GOGDetector::getGOGDatabasePath() const
{
    QString programData = QStandardPaths::writableLocation(QStandardPaths::GenericConfigLocation);
    return QDir(programData).filePath("GOG.com/Galaxy/storage/galaxy-2.0.db");
}

QList<GOGDetector::GOGGameInfo> GOGDetector::getInstalledGamesFromDatabase() const
{
    QList<GOGGameInfo> games;
    
    if (!openGOGDatabase()) {
        return games;
    }
    
    QSqlQuery query(m_gogDatabase);
    QString sql = R"(
        SELECT 
            gameId,
            gameName,
            installPath,
            executable,
            version,
            installedSize
        FROM GamePieces 
        WHERE isInstalled = 1
    )";
    
    if (!query.exec(sql)) {
        qWarning(gogDetector) << "Failed to query GOG database:" << query.lastError().text();
        return games;
    }
    
    while (query.next()) {
        GOGGameInfo game;
        game.gameId = query.value("gameId").toString();
        game.title = query.value("gameName").toString();
        game.installPath = query.value("installPath").toString();
        game.executable = query.value("executable").toString();
        game.version = query.value("version").toString();
        game.installedSize = query.value("installedSize").toLongLong();
        game.isInstalled = true;
        
        if (!game.gameId.isEmpty() && !game.title.isEmpty()) {
            games.append(game);
        }
    }
    
    return games;
}

QList<GOGDetector::GOGGameInfo> GOGDetector::getInstalledGamesFromRegistry() const
{
    QList<GOGGameInfo> games;
    
    // Check both 32-bit and 64-bit registry
    QStringList registryPaths = {
        "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\GOG.com\\Games",
        "HKEY_LOCAL_MACHINE\\SOFTWARE\\GOG.com\\Games"
    };
    
    for (const QString& regPath : registryPaths) {
        QStringList gameKeys = readRegistrySubKeys(regPath);
        
        for (const QString& gameKey : gameKeys) {
            QString fullGamePath = regPath + "\\" + gameKey;
            
            GOGGameInfo game;
            game.gameId = gameKey;
            game.title = readRegistryValue(fullGamePath, "GAMENAME");
            game.installPath = readRegistryValue(fullGamePath, "PATH");
            game.executable = readRegistryValue(fullGamePath, "EXE");
            game.workingDir = readRegistryValue(fullGamePath, "WORKINGDIR");
            game.launchCommand = readRegistryValue(fullGamePath, "LAUNCHCOMMAND");
            game.version = readRegistryValue(fullGamePath, "VERSION");
            
            // Check if game is actually installed
            if (!game.installPath.isEmpty() && directoryExists(game.installPath)) {
                game.isInstalled = true;
                
                // Calculate installed size
                QDirIterator dirIt(game.installPath, QDir::Files, QDirIterator::Subdirectories);
                qint64 totalSize = 0;
                int fileCount = 0;
                
                while (dirIt.hasNext() && fileCount < 1000) {
                    dirIt.next();
                    totalSize += dirIt.fileInfo().size();
                    fileCount++;
                }
                
                game.installedSize = totalSize;
                games.append(game);
            }
        }
    }
    
    return games;
}

bool GOGDetector::openGOGDatabase() const
{
    if (m_gogDatabase.isOpen()) {
        return true;
    }
    
    QString dbPath = getGOGDatabasePath();
    if (!fileExists(dbPath)) {
        qDebug(gogDetector) << "GOG Galaxy database not found:" << dbPath;
        return false;
    }
    
    m_gogDatabase = QSqlDatabase::addDatabase("QSQLITE", "GOG_DB");
    m_gogDatabase.setDatabaseName(dbPath);
    
    if (!m_gogDatabase.open()) {
        qWarning(gogDetector) << "Failed to open GOG database:" << m_gogDatabase.lastError().text();
        return false;
    }
    
    return true;
}

void GOGDetector::closeGOGDatabase()
{
    if (m_gogDatabase.isOpen()) {
        m_gogDatabase.close();
    }
    QSqlDatabase::removeDatabase("GOG_DB");
}

QString GOGDetector::createGOGLaunchUri(const QString& gameId) const
{
    return QString("goggalaxy://openGameView/%1").arg(gameId);
}

QString GOGDetector::getGOGCoverArt(const QString& gameId) const
{
    return QString("https://images.gog-statics.com/games/%1/box_art.jpg").arg(gameId);
}