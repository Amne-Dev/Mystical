#include "Settings.h"
#include <QStandardPaths>
#include <QDir>
#include <QJsonDocument>
#include <QJsonObject>
#include <QCoreApplication>
#include <QLoggingCategory>

#ifdef Q_OS_WIN
#include <QWinTaskbarButton>
#include <QSettings>
#endif

Q_LOGGING_CATEGORY(settings, "mystical.settings")

const QString Settings::DEFAULT_LANGUAGE = "en";

Settings::Settings(QObject* parent)
    : QObject(parent)
{
    // Initialize QSettings
    QString configPath = QStandardPaths::writableLocation(QStandardPaths::AppConfigLocation);
    QDir().mkpath(configPath);
    
    QString settingsFile = QDir(configPath).filePath("mystical.ini");
    m_settings = new QSettings(settingsFile, QSettings::IniFormat, this);
    
    // Load settings
    loadDefaults();
    load();
    
    qInfo(settings) << "Settings initialized from:" << settingsFile;
}

Settings::~Settings()
{
    save();
}

void Settings::setIsDarkMode(bool dark)
{
    if (m_isDarkMode != dark) {
        m_isDarkMode = dark;
        setValue("theme/isDarkMode", dark);
        emit isDarkModeChanged();
        emit settingsChanged();
    }
}

void Settings::setLanguage(const QString& language)
{
    if (m_language != language) {
        m_language = language;
        setValue("general/language", language);
        emit languageChanged();
        emit settingsChanged();
    }
}

void Settings::setStartWithSystem(bool start)
{
    if (m_startWithSystem != start) {
        m_startWithSystem = start;
        setValue("startup/startWithSystem", start);
        
        // Update system startup registry
        if (start) {
            setupSystemStartup();
        } else {
            removeSystemStartup();
        }
        
        emit startWithSystemChanged();
        emit settingsChanged();
    }
}

void Settings::setMinimizeToTray(bool minimize)
{
    if (m_minimizeToTray != minimize) {
        m_minimizeToTray = minimize;
        setValue("window/minimizeToTray", minimize);
        emit minimizeToTrayChanged();
        emit settingsChanged();
    }
}

void Settings::setAutoScanGames(bool autoScan)
{
    if (m_autoScanGames != autoScan) {
        m_autoScanGames = autoScan;
        setValue("games/autoScanGames", autoScan);
        emit autoScanGamesChanged();
        emit settingsChanged();
    }
}

void Settings::setAutoScanInterval(int minutes)
{
    if (m_autoScanInterval != minutes) {
        m_autoScanInterval = minutes;
        setValue("games/autoScanInterval", minutes);
        emit autoScanIntervalChanged();
        emit settingsChanged();
    }
}

void Settings::setWindowWidth(int width)
{
    if (m_windowWidth != width) {
        m_windowWidth = width;
        setValue("window/width", width);
        emit windowWidthChanged();
        emit settingsChanged();
    }
}

void Settings::setWindowHeight(int height)
{
    if (m_windowHeight != height) {
        m_windowHeight = height;
        setValue("window/height", height);
        emit windowHeightChanged();
        emit settingsChanged();
    }
}

void Settings::setWindowMaximized(bool maximized)
{
    if (m_windowMaximized != maximized) {
        m_windowMaximized = maximized;
        setValue("window/maximized", maximized);
        emit windowMaximizedChanged();
        emit settingsChanged();
    }
}

QVariant Settings::getValue(const QString& key, const QVariant& defaultValue) const
{
    return m_settings->value(key, defaultValue);
}

void Settings::setValue(const QString& key, const QVariant& value)
{
    m_settings->setValue(key, value);
    emit settingsChanged();
}

bool Settings::contains(const QString& key) const
{
    return m_settings->contains(key);
}

void Settings::remove(const QString& key)
{
    m_settings->remove(key);
    emit settingsChanged();
}

