#include "GameLibraryModel.h"
#include "../core/GameLibrary.h"
#include <QDebug>
#include <QLocale>
#include <QDateTime>

GameLibraryModel::GameLibraryModel(GameLibrary* gameLibrary, QObject* parent)
    : QAbstractListModel(parent)
    , m_gameLibrary(gameLibrary)
    , m_filterTimer(new QTimer(this))
{
    // Setup filter timer for performance
    m_filterTimer->setSingleShot(true);
    m_filterTimer->setInterval(300); // 300ms delay
    connect(m_filterTimer, &QTimer::timeout, this, &GameLibraryModel::onFilterTimerTimeout);
    
    // Connect to library signals
    connect(m_gameLibrary, &GameLibrary::gameAdded, this, &GameLibraryModel::onGameAdded);
    connect(m_gameLibrary, &GameLibrary::gameUpdated, this, &GameLibraryModel::onGameUpdated);
    connect(m_gameLibrary, &GameLibrary::gameRemoved, this, &GameLibraryModel::onGameRemoved);
    connect(m_gameLibrary, &GameLibrary::libraryDataChanged, this, &GameLibraryModel::onLibraryDataChanged);
    
    // Initial load
    updateFilteredGames();
}

int GameLibraryModel::rowCount(const QModelIndex& parent) const
{
    Q_UNUSED(parent)
    return m_filteredGames.size();
}

QVariant GameLibraryModel::data(const QModelIndex& index, int role) const
{
    if (!index.isValid() || index.row() < 0 || index.row() >= m_filteredGames.size()) {
        return QVariant();
    }
    
    const GameInfo& game = m_filteredGames[index.row()];
    
    switch (role) {
        case TitleRole:
            return game.title();
        case PlatformRole:
            return static_cast<int>(game.platform());
        case GameIdRole:
            return game.gameId();
        case CoverArtRole:
            return game.coverArtPath();
        case CoverArtUrlRole:
            return game.coverArtUrl().toString();
        case PlaytimeRole:
            return game.playtimeMinutes();
        case FormattedPlaytimeRole:
            return formatPlaytime(game.playtimeMinutes());
        case LastPlayedRole:
            return game.lastPlayed();
        case IsFavoriteRole:
            return game.isFavorite();
        case IsInstalledRole:
            return game.isInstalled();
        case InstallPathRole:
            return game.installPath();
        case SizeRole:
            return game.sizeBytes();
        case FormattedSizeRole:
            return formatSize(game.sizeBytes());
        case TagsRole:
            return game.tags();
        case DescriptionRole:
            return game.description();
        case PlatformStringRole:
            return game.platformString();
        default:
            return QVariant();
    }
}

QHash<int, QByteArray> GameLibraryModel::roleNames() const
{
    QHash<int, QByteArray> roles;
    roles[TitleRole] = "title";
    roles[PlatformRole] = "platform";
    roles[GameIdRole] = "gameId";
    roles[CoverArtRole] = "coverArt";
    roles[CoverArtUrlRole] = "coverArtUrl";
    roles[PlaytimeRole] = "playtime";
    roles[FormattedPlaytimeRole] = "formattedPlaytime";
    roles[LastPlayedRole] = "lastPlayed";
    roles[IsFavoriteRole] = "isFavorite";
    roles[IsInstalledRole] = "isInstalled";
    roles[InstallPathRole] = "installPath";
    roles[SizeRole] = "size";
    roles[FormattedSizeRole] = "formattedSize";
    roles[TagsRole] = "tags";
    roles[DescriptionRole] = "description";
    roles[PlatformStringRole] = "platformString";
    return roles;
}

void GameLibraryModel::setSortField(const QString& field)
{
    if (m_sortField != field) {
        m_sortField = field;
        emit sortFieldChanged();
        invalidateSort();
    }
}

void GameLibraryModel::setSortAscending(bool ascending)
{
    if (m_sortAscending != ascending) {
        m_sortAscending = ascending;
        emit sortAscendingChanged();
        invalidateSort();
    }
}

QVariant GameLibraryModel::getGame(int index) const
{
    if (index < 0 || index >= m_filteredGames.size()) {
        return QVariant();
    }
    
    const GameInfo& game = m_filteredGames[index];
    QVariantMap gameMap;
    
    gameMap["title"] = game.title();
    gameMap["platform"] = static_cast<int>(game.platform());
    gameMap["gameId"] = game.gameId();
    gameMap["coverArt"] = game.coverArtPath();
    gameMap["coverArtUrl"] = game.coverArtUrl().toString();
    gameMap["playtime"] = game.playtimeMinutes();
    gameMap["formattedPlaytime"] = formatPlaytime(game.playtimeMinutes());
    gameMap["lastPlayed"] = game.lastPlayed();
    gameMap["isFavorite"] = game.isFavorite();
    gameMap["isInstalled"] = game.isInstalled();
    gameMap["installPath"] = game.installPath();
    gameMap["size"] = game.sizeBytes();
    gameMap["formattedSize"] = formatSize(game.sizeBytes());
    gameMap["tags"] = game.tags();
    gameMap["description"] = game.description();
    gameMap["platformString"] = game.platformString();
    
    return gameMap;
}

