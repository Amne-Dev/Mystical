#pragma once

#include <QAbstractListModel>
#include <QSortFilterProxyModel>
#include <QString>
#include <QTimer>
#include "../core/GameInfo.h"

class GameLibrary;

class GameLibraryModel : public QAbstractListModel
{
    Q_OBJECT
    Q_PROPERTY(int count READ rowCount NOTIFY countChanged)
    Q_PROPERTY(QString filterText READ filterText WRITE setFilterText NOTIFY filterTextChanged)
    Q_PROPERTY(QString filterPlatform READ filterPlatform WRITE setFilterPlatform NOTIFY filterPlatformChanged)
    Q_PROPERTY(bool filterFavorites READ filterFavorites WRITE setFilterFavorites NOTIFY filterFavoritesChanged)
    Q_PROPERTY(bool filterRecent READ filterRecent WRITE setFilterRecent NOTIFY filterRecentChanged)
    Q_PROPERTY(QString sortField READ sortField WRITE setSortField NOTIFY sortFieldChanged)
    Q_PROPERTY(bool sortAscending READ sortAscending WRITE setSortAscending NOTIFY sortAscendingChanged)
    
public:
    enum GameRoles {
        TitleRole = Qt::UserRole + 1,
        PlatformRole,
        GameIdRole,
        CoverArtRole,
        CoverArtUrlRole,
        PlaytimeRole,
        FormattedPlaytimeRole,
        LastPlayedRole,
        IsFavoriteRole,
        IsInstalledRole,
        InstallPathRole,
        SizeRole,
        FormattedSizeRole,
        TagsRole,
        DescriptionRole,
        PlatformStringRole
    };
    Q_ENUM(GameRoles)
    
    explicit GameLibraryModel(GameLibrary* gameLibrary, QObject* parent = nullptr);
    
    // QAbstractListModel interface
    int rowCount(const QModelIndex& parent = QModelIndex()) const override;
    QVariant data(const QModelIndex& index, int role = Qt::DisplayRole) const override;
    QHash<int, QByteArray> roleNames() const override;
    
    // Filtering properties
    QString filterText() const { return m_filterText; }
    void setFilterText(const QString& text);
    
    QString filterPlatform() const { return m_filterPlatform; }
    void setFilterPlatform(const QString& platform);
    
    bool filterFavorites() const { return m_filterFavorites; }
    void setFilterFavorites(bool favorites);
    
    bool filterRecent() const { return m_filterRecent; }
    void setFilterRecent(bool recent);
    
    // Sorting properties
    QString sortField() const { return m_sortField; }
    void setSortField(const QString& field);
    
    bool sortAscending() const { return m_sortAscending; }
    void setSortAscending(bool ascending);
    
    // Game operations (callable from QML)
    Q_INVOKABLE QVariant getGame(int index) const;
    Q_INVOKABLE bool setGameFavorite(int index, bool favorite);
    Q_INVOKABLE bool removeGame(int index);
    Q_INVOKABLE int findGameIndex(const QString& gameId, const QString& platform) const;
    
    // Utility methods
    Q_INVOKABLE QString formatPlaytime(qint64 minutes) const;
    Q_INVOKABLE QString formatSize(qint64 bytes) const;
    Q_INVOKABLE QString getPlatformDisplayName(const QString& platform) const;
    
public slots:
    void refresh();
    void invalidateFilter();
    void invalidateSort();
    
signals:
    void countChanged();
    void filterTextChanged();
    void filterPlatformChanged();
    void filterFavoritesChanged();
    void filterRecentChanged();
    void sortFieldChanged();
    void sortAscendingChanged();
    
private slots:
    void onGameAdded(const GameInfo& game);
    void onGameUpdated(const GameInfo& game);
    void onGameRemoved(const QString& gameId, GamePlatform platform);
    void onLibraryDataChanged();
    void onFilterTimerTimeout();
    
private:
    // Filtering and sorting
    void updateFilteredGames();
    void applySorting();
    bool passesFilter(const GameInfo& game) const;
    bool compareGames(const GameInfo& a, const GameInfo& b) const;
    
    // Helper methods
    GameInfo* getGameAt(int index) const;
    int findFilteredIndex(const QString& gameId, GamePlatform platform) const;
    void beginModelReset();
    void endModelReset();
    
    // Member variables
    GameLibrary* m_gameLibrary;
    QList<GameInfo> m_filteredGames;
    
    // Filter settings
    QString m_filterText;
    QString m_filterPlatform;
    bool m_filterFavorites = false;
    bool m_filterRecent = false;
    
    // Sort settings
    QString m_sortField = "title";
    bool m_sortAscending = true;
    
    // Performance optimization
    QTimer* m_filterTimer;
    bool m_filterDirty = false;
    
    // Cache for expensive operations
    mutable QHash<QString, QString> m_formattedPlaytimeCache;
    mutable QHash<qint64, QString> m_formattedSizeCache;
};