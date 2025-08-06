#include "Settings.h"
#include <QStandardPaths>
#include <QDir>
#include <QDebug>
#include <QApplication>
#include <QScreen>

#ifdef Q_OS_WIN
#include <QSettings>
#include <QSysInfo>
#endif

Settings::Settings(QObject *parent)
    : QObject(parent)
    , m_settings(nullptr)
    , m_isDarkMode(true)
    , m_startWithSystem(false)
    , m_minimizeToTray(true)
    , m_autoScanGames(true)
    , m_autoScanInterval(30)
    , m_windowWidth(1200)
    , m_windowHeight(800)
    , m_windowX(-1)
    , m_windowY(-1)
    , m_isMaximized(false)
{
    initializeSettings();
    loadSettings();
    detectSystemTheme();
}

Settings::~Settings()
{
    save();
}

void Settings::initializeSettings()
{
    // Create settings object with proper organization and application name
    m_settings = new QSettings(QSettings::IniFormat, QSettings::UserScope,
                              QApplication::organizationName(),
                              QApplication::applicationName(),
                              this);
    
    // Ensure settings directory exists
    QString settingsDir = QFileInfo(m_settings->fileName()).absolutePath();
    QDir().mkpath(settingsDir);
    
    qDebug() << "Settings file:" << m_settings->fileName();
}

void Settings::loadSettings()
{
    if (!m_settings) {
        return;
    }
    
    // Appearance settings
    m_isDarkMode = m_settings->value("appearance/darkMode", true).toBool();
    m_language = m_settings->value("appearance/language", "English").toString();
    m_accentColor = m_settings->value("appearance/accentColor", "#0078d4").toString();
    
    // Behavior settings
    m_startWithSystem = m_settings->value("behavior/startWithSystem", false).toBool();
    m_minimizeToTray = m_settings->value("behavior/minimizeToTray", true).toBool();
    m_checkForUpdates = m_settings->value("behavior/checkForUpdates", true).toBool();
    
    // Game library settings
    m_autoScanGames = m_settings->value("gameLibrary/autoScan", true).toBool();
    m_autoScanInterval = m_settings->value("gameLibrary/scanInterval", 30).toInt();
    
    // Window settings
    m_windowWidth = m_settings->value("window/width", 1200).toInt();
    m_windowHeight = m_settings->value("window/height", 800).toInt();
    m_windowX = m_settings->value("window/x", -1).toInt();
    m_windowY = m_settings->value("window/y", -1).toInt();
    m_isMaximized = m_settings->value("window/maximized", false).toBool();
    
    // Performance settings
    m_enableAnimations = m_settings->value("performance/animations", true).toBool();
    m_enableTransparency = m_settings->value("performance/transparency", true).toBool();
    m_imageCacheSize = m_settings->value("performance/imageCacheSize", 500).toInt();
    
    // Privacy settings
    m_usageStatistics = m_settings->value("privacy/usageStatistics", false).toBool();
    m_crashReports = m_settings->value("privacy/crashReports", true).toBool();
    m_onlineCoverArt = m_settings->value("privacy/onlineCoverArt", true).toBool();
    
    // Platform settings
    loadPlatformSettings();
    
    qDebug() << "Settings loaded successfully";
}