bool GameLibraryModel::setGameFavorite(int index, bool favorite)
{
    if (index < 0 || index >= m_filteredGames.size()) {
        return false;
    }
    
    const GameInfo& game = m_filteredGames[index];
    m_gameLibrary->setGameFavorite(game.gameId(), game.platform(), favorite);
    return true;
}

bool GameLibraryModel::removeGame(int index)
{
    if (index < 0 || index >= m_filteredGames.size()) {
        return false;
    }
    
    const GameInfo& game = m_filteredGames[index];
    return m_gameLibrary->removeGame(game.gameId(), game.platform());
}

int GameLibraryModel::findGameIndex(const QString& gameId, const QString& platform) const
{
    GamePlatform platformEnum = GameInfo::platformFromString(platform);
    return findFilteredIndex(gameId, platformEnum);
}

QString GameLibraryModel::formatPlaytime(qint64 minutes) const
{
    // Use cache for performance
    QString key = QString::number(minutes);
    if (m_formattedPlaytimeCache.contains(key)) {
        return m_formattedPlaytimeCache[key];
    }
    
    QString formatted;
    if (minutes == 0) {
        formatted = "Never played";
    } else {
        qint64 hours = minutes / 60;
        qint64 remainingMinutes = minutes % 60;
        
        if (hours == 0) {
            formatted = QString("%1 min").arg(remainingMinutes);
        } else if (hours < 24) {
            formatted = QString("%1h %2m").arg(hours).arg(remainingMinutes);
        } else {
            qint64 days = hours / 24;
            hours = hours % 24;
            formatted = QString("%1d %2h").arg(days).arg(hours);
        }
    }
    
    m_formattedPlaytimeCache[key] = formatted;
    return formatted;
}

QString GameLibraryModel::formatSize(qint64 bytes) const
{
    // Use cache for performance
    if (m_formattedSizeCache.contains(bytes)) {
        return m_formattedSizeCache[bytes];
    }
    
    QString formatted;
    if (bytes == 0) {
        formatted = "Unknown";
    } else {
        QLocale locale;
        const qint64 kb = 1024;
        const qint64 mb = kb * 1024;
        const qint64 gb = mb * 1024;
        
        if (bytes >= gb) {
            formatted = QString("%1 GB").arg(locale.toString(bytes / (double)gb, 'f', 1));
        } else if (bytes >= mb) {
            formatted = QString("%1 MB").arg(locale.toString(bytes / (double)mb, 'f', 1));
        } else if (bytes >= kb) {
            formatted = QString("%1 KB").arg(locale.toString(bytes / (double)kb, 'f', 0));
        } else {
            formatted = QString("%1 bytes").arg(locale.toString(bytes));
        }
    }
    
    m_formattedSizeCache[bytes] = formatted;
    return formatted;
}

QString GameLibraryModel::getPlatformDisplayName(const QString& platform) const
{
    return GameInfo::platformFromString(platform) != GamePlatform::Unknown ? platform : "Unknown";
}

void GameLibraryModel::refresh()
{
    beginModelReset();
    updateFilteredGames();
    endModelReset();
}

void GameLibraryModel::invalidateFilter()
{
    m_filterDirty = true;
    m_filterTimer->start();
}

void GameLibraryModel::invalidateSort()
{
    beginModelReset();
    applySorting();
    endModelReset();
}

// Private slots implementation

void GameLibraryModel::onGameAdded(const GameInfo& game)
{
    if (passesFilter(game)) {
        beginInsertRows(QModelIndex(), m_filteredGames.size(), m_filteredGames.size());
        m_filteredGames.append(game);
        applySorting();
        endInsertRows();
        emit countChanged();
    }
}

void GameLibraryModel::onGameUpdated(const GameInfo& game)
{
    int index = findFilteredIndex(game.gameId(), game.platform());
    
    if (index >= 0) {
        // Game was already in filtered list
        if (passesFilter(game)) {
            // Still passes filter, update in place
            m_filteredGames[index] = game;
            QModelIndex modelIndex = createIndex(index, 0);
            emit dataChanged(modelIndex, modelIndex);
        } else {
            // No longer passes filter, remove
            beginRemoveRows(QModelIndex(), index, index);
            m_filteredGames.removeAt(index);
            endRemoveRows();
            emit countChanged();
        }
    } else {
        // Game was not in filtered list
        if (passesFilter(game)) {
            // Now passes filter, add it
            beginInsertRows(QModelIndex(), m_filteredGames.size(), m_filteredGames.size());
            m_filteredGames.append(game);
            applySorting();
            endInsertRows();
            emit countChanged();
        }
    }
    
    // Clear caches as data may have changed
    m_formattedPlaytimeCache.clear();
    m_formattedSizeCache.clear();
}

