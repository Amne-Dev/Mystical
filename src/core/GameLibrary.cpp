#include "GameLibrary.h"
#include "GameLauncher.h"
#include "../detection/PlatformDetector.h"
#include "../detection/SteamDetector.h"
#include "../detection/EpicDetector.h"
#include "../detection/GOGDetector.h"
#include "../detection/MinecraftDetector.h"
#include <QStandardPaths>
#include <QDir>
#include <QSqlDatabase>
#include <QSqlQuery>
#include <QSqlError>
#include <QJsonDocument>
#include <QJsonArray>
#include <QtConcurrent>
#include <QMutexLocker>
#include <QLoggingCategory>

Q_LOGGING_CATEGORY(gameLibrary, "mystical.library")

GameLibrary::GameLibrary(QObject* parent)
    : QObject(parent)
    , m_launcher(new GameLauncher(this))
    , m_scanTimer(new QTimer(this))
{
    // Initialize database
    initializeDatabase();
    
    // Initialize platform detectors
    initializeDetectors();
    
    // Setup scan timer for periodic updates
    m_scanTimer->setSingleShot(true);
    connect(m_scanTimer, &QTimer::timeout, this, &GameLibrary::onScanTimerTimeout);
    
    // Connect launcher signals
    connect(m_launcher, &GameLauncher::gameLaunched, this, &GameLibrary::gameLatched);
    connect(m_launcher, &GameLauncher::gameLaunchFailed, this, &GameLibrary::gameLaunchFailed);
    connect(m_launcher, &GameLauncher::gameTerminated, this, 
            [this](const GameInfo& game, qint64 sessionMinutes) {
                updateGamePlaytime(game.gameId(), game.platform(), sessionMinutes);
            });
    
    qInfo(gameLibrary) << "GameLibrary initialized";
}

GameLibrary::~GameLibrary()
{
    // Stop any running scan
    stopScan();
    
    // Save current state
    saveLibrary();
    
    // Clean up detectors
    qDeleteAll(m_detectors);
}

QString GameLibrary::lastScanTime() const
{
    if (m_lastScanTime.isValid()) {
        return m_lastScanTime.toString("hh:mm:ss");
    }
    return "Never";
}

QList<GameInfo> GameLibrary::getAllGames() const
{
    QMutexLocker locker(&m_gamesMutex);
    return m_games;
}

GameInfo GameLibrary::getGame(const QString& gameId, GamePlatform platform) const
{
    const GameInfo* game = findGame(gameId, platform);
    if (game) {
        return *game;
    }
    return GameInfo();
}

bool GameLibrary::addGame(const GameInfo& game)
{
    if (!game.isValid()) {
        qWarning(gameLibrary) << "Cannot add invalid game:" << game.validationError();
        return false;
    }
    
    QMutexLocker locker(&m_gamesMutex);
    
    // Check for duplicates
    if (findGame(game.gameId(), game.platform())) {
        qWarning(gameLibrary) << "Game already exists:" << game.title();
        return false;
    }
    
    m_games.append(game);
    locker.unlock();
    
    // Save to database
    if (!saveGameToDatabase(game)) {
        qWarning(gameLibrary) << "Failed to save game to database:" << game.title();
        // Remove from memory if database save failed
        locker.relock();
        m_games.removeOne(game);
        return false;
    }
    
    emit gameAdded(game);
    emit gameCountChanged();
    emit libraryDataChanged();
    
    qInfo(gameLibrary) << "Added game:" << game.title();
    return true;
}

bool GameLibrary::updateGame(const GameInfo& game)
{
    if (!game.isValid()) {
        qWarning(gameLibrary) << "Cannot update invalid game:" << game.validationError();
        return false;
    }
    
    QMutexLocker locker(&m_gamesMutex);
    
    GameInfo* existing = findGame(game.gameId(), game.platform());
    if (!existing) {
        qWarning(gameLibrary) << "Game not found for update:" << game.title();
        return false;
    }
    
    *existing = game;
    locker.unlock();
    
    // Update in database
    if (!updateGameInDatabase(game)) {
        qWarning(gameLibrary) << "Failed to update game in database:" << game.title();
        return false;
    }
    
    emit gameUpdated(game);
    emit libraryDataChanged();
    
    qDebug(gameLibrary) << "Updated game:" << game.title();
    return true;
}