void Settings::resetToDefaults()
{
    qInfo(settings) << "Resetting settings to defaults";
    
    // Clear all settings
    m_settings->clear();
    
    // Load defaults
    loadDefaults();
    
    // Emit all change signals
    emit isDarkModeChanged();
    emit languageChanged();
    emit startWithSystemChanged();
    emit minimizeToTrayChanged();
    emit autoScanGamesChanged();
    emit autoScanIntervalChanged();
    emit windowWidthChanged();
    emit windowHeightChanged();
    emit windowMaximizedChanged();
    emit settingsChanged();
    
    // Save to persist the reset
    save();
}

void Settings::exportSettings(const QString& filePath) const
{
    QJsonObject settingsObj;
    
    // Theme settings
    QJsonObject themeObj;
    themeObj["isDarkMode"] = m_isDarkMode;
    settingsObj["theme"] = themeObj;
    
    // General settings
    QJsonObject generalObj;
    generalObj["language"] = m_language;
    settingsObj["general"] = generalObj;
    
    // Startup settings
    QJsonObject startupObj;
    startupObj["startWithSystem"] = m_startWithSystem;
    settingsObj["startup"] = startupObj;
    
    // Window settings
    QJsonObject windowObj;
    windowObj["minimizeToTray"] = m_minimizeToTray;
    windowObj["width"] = m_windowWidth;
    windowObj["height"] = m_windowHeight;
    windowObj["maximized"] = m_windowMaximized;
    settingsObj["window"] = windowObj;
    
    // Game settings
    QJsonObject gamesObj;
    gamesObj["autoScanGames"] = m_autoScanGames;
    gamesObj["autoScanInterval"] = m_autoScanInterval;
    settingsObj["games"] = gamesObj;
    
    // Export metadata
    QJsonObject rootObj;
    rootObj["version"] = "1.0";
    rootObj["exportDate"] = QDateTime::currentDateTime().toString(Qt::ISODate);
    rootObj["application"] = "Mystical";
    rootObj["settings"] = settingsObj;
    
    QJsonDocument doc(rootObj);
    
    QFile file(filePath);
    if (file.open(QIODevice::WriteOnly)) {
        file.write(doc.toJson());
        qInfo(settings) << "Exported settings to:" << filePath;
    } else {
        qWarning(settings) << "Failed to export settings to:" << filePath;
    }
}

bool Settings::importSettings(const QString& filePath)
{
    QFile file(filePath);
    if (!file.open(QIODevice::ReadOnly)) {
        qWarning(settings) << "Failed to open settings import file:" << filePath;
        return false;
    }
    
    QJsonDocument doc = QJsonDocument::fromJson(file.readAll());
    if (doc.isNull()) {
        qWarning(settings) << "Invalid JSON in settings import file:" << filePath;
        return false;
    }
    
    QJsonObject rootObj = doc.object();
    QJsonObject settingsObj = rootObj["settings"].toObject();
    
    if (settingsObj.isEmpty()) {
        qWarning(settings) << "No settings found in import file:" << filePath;
        return false;
    }
    
    // Import theme settings
    QJsonObject themeObj = settingsObj["theme"].toObject();
    if (themeObj.contains("isDarkMode")) {
        setIsDarkMode(themeObj["isDarkMode"].toBool());
    }
    
    // Import general settings
    QJsonObject generalObj = settingsObj["general"].toObject();
    if (generalObj.contains("language")) {
        setLanguage(generalObj["language"].toString());
    }
    
    // Import startup settings
    QJsonObject startupObj = settingsObj["startup"].toObject();
    if (startupObj.contains("startWithSystem")) {
        setStartWithSystem(startupObj["startWithSystem"].toBool());
    }
    
    // Import window settings
    QJsonObject windowObj = settingsObj["window"].toObject();
    if (windowObj.contains("minimizeToTray")) {
        setMinimizeToTray(windowObj["minimizeToTray"].toBool());
    }
    if (windowObj.contains("width")) {
        setWindowWidth(windowObj["width"].toInt());
    }
    if (windowObj.contains("height")) {
        setWindowHeight(windowObj["height"].toInt());
    }
    if (windowObj.contains("maximized")) {
        setWindowMaximized(windowObj["maximized"].toBool());
    }
    
    // Import game settings
    QJsonObject gamesObj = settingsObj["games"].toObject();
    if (gamesObj.contains("autoScanGames")) {
        setAutoScanGames(gamesObj["autoScanGames"].toBool());
    }
    if (gamesObj.contains("autoScanInterval")) {
        setAutoScanInterval(gamesObj["autoScanInterval"].toInt());
    }
    
    save();
    
    qInfo(settings) << "Imported settings from:" << filePath;
    return true;
}

