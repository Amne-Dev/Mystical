#include "ImageCache.h"
#include "FileUtils.h"
#include <QNetworkReply>
#include <QNetworkRequest>
#include <QStandardPaths>
#include <QDir>
#include <QCryptographicHash>
#include <QPixmap>
#include <QBuffer>
#include <QImageReader>
#include <QLoggingCategory>

Q_LOGGING_CATEGORY(imageCache, "mystical.imagecache")

ImageCache::ImageCache(QObject* parent)
    : QObject(parent)
    , m_memoryCache(1000) // Default: 1000 entries
    , m_networkManager(new QNetworkAccessManager(this))
    , m_cleanupTimer(new QTimer(this))
    , m_maxCacheSizeKB(50 * 1024) // 50 MB default
    , m_maxAgeHours(24 * 7) // 1 week default
    , m_networkTimeoutMs(10000) // 10 seconds
    , m_hits(0)
    , m_misses(0)
{
    // Set default cache directory
    QString appData = QStandardPaths::writableLocation(QStandardPaths::CacheLocation);
    m_cacheDirectory = QDir(appData).filePath("mystical/images");
    FileUtils::createDirectory(m_cacheDirectory);
    
    // Setup cleanup timer
    m_cleanupTimer->setInterval(60 * 60 * 1000); // 1 hour
    m_cleanupTimer->start();
    connect(m_cleanupTimer, &QTimer::timeout, this, &ImageCache::onCleanupTimer);
    
    // Configure memory cache
    m_memoryCache.setMaxCost(m_maxCacheSizeKB);
    
    qInfo(imageCache) << "ImageCache initialized with directory:" << m_cacheDirectory;
}

ImageCache::~ImageCache()
{
    clearCache();
}

QPixmap ImageCache::getImage(const QString& key)
{
    // Check memory cache first
    CacheEntry* entry = m_memoryCache.object(key);
    if (entry) {
        m_hits++;
        return entry->pixmap;
    }
    
    // Try loading from disk
    QPixmap pixmap;
    if (loadFromDisk(key, pixmap)) {
        // Add to memory cache
        CacheEntry* newEntry = new CacheEntry;
        newEntry->pixmap = pixmap;
        newEntry->timestamp = QDateTime::currentDateTime();
        newEntry->size = calculatePixmapSize(pixmap);
        
        m_memoryCache.insert(key, newEntry, newEntry->size / 1024);
        m_hits++;
        return pixmap;
    }
    
    m_misses++;
    return QPixmap();
}

QPixmap ImageCache::getImage(const QUrl& url)
{
    QString key = urlToKey(url);
    QPixmap pixmap = getImage(key);
    
    if (pixmap.isNull() && url.isValid()) {
        // Start download
        downloadImage(key, url);
    }
    
    return pixmap;
}

void ImageCache::preloadImage(const QString& key, const QUrl& url)
{
    if (!getImage(key).isNull()) {
        return; // Already cached
    }
    
    if (url.isValid()) {
        downloadImage(key, url);
    }
}

void ImageCache::clearCache()
{
    m_memoryCache.clear();
    
    // Clear disk cache
    QDir cacheDir(m_cacheDirectory);
    QStringList files = cacheDir.entryList(QDir::Files);
    for (const QString& file : files) {
        FileUtils::deleteFile(cacheDir.filePath(file));
    }
    
    m_hits = 0;
    m_misses = 0;
    
    qInfo(imageCache) << "Cache cleared";
}

void ImageCache::clearExpired()
{
    cleanupOldEntries();
}

void ImageCache::setMaxCacheSize(int maxSizeKB)
{
    m_maxCacheSizeKB = maxSizeKB;
    m_memoryCache.setMaxCost(maxSizeKB);
}

void ImageCache::setCacheDirectory(const QString& directory)
{
    if (m_cacheDirectory != directory) {
        m_cacheDirectory = directory;
        FileUtils::createDirectory(m_cacheDirectory);
        clearCache(); // Clear old cache
    }
}

void ImageCache::setMaxAge(int maxAgeHours)
{
    m_maxAgeHours = maxAgeHours;
}

void ImageCache::setNetworkTimeout(int timeoutMs)
{
    m_networkTimeoutMs = timeoutMs;
}

int ImageCache::cacheSize() const
{
    return FileUtils::getDirectorySize(m_cacheDirectory) / 1024; // Return in KB
}

int ImageCache::cacheCount() const
{
    QDir cacheDir(m_cacheDirectory);
    return cacheDir.entryList(QDir::Files).size();
}