bool GameLibrary::removeGame(const QString& gameId, GamePlatform platform)
{
    QMutexLocker locker(&m_gamesMutex);
    
    auto it = std::find_if(m_games.begin(), m_games.end(),
        [&](const GameInfo& game) {
            return game.gameId() == gameId && game.platform() == platform;
        });
    
    if (it == m_games.end()) {
        qWarning(gameLibrary) << "Game not found for removal:" << gameId;
        return false;
    }
    
    GameInfo removedGame = *it;
    m_games.erase(it);
    locker.unlock();
    
    // Remove from database
    if (!deleteGameFromDatabase(gameId, platform)) {
        qWarning(gameLibrary) << "Failed to remove game from database:" << gameId;
        // Add back to memory if database removal failed
        locker.relock();
        m_games.append(removedGame);
        return false;
    }
    
    emit gameRemoved(gameId, platform);
    emit gameCountChanged();
    emit libraryDataChanged();
    
    qInfo(gameLibrary) << "Removed game:" << removedGame.title();
    return true;
}

bool GameLibrary::launchGame(const QString& gameId, GamePlatform platform)
{
    const GameInfo* game = findGame(gameId, platform);
    if (!game) {
        qWarning(gameLibrary) << "Game not found for launch:" << gameId;
        return false;
    }
    
    // Update last played time
    GameInfo updatedGame = *game;
    updatedGame.setLastPlayed(QDateTime::currentDateTime());
    updateGame(updatedGame);
    
    return m_launcher->launchGame(*game);
}

bool GameLibrary::launchGameByIndex(int index)
{
    QMutexLocker locker(&m_gamesMutex);
    
    if (index < 0 || index >= m_games.size()) {
        qWarning(gameLibrary) << "Invalid game index for launch:" << index;
        return false;
    }
    
    const GameInfo& game = m_games[index];
    locker.unlock();
    
    return launchGame(game.gameId(), game.platform());
}

void GameLibrary::startInitialScan()
{
    if (m_isScanning) {
        qInfo(gameLibrary) << "Scan already in progress";
        return;
    }
    
    setInitializing(true);
    
    // Load existing games from database first
    loadGamesFromDatabase();
    
    // Start background detection
    emit scanStarted("Initial");
    qInfo(gameLibrary) << "Starting initial game scan";
    
    m_scanFuture = QtConcurrent::run([this]() {
        runDetection(true); // Full scan
    });
}

void GameLibrary::startQuickScan()
{
    if (m_isScanning) {
        qInfo(gameLibrary) << "Scan already in progress";
        return;
    }
    
    emit scanStarted("Quick");
    qInfo(gameLibrary) << "Starting quick game scan";
    
    m_scanFuture = QtConcurrent::run([this]() {
        runDetection(false); // Quick scan
    });
}

void GameLibrary::startFullScan()
{
    if (m_isScanning) {
        stopScan();
    }
    
    emit scanStarted("Full");
    qInfo(gameLibrary) << "Starting full game scan";
    
    m_scanFuture = QtConcurrent::run([this]() {
        runDetection(true); // Full scan
    });
}

void GameLibrary::stopScan()
{
    if (!m_isScanning) {
        return;
    }
    
    qInfo(gameLibrary) << "Stopping game scan";
    
    if (m_scanFuture.isRunning()) {
        m_scanFuture.cancel();
        m_scanFuture.waitForFinished();
    }
    
    setScanning(false);
}

QList<GameInfo> GameLibrary::getGamesByPlatform(GamePlatform platform) const
{
    QMutexLocker locker(&m_gamesMutex);
    
    QList<GameInfo> filtered;
    for (const GameInfo& game : m_games) {
        if (game.platform() == platform) {
            filtered.append(game);
        }
    }
    
    return filtered;
}

QList<GameInfo> GameLibrary::searchGames(const QString& query) const
{
    if (query.isEmpty()) {
        return getAllGames();
    }
    
    QMutexLocker locker(&m_gamesMutex);
    
    QString lowerQuery = query.toLower();
    QList<GameInfo> results;
    
    for (const GameInfo& game : m_games) {
        if (game.title().toLower().contains(lowerQuery) ||
            game.description().toLower().contains(lowerQuery) ||
            game.tags().join(" ").toLower().contains(lowerQuery)) {
            results.append(game);
        }
    }
    
    return results;
}

