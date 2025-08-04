#pragma once

#include <QObject>
#include <QList>
#include <QTimer>
#include <QFuture>
#include <QMutex>
#include <QSqlDatabase>
#include "GameInfo.h"

class PlatformDetector;
class GameLauncher;

class GameLibrary : public QObject
{
    Q_OBJECT
    Q_PROPERTY(bool isScanning READ isScanning NOTIFY scanningChanged)
    Q_PROPERTY(bool isInitializing READ isInitializing NOTIFY initializingChanged)
    Q_PROPERTY(int gameCount READ gameCount NOTIFY gameCountChanged)
    Q_PROPERTY(QString lastScanTime READ lastScanTime NOTIFY lastScanTimeChanged)
    
public:
    explicit GameLibrary(QObject* parent = nullptr);
    ~GameLibrary();
    
    // Properties
    bool isScanning() const { return m_isScanning; }
    bool isInitializing() const { return m_isInitializing; }
    int gameCount() const { return m_games.size(); }
    QString lastScanTime() const;
    
    // Game management
    QList<GameInfo> getAllGames() const;
    GameInfo getGame(const QString& gameId, GamePlatform platform) const;
    bool addGame(const GameInfo& game);
    bool updateGame(const GameInfo& game);
    bool removeGame(const QString& gameId, GamePlatform platform);
    
    // Game operations
    Q_INVOKABLE bool launchGame(const QString& gameId, GamePlatform platform);
    Q_INVOKABLE bool launchGameByIndex(int index);
    
    // Library management
    Q_INVOKABLE void startInitialScan();
    Q_INVOKABLE void startQuickScan();
    Q_INVOKABLE void startFullScan();
    Q_INVOKABLE void stopScan();
    
    // Filtering and searching
    QList<GameInfo> getGamesByPlatform(GamePlatform platform) const;
    QList<GameInfo> searchGames(const QString& query) const;
    QList<GameInfo> getFavoriteGames() const;
    QList<GameInfo> getRecentlyPlayedGames(int maxCount = 10) const;
    
    // Statistics
    struct LibraryStats {
        int totalGames = 0;
        int installedGames = 0;
        int favoriteGames = 0;
        qint64 totalPlaytime = 0;
        qint64 totalSize = 0;
        QMap<GamePlatform, int> gamesByPlatform;
    };
    
    LibraryStats getLibraryStats() const;
    
    // Data persistence
    bool saveLibrary();
    bool loadLibrary();
    void exportLibrary(const QString& filePath) const;
    bool importLibrary(const QString& filePath);
    
public slots:
    void refreshGame(const QString& gameId, GamePlatform platform);
    void refreshAllGames();
    void updateGamePlaytime(const QString& gameId, GamePlatform platform, qint64 minutes);
    void setGameFavorite(const QString& gameId, GamePlatform platform, bool favorite);
    
signals:
    void scanningChanged();
    void initializingChanged();
    void gameCountChanged();
    void lastScanTimeChanged();
    
    void gameAdded(const GameInfo& game);
    void gameUpdated(const GameInfo& game);
    void gameRemoved(const QString& gameId, GamePlatform platform);
    
    void scanStarted(const QString& scanType);
    void scanProgress(int current, int total, const QString& currentOperation);
    void scanFinished(int gamesFound, int newGames);
    void scanError(const QString& error);
    
    void gameLatched(const GameInfo& game);
    void gameLaunchFailed(const GameInfo& game, const QString& error);
    
    void errorOccurred(const QString& error);
    void libraryDataChanged();
    
private slots:
    void onDetectorFinished(const QList<GameInfo>& games);
    void onDetectorError(const QString& error);
    void onScanTimerTimeout();
    
private:
    // Detection management
    void initializeDetectors();
    void runDetection(bool fullScan = false);
    void processDetectedGames(const QList<GameInfo>& newGames);
    
    // Database operations
    bool initializeDatabase();
    bool createTables();
    bool loadGamesFromDatabase();
    bool saveGameToDatabase(const GameInfo& game);
    bool updateGameInDatabase(const GameInfo& game);
    bool deleteGameFromDatabase(const QString& gameId, GamePlatform platform);
    
    // Utility methods
    GameInfo* findGame(const QString& gameId, GamePlatform platform);
    const GameInfo* findGame(const QString& gameId, GamePlatform platform) const;
    bool isGameDuplicate(const GameInfo& game) const;
    void mergeGameInfo(GameInfo& existing, const GameInfo& detected);
    
    // State management
    void setScanning(bool scanning);
    void setInitializing(bool initializing);
    void updateLastScanTime();
    
    // Member variables
    QList<GameInfo> m_games;
    QList<PlatformDetector*> m_detectors;
    GameLauncher* m_launcher;
    
    QSqlDatabase m_database;
    QString m_databasePath;
    
    mutable QMutex m_gamesMutex;
    QFuture<void> m_scanFuture;
    QTimer* m_scanTimer;
    
    bool m_isScanning = false;
    bool m_isInitializing = true;
    QDateTime m_lastScanTime;
    
    int m_scanProgress = 0;
    int m_scanTotal = 0;
    QString m_currentScanOperation;
    
    // Configuration
    bool m_autoScanOnStartup = true;
    int m_autoScanInterval = 300000; // 5 minutes
    bool m_detectNewGamesOnly = false;
};