void Settings::save()
{
    m_settings->sync();
    qDebug(settings) << "Settings saved";
}

void Settings::load()
{
    m_isDarkMode = m_settings->value("theme/isDarkMode", DEFAULT_DARK_MODE).toBool();
    m_language = m_settings->value("general/language", DEFAULT_LANGUAGE).toString();
    m_startWithSystem = m_settings->value("startup/startWithSystem", DEFAULT_START_WITH_SYSTEM).toBool();
    m_minimizeToTray = m_settings->value("window/minimizeToTray", DEFAULT_MINIMIZE_TO_TRAY).toBool();
    m_autoScanGames = m_settings->value("games/autoScanGames", DEFAULT_AUTO_SCAN_GAMES).toBool();
    m_autoScanInterval = m_settings->value("games/autoScanInterval", DEFAULT_AUTO_SCAN_INTERVAL).toInt();
    m_windowWidth = m_settings->value("window/width", DEFAULT_WINDOW_WIDTH).toInt();
    m_windowHeight = m_settings->value("window/height", DEFAULT_WINDOW_HEIGHT).toInt();
    m_windowMaximized = m_settings->value("window/maximized", DEFAULT_WINDOW_MAXIMIZED).toBool();
    
    qDebug(settings) << "Settings loaded";
}

void Settings::sync()
{
    m_settings->sync();
}

void Settings::loadDefaults()
{
    m_isDarkMode = DEFAULT_DARK_MODE;
    m_language = DEFAULT_LANGUAGE;
    m_startWithSystem = DEFAULT_START_WITH_SYSTEM;
    m_minimizeToTray = DEFAULT_MINIMIZE_TO_TRAY;
    m_autoScanGames = DEFAULT_AUTO_SCAN_GAMES;
    m_autoScanInterval = DEFAULT_AUTO_SCAN_INTERVAL;
    m_windowWidth = DEFAULT_WINDOW_WIDTH;
    m_windowHeight = DEFAULT_WINDOW_HEIGHT;
    m_windowMaximized = DEFAULT_WINDOW_MAXIMIZED;
}

void Settings::setupSystemStartup()
{
#ifdef Q_OS_WIN
    QString appPath = QCoreApplication::applicationFilePath();
    QString regPath = getStartupRegistryPath();
    
    QSettings registry(regPath, QSettings::NativeFormat);
    registry.setValue("Mystical", QString("\"%1\"").arg(appPath));
    
    qInfo(settings) << "Added to system startup";
#else
    qWarning(settings) << "System startup not implemented for this platform";
#endif
}

void Settings::removeSystemStartup()
{
#ifdef Q_OS_WIN
    QString regPath = getStartupRegistryPath();
    
    QSettings registry(regPath, QSettings::NativeFormat);
    registry.remove("Mystical");
    
    qInfo(settings) << "Removed from system startup";
#else
    qWarning(settings) << "System startup not implemented for this platform";
#endif
}

QString Settings::getStartupRegistryPath() const
{
#ifdef Q_OS_WIN
    return "HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
#else
    return QString();
#endif
}