QList<GameInfo> GameLibrary::getFavoriteGames() const
{
    QMutexLocker locker(&m_gamesMutex);
    
    QList<GameInfo> favorites;
    for (const GameInfo& game : m_games) {
        if (game.isFavorite()) {
            favorites.append(game);
        }
    }
    
    return favorites;
}

QList<GameInfo> GameLibrary::getRecentlyPlayedGames(int maxCount) const
{
    QMutexLocker locker(&m_gamesMutex);
    
    QList<GameInfo> games = m_games;
    
    // Sort by last played time (most recent first)
    std::sort(games.begin(), games.end(), [](const GameInfo& a, const GameInfo& b) {
        return a.lastPlayed() > b.lastPlayed();
    });
    
    // Filter out games that have never been played and limit count
    QList<GameInfo> recent;
    for (const GameInfo& game : games) {
        if (game.lastPlayed().isValid()) {
            recent.append(game);
            if (recent.size() >= maxCount) {
                break;
            }
        }
    }
    
    return recent;
}

GameLibrary::LibraryStats GameLibrary::getLibraryStats() const
{
    QMutexLocker locker(&m_gamesMutex);
    
    LibraryStats stats;
    stats.totalGames = m_games.size();
    
    for (const GameInfo& game : m_games) {
        if (game.isInstalled()) {
            stats.installedGames++;
        }
        
        if (game.isFavorite()) {
            stats.favoriteGames++;
        }
        
        stats.totalPlaytime += game.playtimeMinutes();
        stats.totalSize += game.sizeBytes();
        
        // Count by platform
        GamePlatform platform = game.platform();
        stats.gamesByPlatform[platform]++;
    }
    
    return stats;
}

bool GameLibrary::saveLibrary()
{
    qInfo(gameLibrary) << "Saving game library";
    
    QMutexLocker locker(&m_gamesMutex);
    
    // The individual games are already saved to database
    // This could save additional library metadata if needed
    
    return true;
}

bool GameLibrary::loadLibrary()
{
    qInfo(gameLibrary) << "Loading game library";
    return loadGamesFromDatabase();
}

void GameLibrary::exportLibrary(const QString& filePath) const
{
    QMutexLocker locker(&m_gamesMutex);
    
    QJsonArray gamesArray;
    for (const GameInfo& game : m_games) {
        gamesArray.append(game.toJson());
    }
    
    QJsonObject rootObj;
    rootObj["version"] = "1.0";
    rootObj["exportDate"] = QDateTime::currentDateTime().toString(Qt::ISODate);
    rootObj["gameCount"] = m_games.size();
    rootObj["games"] = gamesArray;
    
    QJsonDocument doc(rootObj);
    
    QFile file(filePath);
    if (file.open(QIODevice::WriteOnly)) {
        file.write(doc.toJson());
        qInfo(gameLibrary) << "Exported library to:" << filePath;
    } else {
        qWarning(gameLibrary) << "Failed to export library to:" << filePath;
    }
}

bool GameLibrary::importLibrary(const QString& filePath)
{
    QFile file(filePath);
    if (!file.open(QIODevice::ReadOnly)) {
        qWarning(gameLibrary) << "Failed to open import file:" << filePath;
        return false;
    }
    
    QJsonDocument doc = QJsonDocument::fromJson(file.readAll());
    if (doc.isNull()) {
        qWarning(gameLibrary) << "Invalid JSON in import file:" << filePath;
        return false;
    }
    
    QJsonObject rootObj = doc.object();
    QJsonArray gamesArray = rootObj["games"].toArray();
    
    int importedCount = 0;
    for (const QJsonValue& gameValue : gamesArray) {
        GameInfo game;
        game.fromJson(gameValue.toObject());
        
        if (game.isValid() && !isGameDuplicate(game)) {
            if (addGame(game)) {
                importedCount++;
            }
        }
    }
    
    qInfo(gameLibrary) << "Imported" << importedCount << "games from:" << filePath;
    return importedCount > 0;
}

void GameLibrary::refreshGame(const QString& gameId, GamePlatform platform)
{
    GameInfo* game = findGame(gameId, platform);
    if (!game) {
        qWarning(gameLibrary) << "Game not found for refresh:" << gameId;
        return;
    }
    
    // Mark for refresh - could trigger platform-specific refresh logic
    emit gameUpdated(*game);
}

void GameLibrary::refreshAllGames()
{
    qInfo(gameLibrary) << "Refreshing all games";
    startQuickScan();
}

