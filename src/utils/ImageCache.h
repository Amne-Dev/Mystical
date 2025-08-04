#pragma once

#include <QObject>
#include <QPixmap>
#include <QUrl>
#include <QNetworkAccessManager>
#include <QCache>
#include <QTimer>

class QNetworkReply;

class ImageCache : public QObject
{
    Q_OBJECT
    
public:
    explicit ImageCache(QObject* parent = nullptr);
    ~ImageCache();
    
    // Cache operations
    QPixmap getImage(const QString& key);
    QPixmap getImage(const QUrl& url);
    void preloadImage(const QString& key, const QUrl& url);
    void clearCache();
    void clearExpired();
    
    // Configuration
    void setMaxCacheSize(int maxSizeKB);
    void setCacheDirectory(const QString& directory);
    void setMaxAge(int maxAgeHours);
    void setNetworkTimeout(int timeoutMs);
    
    // Statistics
    int cacheSize() const;
    int cacheCount() const;
    qreal hitRatio() const;
    
signals:
    void imageLoaded(const QString& key, const QPixmap& pixmap);
    void imageLoadFailed(const QString& key, const QString& error);
    
private slots:
    void onNetworkReplyFinished();
    void onCleanupTimer();
    
private:
    struct CacheEntry {
        QPixmap pixmap;
        QDateTime timestamp;
        qint64 size;
    };
    
    // Helper methods
    QString urlToKey(const QUrl& url) const;
    QString getCacheFilePath(const QString& key) const;
    bool loadFromDisk(const QString& key, QPixmap& pixmap);
    bool saveToDisk(const QString& key, const QPixmap& pixmap);
    void downloadImage(const QString& key, const QUrl& url);
    void cleanupOldEntries();
    int calculatePixmapSize(const QPixmap& pixmap) const;
    
    // Member variables
    QCache<QString, CacheEntry> m_memoryCache;
    QNetworkAccessManager* m_networkManager;
    QTimer* m_cleanupTimer;
    
    QString m_cacheDirectory;
    int m_maxCacheSizeKB;
    int m_maxAgeHours;
    int m_networkTimeoutMs;
    
    // Statistics
    mutable int m_hits;
    mutable int m_misses;
    
    // Pending downloads
    QHash<QNetworkReply*, QString> m_pendingDownloads;
};
