#include "FileUtils.h"
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QDateTime>
#include <QDirIterator>

bool FileUtils::createDirectory(const QString& path)
{
    QDir dir;
    return dir.mkpath(path);
}

bool FileUtils::deleteFile(const QString& path)
{
    return QFile::remove(path);
}

bool FileUtils::isFile(const QString& path)
{
    QFileInfo info(path);
    return info.exists() && info.isFile();
}

bool FileUtils::isDirectory(const QString& path)
{
    QFileInfo info(path);
    return info.exists() && info.isDir();
}

QDateTime FileUtils::getFileModificationTime(const QString& path)
{
    QFileInfo info(path);
    return info.lastModified();
}

qint64 FileUtils::getDirectorySize(const QString& path)
{
    qint64 size = 0;
    QDirIterator it(path, QDirIterator::Subdirectories);
    while (it.hasNext()) {
        QFileInfo info(it.next());
        if (info.isFile()) {
            size += info.size();
        }
    }
    return size;
}

QString FileUtils::normalizePath(const QString& path)
{
    return QDir::cleanPath(path);
}
