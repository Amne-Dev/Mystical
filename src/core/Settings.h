#pragma once

#include <QObject>
#include <QSettings>
#include <QVariant>

class Settings : public QObject
{
    Q_OBJECT
    Q_PROPERTY(bool isDarkMode READ isDarkMode WRITE setIsDarkMode NOTIFY isDarkModeChanged)
    Q_PROPERTY(QString language READ language WRITE setLanguage NOTIFY languageChanged)
    Q_PROPERTY(bool startWithSystem READ startWithSystem WRITE setStartWithSystem NOTIFY startWithSystemChanged)
    Q_PROPERTY(bool minimizeToTray READ minimizeToTray WRITE setMinimizeToTray NOTIFY minimizeToTrayChanged)
    Q_PROPERTY(bool autoScanGames READ autoScanGames WRITE setAutoScanGames NOTIFY autoScanGamesChanged)
    Q_PROPERTY(int autoScanInterval READ autoScanInterval WRITE setAutoScanInterval NOTIFY autoScanIntervalChanged)
    Q_PROPERTY(int windowWidth READ windowWidth WRITE setWindowWidth NOTIFY windowWidthChanged)
    Q_PROPERTY(int windowHeight READ windowHeight WRITE setWindowHeight NOTIFY windowHeightChanged)
    Q_PROPERTY(bool windowMaximized READ windowMaximized WRITE setWindowMaximized NOTIFY windowMaximizedChanged)
    
public:
    explicit Settings(QObject* parent = nullptr);
    ~Settings();
    
    // Theme settings
    bool isDarkMode() const { return m_isDarkMode; }
    void setIsDarkMode(bool dark);
    
    // Language settings
    QString language() const { return m_language; }
    void setLanguage(const QString& language);
    
    // Startup behavior
    bool startWithSystem() const { return m_startWithSystem; }
    void setStartWithSystem(bool start);
    
    bool minimizeToTray() const { return m_minimizeToTray; }
    void setMinimizeToTray(bool minimize);
    
    // Game scanning
    bool autoScanGames() const { return m_autoScanGames; }
    void setAutoScanGames(bool autoScan);
    
    int autoScanInterval() const { return m_autoScanInterval; }
    void setAutoScanInterval(int minutes);
    
    // Window state
    int windowWidth() const { return m_windowWidth; }
    void setWindowWidth(int width);
    
    int windowHeight() const { return m_windowHeight; }
    void setWindowHeight(int height);
    
    bool windowMaximized() const { return m_windowMaximized; }
    void setWindowMaximized(bool maximized);
    
    // Advanced settings
    Q_INVOKABLE QVariant getValue(const QString& key, const QVariant& defaultValue = QVariant()) const;
    Q_INVOKABLE void setValue(const QString& key, const QVariant& value);
    Q_INVOKABLE bool contains(const QString& key) const;
    Q_INVOKABLE void remove(const QString& key);
    
    // Bulk operations
    Q_INVOKABLE void resetToDefaults();
    Q_INVOKABLE void exportSettings(const QString& filePath) const;
    Q_INVOKABLE bool importSettings(const QString& filePath);
    
public slots:
    void save();
    void load();
    void sync();
    
signals:
    void isDarkModeChanged();
    void languageChanged();
    void startWithSystemChanged();
    void minimizeToTrayChanged();
    void autoScanGamesChanged();
    void autoScanIntervalChanged();
    void windowWidthChanged();
    void windowHeightChanged();
    void windowMaximizedChanged();
    void settingsChanged();
    
private:
    void loadDefaults();
    void setupSystemStartup();
    void removeSystemStartup();
    QString getStartupRegistryPath() const;
    
    QSettings* m_settings;
    
    // Cached values for performance
    bool m_isDarkMode;
    QString m_language;
    bool m_startWithSystem;
    bool m_minimizeToTray;
    bool m_autoScanGames;
    int m_autoScanInterval;
    int m_windowWidth;
    int m_windowHeight;
    bool m_windowMaximized;
    
    // Default values
    static const bool DEFAULT_DARK_MODE = true;
    static const QString DEFAULT_LANGUAGE;
    static const bool DEFAULT_START_WITH_SYSTEM = false;
    static const bool DEFAULT_MINIMIZE_TO_TRAY = true;
    static const bool DEFAULT_AUTO_SCAN_GAMES = true;
    static const int DEFAULT_AUTO_SCAN_INTERVAL = 30; // minutes
    static const int DEFAULT_WINDOW_WIDTH = 1200;
    static const int DEFAULT_WINDOW_HEIGHT = 800;
    static const bool DEFAULT_WINDOW_MAXIMIZED = false;
};