void GameLibrary::updateGamePlaytime(const QString& gameId, GamePlatform platform, qint64 minutes)
{
    GameInfo* game = findGame(gameId, platform);
    if (!game) {
        return;
    }
    
    game->setPlaytimeMinutes(game->playtimeMinutes() + minutes);
    game->setLastPlayed(QDateTime::currentDateTime());
    
    updateGameInDatabase(*game);
    emit gameUpdated(*game);
    
    qDebug(gameLibrary) << "Updated playtime for" << game->title() << "+" << minutes << "minutes";
}

void GameLibrary::setGameFavorite(const QString& gameId, GamePlatform platform, bool favorite)
{
    GameInfo* game = findGame(gameId, platform);
    if (!game) {
        return;
    }
    
    game->setFavorite(favorite);
    updateGameInDatabase(*game);
    emit gameUpdated(*game);
    
    qDebug(gameLibrary) << "Set favorite for" << game->title() << ":" << favorite;
}

// Private slots implementation

void GameLibrary::onDetectorFinished(const QList<GameInfo>& games)
{
    processDetectedGames(games);
}

void GameLibrary::onDetectorError(const QString& error)
{
    qWarning(gameLibrary) << "Detector error:" << error;
    emit scanError(error);
}

void GameLibrary::onScanTimerTimeout()
{
    if (m_autoScanOnStartup && !m_isScanning) {
        startQuickScan();
    }
}

// Private methods implementation

void GameLibrary::initializeDetectors()
{
    // Create platform detectors
    m_detectors.append(new SteamDetector(this));
    m_detectors.append(new EpicDetector(this));
    m_detectors.append(new GOGDetector(this));
    m_detectors.append(new MinecraftDetector(this));
    
    // Connect detector signals
    for (PlatformDetector* detector : m_detectors) {
        connect(detector, &PlatformDetector::detectionFinished,
                this, &GameLibrary::onDetectorFinished);
        connect(detector, &PlatformDetector::error,
                this, &GameLibrary::onDetectorError);
    }
    
    qInfo(gameLibrary) << "Initialized" << m_detectors.size() << "platform detectors";
}

void GameLibrary::runDetection(bool fullScan)
{
    setScanning(true);
    
    QList<GameInfo> allDetectedGames;
    int totalDetectors = 0;
    int currentDetector = 0;
    
    // Count available detectors
    for (PlatformDetector* detector : m_detectors) {
        if (detector->isAvailable()) {
            totalDetectors++;
        }
    }
    
    // Run detection on each available platform
    for (PlatformDetector* detector : m_detectors) {
        if (!detector->isAvailable()) {
            qDebug(gameLibrary) << "Skipping unavailable detector:" << detector->platformName();
            continue;
        }
        
        emit scanProgress(currentDetector++, totalDetectors, 
                         QString("Scanning %1").arg(detector->platformName()));
        
        qInfo(gameLibrary) << "Running detection for:" << detector->platformName();
        
        try {
            QList<GameInfo> platformGames = detector->detectGames();
            allDetectedGames.append(platformGames);
            
            qInfo(gameLibrary) << "Found" << platformGames.size() 
                              << "games on" << detector->platformName();
        } catch (...) {
            qWarning(gameLibrary) << "Exception during detection for:" << detector->platformName();
        }
    }
    
    // Process all detected games
    processDetectedGames(allDetectedGames);
    
    setScanning(false);
    updateLastScanTime();
    
    int newGames = 0; // Could track actual new games added
    emit scanFinished(allDetectedGames.size(), newGames);
    
    qInfo(gameLibrary) << "Detection complete. Found" << allDetectedGames.size() << "total games";
}

void GameLibrary::processDetectedGames(const QList<GameInfo>& newGames)
{
    int addedCount = 0;
    int updatedCount = 0;
    
    for (const GameInfo& detectedGame : newGames) {
        if (!detectedGame.isValid()) {
            continue;
        }
        
        GameInfo* existing = findGame(detectedGame.gameId(), detectedGame.platform());
        
        if (existing) {
            // Update existing game with new information
            mergeGameInfo(*existing, detectedGame);
            updateGameInDatabase(*existing);
            emit gameUpdated(*existing);
            updatedCount++;
        } else {
            // Add new game
            if (addGame(detectedGame)) {
                addedCount++;
            }
        }
    }
    
    qInfo(gameLibrary) << "Processed detected games - Added:" << addedCount << "Updated:" << updatedCount;
}

