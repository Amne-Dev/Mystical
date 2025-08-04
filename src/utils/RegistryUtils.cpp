#include "RegistryUtils.h"

#ifdef Q_OS_WIN
#include <QSettings>
#include <QDir>
#include <QStandardPaths>
#include <windows.h>
#include <lmcons.h>  // For UNLEN constant
#endif

QString RegistryUtils::readString(const QString& key, const QString& valueName, const QString& defaultValue)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    return registry.value(valueName, defaultValue).toString();
#else
    Q_UNUSED(key)
    Q_UNUSED(valueName)
    return defaultValue;
#endif
}

int RegistryUtils::readInt(const QString& key, const QString& valueName, int defaultValue)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    return registry.value(valueName, defaultValue).toInt();
#else
    Q_UNUSED(key)
    Q_UNUSED(valueName)
    return defaultValue;
#endif
}

bool RegistryUtils::readBool(const QString& key, const QString& valueName, bool defaultValue)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    return registry.value(valueName, defaultValue).toBool();
#else
    Q_UNUSED(key)
    Q_UNUSED(valueName)
    return defaultValue;
#endif
}

QVariant RegistryUtils::readValue(const QString& key, const QString& valueName, const QVariant& defaultValue)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    return registry.value(valueName, defaultValue);
#else
    Q_UNUSED(key)
    Q_UNUSED(valueName)
    return defaultValue;
#endif
}

bool RegistryUtils::writeString(const QString& key, const QString& valueName, const QString& value)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    registry.setValue(valueName, value);
    return registry.status() == QSettings::NoError;
#else
    Q_UNUSED(key)
    Q_UNUSED(valueName)
    Q_UNUSED(value)
    return false;
#endif
}

bool RegistryUtils::writeInt(const QString& key, const QString& valueName, int value)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    registry.setValue(valueName, value);
    return registry.status() == QSettings::NoError;
#else
    Q_UNUSED(key)
    Q_UNUSED(valueName)
    Q_UNUSED(value)
    return false;
#endif
}

bool RegistryUtils::writeBool(const QString& key, const QString& valueName, bool value)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    registry.setValue(valueName, value);
    return registry.status() == QSettings::NoError;
#else
    Q_UNUSED(key)
    Q_UNUSED(valueName)
    Q_UNUSED(value)
    return false;
#endif
}

bool RegistryUtils::writeValue(const QString& key, const QString& valueName, const QVariant& value)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    registry.setValue(valueName, value);
    return registry.status() == QSettings::NoError;
#else
    Q_UNUSED(key)
    Q_UNUSED(valueName)
    Q_UNUSED(value)
    return false;
#endif
}

QStringList RegistryUtils::getSubKeys(const QString& key)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    return registry.childGroups();
#else
    Q_UNUSED(key)
    return QStringList();
#endif
}

QStringList RegistryUtils::getValueNames(const QString& key)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    return registry.childKeys();
#else
    Q_UNUSED(key)
    return QStringList();
#endif
}

bool RegistryUtils::keyExists(const QString& key)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    return !registry.childGroups().isEmpty() || !registry.childKeys().isEmpty();
#else
    Q_UNUSED(key)
    return false;
#endif
}

bool RegistryUtils::valueExists(const QString& key, const QString& valueName)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    return registry.contains(valueName);
#else
    Q_UNUSED(key)
    Q_UNUSED(valueName)
    return false;
#endif
}

bool RegistryUtils::createKey(const QString& key)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    registry.setValue("__temp__", "");
    registry.remove("__temp__");
    return registry.status() == QSettings::NoError;
#else
    Q_UNUSED(key)
    return false;
#endif
}

bool RegistryUtils::deleteKey(const QString& key)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    registry.clear();
    return registry.status() == QSettings::NoError;
#else
    Q_UNUSED(key)
    return false;
#endif
}

bool RegistryUtils::deleteValue(const QString& key, const QString& valueName)
{
#ifdef Q_OS_WIN
    QSettings registry(key, QSettings::NativeFormat);
    registry.remove(valueName);
    return registry.status() == QSettings::NoError;
#else
    Q_UNUSED(key)
    Q_UNUSED(valueName)
    return false;
#endif
}

QString RegistryUtils::expandEnvironmentStrings(const QString& str)
{
#ifdef Q_OS_WIN
    QString result = str;
    
    // Replace common environment variables
    result.replace("%USERPROFILE%", QDir::homePath());
    result.replace("%APPDATA%", QStandardPaths::writableLocation(QStandardPaths::AppDataLocation));
    result.replace("%LOCALAPPDATA%", QStandardPaths::writableLocation(QStandardPaths::AppLocalDataLocation));
    result.replace("%PROGRAMFILES%", "C:/Program Files");
    result.replace("%PROGRAMFILES(X86)%", "C:/Program Files (x86)");
    result.replace("%TEMP%", QDir::tempPath());
    
    return QDir::toNativeSeparators(result);
#else
    return str;
#endif
}

QString RegistryUtils::getComputerName()
{
#ifdef Q_OS_WIN
    wchar_t buffer[MAX_COMPUTERNAME_LENGTH + 1];
    DWORD size = sizeof(buffer) / sizeof(wchar_t);
    
    if (GetComputerNameW(buffer, &size)) {
        return QString::fromWCharArray(buffer);
    }
#endif
    return QString();
}

QString RegistryUtils::getUserName()
{
#ifdef Q_OS_WIN
    wchar_t buffer[UNLEN + 1];
    DWORD size = sizeof(buffer) / sizeof(wchar_t);
    
    if (GetUserNameW(buffer, &size)) {
        return QString::fromWCharArray(buffer);
    }
#endif
    return QString();
}