qreal ImageCache::hitRatio() const
{
    int total = m_hits + m_misses;
    return total > 0 ? (qreal)m_hits / total : 0.0;
}

// Private slots implementation

void ImageCache::onNetworkReplyFinished()
{
    QNetworkReply* reply = qobject_cast<QNetworkReply*>(sender());
    if (!reply) {
        return;
    }
    
    QString key = m_pendingDownloads.take(reply);
    
    if (reply->error() == QNetworkReply::NoError) {
        QByteArray data = reply->readAll();
        QPixmap pixmap;
        
        if (pixmap.loadFromData(data)) {
            // Save to disk cache
            saveToDisk(key, pixmap);
            
            // Add to memory cache
            CacheEntry* entry = new CacheEntry;
            entry->pixmap = pixmap;
            entry->timestamp = QDateTime::currentDateTime();
            entry->size = calculatePixmapSize(pixmap);
            
            m_memoryCache.insert(key, entry, entry->size / 1024);
            
            emit imageLoaded(key, pixmap);
            qDebug(imageCache) << "Image loaded and cached:" << key;
        } else {
            emit imageLoadFailed(key, "Failed to decode image data");
            qWarning(imageCache) << "Failed to decode image:" << key;
        }
    } else {
        emit imageLoadFailed(key, reply->errorString());
        qWarning(imageCache) << "Network error loading image:" << key << reply->errorString();
    }
    
    reply->deleteLater();
}

void ImageCache::onCleanupTimer()
{
    cleanupOldEntries();
}

// Private methods implementation

QString ImageCache::urlToKey(const QUrl& url) const
{
    // Create a hash-based key from the URL
    QCryptographicHash hash(QCryptographicHash::Md5);
    hash.addData(url.toString().toUtf8());
    return hash.result().toHex();
}

QString ImageCache::getCacheFilePath(const QString& key) const
{
    return QDir(m_cacheDirectory).filePath(key + ".cache");
}

bool ImageCache::loadFromDisk(const QString& key, QPixmap& pixmap)
{
    QString filePath = getCacheFilePath(key);
    
    if (!FileUtils::isFile(filePath)) {
        return false;
    }
    
    // Check if file is too old
    QDateTime modTime = FileUtils::getFileModificationTime(filePath);
    if (modTime.isValid() && modTime.secsTo(QDateTime::currentDateTime()) > m_maxAgeHours * 3600) {
        FileUtils::deleteFile(filePath);
        return false;
    }
    
    return pixmap.load(filePath);
}

bool ImageCache::saveToDisk(const QString& key, const QPixmap& pixmap)
{
    QString filePath = getCacheFilePath(key);
    return pixmap.save(filePath, "PNG");
}

void ImageCache::downloadImage(const QString& key, const QUrl& url)
{
    // Check if already downloading
    if (m_pendingDownloads.values().contains(key)) {
        return;
    }
    
    QNetworkRequest request(url);
    request.setRawHeader("User-Agent", "Mystical Game Launcher/1.0");
    request.setAttribute(QNetworkRequest::RedirectPolicyAttribute, QNetworkRequest::NoLessSafeRedirectPolicy);
    
    QNetworkReply* reply = m_networkManager->get(request);
    reply->setParent(this);
    
    // Set timeout
    QTimer::singleShot(m_networkTimeoutMs, reply, [reply]() {
        if (reply->isRunning()) {
            reply->abort();
        }
    });
    
    connect(reply, &QNetworkReply::finished, this, &ImageCache::onNetworkReplyFinished);
    
    m_pendingDownloads[reply] = key;
    
    qDebug(imageCache) << "Started downloading image:" << url;
}

void ImageCache::cleanupOldEntries()
{
    QDir cacheDir(m_cacheDirectory);
    QFileInfoList files = cacheDir.entryInfoList(QDir::Files);
    
    QDateTime cutoffTime = QDateTime::currentDateTime().addSecs(-m_maxAgeHours * 3600);
    int removedCount = 0;
    
    for (const QFileInfo& fileInfo : files) {
        if (fileInfo.lastModified() < cutoffTime) {
            if (FileUtils::deleteFile(fileInfo.filePath())) {
                removedCount++;
            }
        }
    }
    
    if (removedCount > 0) {
        qInfo(imageCache) << "Cleaned up" << removedCount << "expired cache entries";
    }
}

int ImageCache::calculatePixmapSize(const QPixmap& pixmap) const
{
    // Rough estimate: width * height * 4 bytes per pixel (ARGB)
    return pixmap.width() * pixmap.height() * 4;
}