#pragma once

#include <QString>
#include <QStringList>
#include <QVariant>

class RegistryUtils
{
public:
    // Registry reading
    static QString readString(const QString& key, const QString& valueName, const QString& defaultValue = QString());
    static int readInt(const QString& key, const QString& valueName, int defaultValue = 0);
    static bool readBool(const QString& key, const QString& valueName, bool defaultValue = false);
    static QVariant readValue(const QString& key, const QString& valueName, const QVariant& defaultValue = QVariant());
    
    // Registry writing
    static bool writeString(const QString& key, const QString& valueName, const QString& value);
    static bool writeInt(const QString& key, const QString& valueName, int value);
    static bool writeBool(const QString& key, const QString& valueName, bool value);
    static bool writeValue(const QString& key, const QString& valueName, const QVariant& value);
    
    // Registry structure
    static QStringList getSubKeys(const QString& key);
    static QStringList getValueNames(const QString& key);
    static bool keyExists(const QString& key);
    static bool valueExists(const QString& key, const QString& valueName);
    
    // Registry management
    static bool createKey(const QString& key);
    static bool deleteKey(const QString& key);
    static bool deleteValue(const QString& key, const QString& valueName);
    
    // Utility functions
    static QString expandEnvironmentStrings(const QString& str);
    static QString getComputerName();
    static QString getUserName();
};
