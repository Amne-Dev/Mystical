#ifndef SETTINGS_H
#define SETTINGS_H

#include <QObject>
#include <QSettings>
#include <QVariantMap>
#include <QString>

/**
 * @brief Manages application settings and preferences
 * 
 * This class handles all application settings including:
 * - Appearance (theme, colors, language)
 * - Behavior (startup, system tray)
 * - Game library preferences
 * - Window geometry
 * - Platform detection settings
 * - Performance options
 * - Privacy settings
 */
class Settings : public QObject
{
    Q_OBJECT
    
    // Appearance properties
    Q_PROPERTY(bool isDarkMode READ isDarkMode WRITE setIsDarkMode NOTIFY isDarkModeChanged)
    Q_PROPERTY(QString language READ language WRITE setLanguage NOTIFY languageChanged)
    Q_PROPERTY(QString accentColor READ accentColor WRITE setAccentColor NOTIFY accentColorChanged)
    Q_PROPERTY(bool systemIsDarkMode READ systemIsDarkMode NOTIFY systemThemeChanged)
    
    // Behavior properties
    Q_PROPERTY(bool startWithSystem READ startWithSystem WRITE setStartWithSystem NOTIFY startWithSystemChanged)
    Q_PROPERTY(bool minimizeToTray READ minimizeToTray WRITE setMinimizeToTray NOTIFY minimizeToTrayChanged)
    Q_PROPERTY(bool checkForUpdates READ checkForUpdates WRITE setCheckForUpdates NOTIFY checkForUpdatesChanged)
    
    // Game library properties
    Q_PROPERTY(bool autoScanGames READ autoScanGames WRITE setAutoScanGames NOTIFY autoScanGamesChanged)
    Q_PROPERTY(int autoScanInterval READ autoScanInterval WRITE setAutoScanInterval NOTIFY autoScanIntervalChanged)
    
    // Window properties
    Q_PROPERTY(int windowWidth READ windowWidth NOTIFY windowGeometryChanged)
    Q_PROPERTY(int windowHeight READ windowHeight NOTIFY windowGeometryChanged)
    Q_PROPERTY(int windowX READ windowX NOTIFY windowGeometryChanged)
    Q_PROPERTY(int windowY READ windowY NOTIFY windowGeometryChanged)
    Q_PROPERTY(bool isMaximized READ isMaximized NOTIFY windowGeometryChanged)
    
    // Performance properties
    Q_PROPERTY(bool enableAnimations READ enableAnimations WRITE setEnableAnimations NOTIFY enableAnimationsChanged)
    Q_PROPERTY(bool enableTransparency READ enableTransparency WRITE setEnableTransparency NOTIFY enableTransparencyChanged)
    Q_PROPERTY(int imageCacheSize READ imageCacheSize WRITE setImageCacheSize NOTIFY imageCacheSizeChanged)
    
    // Privacy properties
    Q_PROPERTY(bool usageStatistics READ usageStatistics WRITE setUsageStatistics NOTIFY usageStatisticsChanged)
    Q_PROPERTY(bool crashReports READ crashReports WRITE setCrashReports NOTIFY crashReportsChanged)
    Q_PROPERTY(bool onlineCoverArt READ onlineCoverArt WRITE setOnlineCoverArt NOTIFY onlineCoverArtChanged)

public:
    explicit Settings(QObject *parent = nullptr);
    ~Settings() override;

    // Appearance getters
    bool isDarkMode() const { return m_isDarkMode; }
    QString language() const { return m_language; }
    QString accentColor() const { return m_accentColor; }
    bool systemIsDarkMode() const { return m_systemIsDarkMode; }
    
    // Behavior getters
    bool startWithSystem() const { return m_startWithSystem; }
    bool minimizeToTray() const { return m_minimizeToTray; }
    bool checkForUpdates() const { return m_checkForUpdates; }
    