void Settings::save()
{
    if (!m_settings) {
        return;
    }
    
    // Appearance settings
    m_settings->setValue("appearance/darkMode", m_isDarkMode);
    m_settings->setValue("appearance/language", m_language);
    m_settings->setValue("appearance/accentColor", m_accentColor);
    
    // Behavior settings
    m_settings->setValue("behavior/startWithSystem", m_startWithSystem);
    m_settings->setValue("behavior/minimizeToTray", m_minimizeToTray);
    m_settings->setValue("behavior/checkForUpdates", m_checkForUpdates);
    
    // Game library settings
    m_settings->setValue("gameLibrary/autoScan", m_autoScanGames);
    m_settings->setValue("gameLibrary/scanInterval", m_autoScanInterval);
    
    // Window settings
    m_settings->setValue("window/width", m_windowWidth);
    m_settings->setValue("window/height", m_windowHeight);
    m_settings->setValue("window/x", m_windowX);
    m_settings->setValue("window/y", m_windowY);
    m_settings->setValue("window/maximized", m_isMaximized);
    
    // Performance settings
    m_settings->setValue("performance/animations", m_enableAnimations);
    m_settings->setValue("performance/transparency", m_enableTransparency);
    m_settings->setValue("performance/imageCacheSize", m_imageCacheSize);
    
    // Privacy settings
    m_settings->setValue("privacy/usageStatistics", m_usageStatistics);
    m_settings->setValue("privacy/crashReports", m_crashReports);
    m_settings->setValue("privacy/onlineCoverArt", m_onlineCoverArt);
    
    // Platform settings
    savePlatformSettings();
    
    // Force sync to disk
    m_settings->sync();
    
    qDebug() << "Settings saved successfully";
}

void Settings::resetToDefaults()
{
    if (!m_settings) {
        return;
    }
    
    // Clear all settings
    m_settings->clear();
    
    // Reset to default values
    setIsDarkMode(true);
    setLanguage("English");
    setAccentColor("#0078d4");
    setStartWithSystem(false);
    setMinimizeToTray(true);
    setCheckForUpdates(true);
    setAutoScanGames(true);
    setAutoScanInterval(30);
    setEnableAnimations(true);
    setEnableTransparency(true);
    setImageCacheSize(500);
    setUsageStatistics(false);
    setCrashReports(true);
    setOnlineCoverArt(true);
    
    // Reset platform settings
    m_platformSettings.clear();
    m_platformSettings["Steam"] = true;
    m_platformSettings["Epic Games"] = true;
    m_platformSettings["GOG"] = true;
    m_platformSettings["EA App"] = true;
    m_platformSettings["Minecraft"] = true;
    m_platformSettings["Roblox"] = false;
    
    save();
    
    emit settingsReset();
    qDebug() << "Settings reset to defaults";
}

