#pragma once

#include <QString>
#include <QStringList>
#include <QFileInfo>

class FileUtils
{
public:
    // File system operations
    static bool copyFile(const QString& source, const QString& destination);
    static bool moveFile(const QString& source, const QString& destination);
    static bool deleteFile(const QString& filePath);
    static bool createDirectory(const QString& dirPath);
    static bool deleteDirectory(const QString& dirPath);
    
    // File information
    static qint64 getFileSize(const QString& filePath);
    static qint64 getDirectorySize(const QString& dirPath);
    static QDateTime getFileModificationTime(const QString& filePath);
    static QString getFileExtension(const QString& filePath);
    static QString getFileName(const QString& filePath);
    static QString getBaseName(const QString& filePath);
    
    // Path utilities
    static QString combinePaths(const QString& path1, const QString& path2);
    static QString normalizePath(const QString& path);
    static QString getRelativePath(const QString& from, const QString& to);
    static QString getParentDirectory(const QString& filePath);
    
    // File searching
    static QStringList findFiles(const QString& directory, const QStringList& patterns, bool recursive = false);
    static QStringList findExecutables(const QString& directory, bool recursive = false);
    static QString findExecutable(const QString& directory, const QString& name);
    
    // Validation
    static bool isValidFileName(const QString& fileName);
    static bool isValidPath(const QString& path);
    static bool pathExists(const QString& path);
    static bool isDirectory(const QString& path);
    static bool isFile(const QString& path);
    static bool isExecutable(const QString& filePath);
    
    // Safe operations
    static QString createTempFile(const QString& prefix = "mystical_");
    static QString createTempDirectory(const QString& prefix = "mystical_");
    static bool safeWrite(const QString& filePath, const QByteArray& data);
    static QByteArray safeRead(const QString& filePath);
};