    // Game library getters
    bool autoScanGames() const { return m_autoScanGames; }
    int autoScanInterval() const { return m_autoScanInterval; }
    
    // Window getters
    int windowWidth() const { return m_windowWidth; }
    int windowHeight() const { return m_windowHeight; }
    int windowX() const { return m_windowX; }
    int windowY() const { return m_windowY; }
    bool isMaximized() const { return m_isMaximized; }
    
    // Performance getters
    bool enableAnimations() const { return m_enableAnimations; }
    bool enableTransparency() const { return m_enableTransparency; }
    int imageCacheSize() const { return m_imageCacheSize; }
    
    // Privacy getters
    bool usageStatistics() const { return m_usageStatistics; }
    bool crashReports() const { return m_crashReports; }
    bool onlineCoverArt() const { return m_onlineCoverArt; }
    
    // Platform settings
    Q_INVOKABLE bool isPlatformEnabled(const QString &platform) const;
    Q_INVOKABLE void setPlatformEnabled(const QString &platform, bool enabled);
    
    // Window geometry management
    Q_INVOKABLE void setWindowGeometry(int width, int height, int x, int y, bool maximized);

public slots:
    // Settings management
    void save();
    void resetToDefaults();
    void detectSystemTheme();
    
    // Appearance setters
    void setIsDarkMode(bool isDarkMode);
    void setLanguage(const QString &language);
    void setAccentColor(const QString &accentColor);
    
    // Behavior setters
    void setStartWithSystem(bool startWithSystem);
    void setMinimizeToTray(bool minimizeToTray);
    void setCheckForUpdates(bool checkForUpdates);
    
    // Game library setters
    void setAutoScanGames(bool autoScanGames);
    void setAutoScanInterval(int autoScanInterval);
    
    // Performance setters
    void setEnableAnimations(bool enableAnimations);
    void setEnableTransparency(bool enableTransparency);
    void setImageCacheSize(int imageCacheSize);
    
    // Privacy setters
    void setUsageStatistics(bool usageStatistics);
    void setCrashReports(bool crashReports);
    void setOnlineCoverArt(bool onlineCoverArt);

signals:
    // Appearance signals
    void isDarkModeChanged();
    void languageChanged();
    void accentColorChanged();
    void systemThemeChanged();
    
    // Behavior signals
    void startWithSystemChanged();
    void minimizeToTrayChanged();
    void checkForUpdatesChanged();
    
    // Game library signals
    void autoScanGamesChanged();
    void autoScanIntervalChanged();
    
    // Window signals
    void windowGeometryChanged();
    
    // Performance signals
    void enableAnimationsChanged();
    void enableTransparencyChanged();
    void imageCacheSizeChanged();
    
    // Privacy signals
    void usageStatisticsChanged();
    void crashReportsChanged();
    void onlineCoverArtChanged();
    
    // Platform signals
    void platformSettingsChanged();
    
    // General signals
    void settingsReset();

private:
    void initializeSettings();
    void loadSettings();
    void loadPlatformSettings();
    void savePlatformSettings();
    void updateStartupRegistry();

private:
    QSettings *m_settings;
    
    // Appearance settings
    bool m_isDarkMode;
    QString m_language;
    QString m_accentColor;
    bool m_systemIsDarkMode;
    
    // Behavior settings
    bool m_startWithSystem;
    bool m_minimizeToTray;
    bool m_checkForUpdates;
    
    // Game library settings
    bool m_autoScanGames;
    int m_autoScanInterval;
    
    // Window settings
    int m_windowWidth;
    int m_windowHeight;
    int m_windowX;
    int m_windowY;
    bool m_isMaximized;
    
    // Performance settings
    bool m_enableAnimations;
    bool m_enableTransparency;
    int m_imageCacheSize;
    
    // Privacy settings
    bool m_usageStatistics;
    bool m_crashReports;
    bool m_onlineCoverArt;
    
    // Platform settings
    QVariantMap m_platformSettings;
};

#endif // SETTINGS_H