void GameLibraryModel::onGameRemoved(const QString& gameId, GamePlatform platform)
{
    int index = findFilteredIndex(gameId, platform);
    if (index >= 0) {
        beginRemoveRows(QModelIndex(), index, index);
        m_filteredGames.removeAt(index);
        endRemoveRows();
        emit countChanged();
    }
}

void GameLibraryModel::onLibraryDataChanged()
{
    refresh();
}

void GameLibraryModel::onFilterTimerTimeout()
{
    if (m_filterDirty) {
        m_filterDirty = false;
        beginModelReset();
        updateFilteredGames();
        endModelReset();
    }
}

// Private methods implementation

void GameLibraryModel::updateFilteredGames()
{
    m_filteredGames.clear();
    
    QList<GameInfo> allGames = m_gameLibrary->getAllGames();
    
    for (const GameInfo& game : allGames) {
        if (passesFilter(game)) {
            m_filteredGames.append(game);
        }
    }
    
    applySorting();
    emit countChanged();
}

void GameLibraryModel::applySorting()
{
    std::sort(m_filteredGames.begin(), m_filteredGames.end(), 
              [this](const GameInfo& a, const GameInfo& b) {
                  return compareGames(a, b);
              });
}

bool GameLibraryModel::passesFilter(const GameInfo& game) const
{
    // Text filter
    if (!m_filterText.isEmpty()) {
        QString lowerText = m_filterText.toLower();
        if (!game.title().toLower().contains(lowerText) &&
            !game.description().toLower().contains(lowerText) &&
            !game.tags().join(" ").toLower().contains(lowerText)) {
            return false;
        }
    }
    
    // Platform filter
    if (!m_filterPlatform.isEmpty() && m_filterPlatform != "All") {
        if (game.platformString() != m_filterPlatform) {
            return false;
        }
    }
    
    // Favorites filter
    if (m_filterFavorites && !game.isFavorite()) {
        return false;
    }
    
    // Recent filter (played in last 30 days)
    if (m_filterRecent) {
        if (!game.lastPlayed().isValid() ||
            game.lastPlayed().daysTo(QDateTime::currentDateTime()) > 30) {
            return false;
        }
    }
    
    return true;
}

bool GameLibraryModel::compareGames(const GameInfo& a, const GameInfo& b) const
{
    bool result = false;
    
    if (m_sortField == "title") {
        result = a.title().compare(b.title(), Qt::CaseInsensitive) < 0;
    } else if (m_sortField == "platform") {
        result = a.platformString().compare(b.platformString(), Qt::CaseInsensitive) < 0;
    } else if (m_sortField == "playtime") {
        result = a.playtimeMinutes() < b.playtimeMinutes();
    } else if (m_sortField == "lastPlayed") {
        result = a.lastPlayed() < b.lastPlayed();
    } else if (m_sortField == "size") {
        result = a.sizeBytes() < b.sizeBytes();
    } else if (m_sortField == "installDate") {
        result = a.installDate() < b.installDate();
    } else {
        // Default to title sorting
        result = a.title().compare(b.title(), Qt::CaseInsensitive) < 0;
    }
    
    return m_sortAscending ? result : !result;
}

GameInfo* GameLibraryModel::getGameAt(int index) const
{
    if (index < 0 || index >= m_filteredGames.size()) {
        return nullptr;
    }
    
    // Return pointer to the game in the main library
    const GameInfo& filteredGame = m_filteredGames[index];
    QList<GameInfo> allGames = m_gameLibrary->getAllGames();
    
    for (GameInfo& game : allGames) {
        if (game.gameId() == filteredGame.gameId() && game.platform() == filteredGame.platform()) {
            return &game;
        }
    }
    
    return nullptr;
}

int GameLibraryModel::findFilteredIndex(const QString& gameId, GamePlatform platform) const
{
    for (int i = 0; i < m_filteredGames.size(); ++i) {
        const GameInfo& game = m_filteredGames[i];
        if (game.gameId() == gameId && game.platform() == platform) {
            return i;
        }
    }
    return -1;
}

void GameLibraryModel::beginModelReset()
{
    beginResetModel();
}

void GameLibraryModel::endModelReset()
{
    endResetModel();
}

void GameLibraryModel::setFilterText(const QString& text)
{
    if (m_filterText != text) {
        m_filterText = text;
        emit filterTextChanged();
        invalidateFilter();
    }
}

void GameLibraryModel::setFilterPlatform(const QString& platform)
{
    if (m_filterPlatform != platform) {
        m_filterPlatform = platform;
        emit filterPlatformChanged();
        invalidateFilter();
    }
}

void GameLibraryModel::setFilterFavorites(bool favorites)
{
    if (m_filterFavorites != favorites) {
        m_filterFavorites = favorites;
        emit filterFavoritesChanged();
        invalidateFilter();
    }
}

void GameLibraryModel::setFilterRecent(bool recent)
{
    if (m_filterRecent != recent) {
        m_filterRecent = recent;
        emit filterRecentChanged();
        invalidateFilter();
    }
}