void Settings::detectSystemTheme()
{
#ifdef Q_OS_WIN
    // On Windows, check the system theme preference
    QSettings registry("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                      QSettings::NativeFormat);
    
    bool systemUsesLightTheme = registry.value("AppsUseLightTheme", 0).toBool();
    m_systemIsDarkMode = !systemUsesLightTheme;
    
    qDebug() << "System theme detected:" << (m_systemIsDarkMode ? "Dark" : "Light");
#else
    // On other platforms, assume dark mode for now
    m_systemIsDarkMode = true;
#endif
    
    emit systemThemeChanged();
}

void Settings::loadPlatformSettings()
{
    m_settings->beginGroup("platforms");
    
    // Default platform enablement
    m_platformSettings["Steam"] = m_settings->value("Steam", true).toBool();
    m_platformSettings["Epic Games"] = m_settings->value("EpicGames", true).toBool();
    m_platformSettings["GOG"] = m_settings->value("GOG", true).toBool();
    m_platformSettings["EA App"] = m_settings->value("EAApp", true).toBool();
    m_platformSettings["Minecraft"] = m_settings->value("Minecraft", true).toBool();
    m_platformSettings["Roblox"] = m_settings->value("Roblox", false).toBool();
    
    m_settings->endGroup();
}

void Settings::savePlatformSettings()
{
    m_settings->beginGroup("platforms");
    
    for (auto it = m_platformSettings.begin(); it != m_platformSettings.end(); ++it) {
        QString key = it.key();
        key.replace(" ", ""); // Remove spaces for registry key
        m_settings->setValue(key, it.value());
    }
    
    m_settings->endGroup();
}

void Settings::updateStartupRegistry()
{
#ifdef Q_OS_WIN
    QSettings startup("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Run",
                     QSettings::NativeFormat);
    
    const QString appName = QApplication::applicationName();
    
    if (m_startWithSystem) {
        QString appPath = QApplication::applicationFilePath();
        startup.setValue(appName, QDir::toNativeSeparators(appPath));
        qDebug() << "Added to Windows startup";
    } else {
        startup.remove(appName);
        qDebug() << "Removed from Windows startup";
    }
#endif
}

// Property setters with change notifications
void Settings::setIsDarkMode(bool isDarkMode)
{
    if (m_isDarkMode != isDarkMode) {
        m_isDarkMode = isDarkMode;
        emit isDarkModeChanged();
    }
}

void Settings::setLanguage(const QString &language)
{
    if (m_language != language) {
        m_language = language;
        emit languageChanged();
    }
}

void Settings::setAccentColor(const QString &accentColor)
{
    if (m_accentColor != accentColor) {
        m_accentColor = accentColor;
        emit accentColorChanged();
    }
}

void Settings::setStartWithSystem(bool startWithSystem)
{
    if (m_startWithSystem != startWithSystem) {
        m_startWithSystem = startWithSystem;
        updateStartupRegistry();
        emit startWithSystemChanged();
    }
}

void Settings::setMinimizeToTray(bool minimizeToTray)
{
    if (m_minimizeToTray != minimizeToTray) {
        m_minimizeToTray = minimizeToTray;
        emit minimizeToTrayChanged();
    }
}

void Settings::setCheckForUpdates(bool checkForUpdates)
{
    if (m_checkForUpdates != checkForUpdates) {
        m_checkForUpdates = checkForUpdates;
        emit checkForUpdatesChanged();
    }
}

void Settings::setAutoScanGames(bool autoScanGames)
{
    if (m_autoScanGames != autoScanGames) {
        m_autoScanGames = autoScanGames;
        emit autoScanGamesChanged();
    }
}

void Settings::setAutoScanInterval(int autoScanInterval)
{
    if (m_autoScanInterval != autoScanInterval) {
        m_autoScanInterval = autoScanInterval;
        emit autoScanIntervalChanged();
    }
}

void Settings::setEnableAnimations(bool enableAnimations)
{
    if (m_enableAnimations != enableAnimations) {
        m_enableAnimations = enableAnimations;
        emit enableAnimationsChanged();
    }
}

void Settings::setEnableTransparency(bool enableTransparency)
{
    if (m_enableTransparency != enableTransparency) {
        m_enableTransparency = enableTransparency;
        emit enableTransparencyChanged();
    }
}

void Settings::setImageCacheSize(int imageCacheSize)
{
    if (m_imageCacheSize != imageCacheSize) {
        m_imageCacheSize = imageCacheSize;
        emit imageCacheSizeChanged();
    }
}

void Settings::setUsageStatistics(bool usageStatistics)
{
    if (m_usageStatistics != usageStatistics) {
        m_usageStatistics = usageStatistics;
        emit usageStatisticsChanged();
    }
}

void Settings::setCrashReports(bool crashReports)
{
    if (m_crashReports != crashReports) {
        m_crashReports = crashReports;
        emit crashReportsChanged();
    }
}

void Settings::setOnlineCoverArt(bool onlineCoverArt)
{
    if (m_onlineCoverArt != onlineCoverArt) {
        m_onlineCoverArt = onlineCoverArt;
        emit onlineCoverArtChanged();
    }
}

void Settings::setPlatformEnabled(const QString &platform, bool enabled)
{
    if (m_platformSettings.value(platform) != enabled) {
        m_platformSettings[platform] = enabled;
        emit platformSettingsChanged();
    }
}

bool Settings::isPlatformEnabled(const QString &platform) const
{
    return m_platformSettings.value(platform, true);
}

void Settings::setWindowGeometry(int width, int height, int x, int y, bool maximized)
{
    bool changed = false;
    
    if (m_windowWidth != width) {
        m_windowWidth = width;
        changed = true;
    }
    
    if (m_windowHeight != height) {
        m_windowHeight = height;
        changed = true;
    }
    
    if (m_windowX != x) {
        m_windowX = x;
        changed = true;
    }
    
    if (m_windowY != y) {
        m_windowY = y;
        changed = true;
    }
    
    if (m_isMaximized != maximized) {
        m_isMaximized = maximized;
        changed = true;
    }
    
    if (changed) {
        emit windowGeometryChanged();
    }
}