bool GameLibrary::initializeDatabase()
{
    QString dataPath = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation);
    QDir().mkpath(dataPath);
    
    m_databasePath = QDir(dataPath).filePath("mystical_library.db");
    
    m_database = QSqlDatabase::addDatabase("QSQLITE");
    m_database.setDatabaseName(m_databasePath);
    
    if (!m_database.open()) {
        qCritical(gameLibrary) << "Failed to open database:" << m_database.lastError().text();
        return false;
    }
    
    if (!createTables()) {
        qCritical(gameLibrary) << "Failed to create database tables";
        return false;
    }
    
    qInfo(gameLibrary) << "Database initialized at:" << m_databasePath;
    return true;
}

bool GameLibrary::createTables()
{
    QSqlQuery query(m_database);
    
    QString createGamesTable = R"(
        CREATE TABLE IF NOT EXISTS games (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            game_id TEXT NOT NULL,
            platform INTEGER NOT NULL,
            title TEXT NOT NULL,
            description TEXT,
            install_path TEXT,
            cover_art_path TEXT,
            cover_art_url TEXT,
            last_played TEXT,
            playtime_minutes INTEGER DEFAULT 0,
            install_date TEXT,
            size_bytes INTEGER DEFAULT 0,
            is_installed BOOLEAN DEFAULT 0,
            is_favorite BOOLEAN DEFAULT 0,
            tags TEXT,
            launch_config TEXT,
            created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
            updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
            UNIQUE(game_id, platform)
        )
    )";
    
    if (!query.exec(createGamesTable)) {
        qCritical(gameLibrary) << "Failed to create games table:" << query.lastError().text();
        return false;
    }
    
    // Create indexes for better performance
    query.exec("CREATE INDEX IF NOT EXISTS idx_games_platform ON games(platform)");
    query.exec("CREATE INDEX IF NOT EXISTS idx_games_title ON games(title)");
    query.exec("CREATE INDEX IF NOT EXISTS idx_games_last_played ON games(last_played)");
    query.exec("CREATE INDEX IF NOT EXISTS idx_games_favorite ON games(is_favorite)");
    
    return true;
}

bool GameLibrary::loadGamesFromDatabase()
{
    QSqlQuery query(m_database);
    query.prepare("SELECT * FROM games ORDER BY title");
    
    if (!query.exec()) {
        qWarning(gameLibrary) << "Failed to load games from database:" << query.lastError().text();
        return false;
    }
    
    QMutexLocker locker(&m_gamesMutex);
    m_games.clear();
    
    while (query.next()) {
        GameInfo game;
        
        // Load basic fields
        game.setGameId(query.value("game_id").toString());
        game.setPlatform(static_cast<GamePlatform>(query.value("platform").toInt()));
        game.setTitle(query.value("title").toString());
        game.setDescription(query.value("description").toString());
        game.setInstallPath(query.value("install_path").toString());
        game.setCoverArtPath(query.value("cover_art_path").toString());
        game.setCoverArtUrl(QUrl(query.value("cover_art_url").toString()));
        
        // Load dates
        game.setLastPlayed(QDateTime::fromString(query.value("last_played").toString(), Qt::ISODate));
        game.setInstallDate(QDateTime::fromString(query.value("install_date").toString(), Qt::ISODate));
        
        // Load numeric fields
        game.setPlaytimeMinutes(query.value("playtime_minutes").toLongLong());
        game.setSizeBytes(query.value("size_bytes").toLongLong());
        
        // Load boolean fields
        game.setInstalled(query.value("is_installed").toBool());
        game.setFavorite(query.value("is_favorite").toBool());
        
        // Load tags
        QString tagsString = query.value("tags").toString();
        if (!tagsString.isEmpty()) {
            game.setTags(tagsString.split(",", Qt::SkipEmptyParts));
        }
        
        // Load launch config
        QString launchConfigJson = query.value("launch_config").toString();
        if (!launchConfigJson.isEmpty()) {
            QJsonDocument doc = QJsonDocument::fromJson(launchConfigJson.toUtf8());
            if (!doc.isNull()) {
                LaunchConfig config;
                config.fromJson(doc.object());
                game.setLaunchConfig(config);
            }
        }
        
        m_games.append(game);
    }
    
    locker.unlock();
    
    emit gameCountChanged();
    emit libraryDataChanged();
    
    qInfo(gameLibrary) << "Loaded" << m_games.size() << "games from database";
    return true;
}

bool GameLibrary::saveGameToDatabase(const GameInfo& game)
{
    QSqlQuery query(m_database);
    
    query.prepare(R"(
        INSERT OR REPLACE INTO games (
            game_id, platform, title, description, install_path,
            cover_art_path, cover_art_url, last_played, playtime_minutes,
            install_date, size_bytes, is_installed, is_favorite,
            tags, launch_config, updated_at
        ) VALUES (
            ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, CURRENT_TIMESTAMP
        )
    )");
    
    query.addBindValue(game.gameId());
    query.addBindValue(static_cast<int>(game.platform()));
    query.addBindValue(game.title());
    query.addBindValue(game.description());
    query.addBindValue(game.installPath());
    query.addBindValue(game.coverArtPath());
    query.addBindValue(game.coverArtUrl().toString());
    query.addBindValue(game.lastPlayed().toString(Qt::ISODate));
    query.addBindValue(game.playtimeMinutes());
    query.addBindValue(game.installDate().toString(Qt::ISODate));
    query.addBindValue(game.sizeBytes());
    query.addBindValue(game.isInstalled());
    query.addBindValue(game.isFavorite());
    query.addBindValue(game.tags().join(","));
    
    QJsonDocument launchConfigDoc(game.launchConfig().toJson());
    query.addBindValue(launchConfigDoc.toJson(QJsonDocument::Compact));
    
    if (!query.exec()) {
        qWarning(gameLibrary) << "Failed to save game to database:" << query.lastError().text();
        return false;
    }
    
    return true;
}

bool GameLibrary::updateGameInDatabase(const GameInfo& game)
{
    return saveGameToDatabase(game); // INSERT OR REPLACE handles updates
}

bool GameLibrary::deleteGameFromDatabase(const QString& gameId, GamePlatform platform)
{
    QSqlQuery query(m_database);
    query.prepare("DELETE FROM games WHERE game_id = ? AND platform = ?");
    query.addBindValue(gameId);
    query.addBindValue(static_cast<int>(platform));
    
    if (!query.exec()) {
        qWarning(gameLibrary) << "Failed to delete game from database:" << query.lastError().text();
        return false;
    }
    
    return true;
}

GameInfo* GameLibrary::findGame(const QString& gameId, GamePlatform platform)
{
    auto it = std::find_if(m_games.begin(), m_games.end(),
        [&](const GameInfo& game) {
            return game.gameId() == gameId && game.platform() == platform;
        });
    
    return (it != m_games.end()) ? &(*it) : nullptr;
}

const GameInfo* GameLibrary::findGame(const QString& gameId, GamePlatform platform) const
{
    auto it = std::find_if(m_games.begin(), m_games.end(),
        [&](const GameInfo& game) {
            return game.gameId() == gameId && game.platform() == platform;
        });
    
    return (it != m_games.end()) ? &(*it) : nullptr;
}

bool GameLibrary::isGameDuplicate(const GameInfo& game) const
{
    return findGame(game.gameId(), game.platform()) != nullptr;
}

void GameLibrary::mergeGameInfo(GameInfo& existing, const GameInfo& detected)
{
    // Update installation status and path
    existing.setInstalled(detected.isInstalled());
    if (!detected.installPath().isEmpty()) {
        existing.setInstallPath(detected.installPath());
    }
    
    // Update size if detected
    if (detected.sizeBytes() > 0) {
        existing.setSizeBytes(detected.sizeBytes());
    }
    
    // Update launch config
    existing.setLaunchConfig(detected.launchConfig());
    
    // Update cover art URL if available
    if (!detected.coverArtUrl().isEmpty()) {
        existing.setCoverArtUrl(detected.coverArtUrl());
    }
    
    // Preserve user data (favorites, custom tags, etc.)
    // These are not overwritten by detection
}

void GameLibrary::setScanning(bool scanning)
{
    if (m_isScanning != scanning) {
        m_isScanning = scanning;
        emit scanningChanged();
    }
}

void GameLibrary::setInitializing(bool initializing)
{
    if (m_isInitializing != initializing) {
        m_isInitializing = initializing;
        emit initializingChanged();
    }
}

void GameLibrary::updateLastScanTime()
{
    m_lastScanTime = QDateTime::currentDateTime();
    emit lastScanTimeChanged();
}