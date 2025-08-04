#pragma once

#include <QObject>
#include <QAbstractListModel>

class Settings;

class SettingsModel : public QObject
{
    Q_OBJECT
    Q_PROPERTY(bool isDarkMode READ isDarkMode WRITE setIsDarkMode NOTIFY isDarkModeChanged)
    Q_PROPERTY(QString language READ language WRITE setLanguage NOTIFY languageChanged)
    Q_PROPERTY(bool startWithSystem READ startWithSystem WRITE setStartWithSystem NOTIFY startWithSystemChanged)
    Q_PROPERTY(bool minimizeToTray READ minimizeToTray WRITE setMinimizeToTray NOTIFY minimizeToTrayChanged)
    Q_PROPERTY(bool autoScanGames READ autoScanGames WRITE setAutoScanGames NOTIFY autoScanGamesChanged)
    Q_PROPERTY(int autoScanInterval READ autoScanInterval WRITE setAutoScanInterval NOTIFY autoScanIntervalChanged)
    Q_PROPERTY(QStringList availableLanguages READ availableLanguages CONSTANT)
    Q_PROPERTY(QStringList availableThemes READ availableThemes CONSTANT)
    
public:
    explicit SettingsModel(Settings* settings, QObject* parent = nullptr);
    
    // Properties
    bool isDarkMode() const;
    void setIsDarkMode(bool dark);
    
    QString language() const;
    void setLanguage(const QString& language);
    
    bool startWithSystem() const;
    void setStartWithSystem(bool start);
    
    bool minimizeToTray() const;
    void setMinimizeToTray(bool minimize);
    
    bool autoScanGames() const;
    void setAutoScanGames(bool autoScan);
    
    int autoScanInterval() const;
    void setAutoScanInterval(int minutes);
    
    // Lists for UI
    QStringList availableLanguages() const;
    QStringList availableThemes() const;
    
    // Actions
    Q_INVOKABLE void resetToDefaults();
    Q_INVOKABLE void exportSettings(const QString& filePath);
    Q_INVOKABLE bool importSettings(const QString& filePath);
    Q_INVOKABLE void save();
    
    // Advanced settings access
    Q_INVOKABLE QVariant getValue(const QString& key, const QVariant& defaultValue = QVariant());
    Q_INVOKABLE void setValue(const QString& key, const QVariant& value);
    
signals:
    void isDarkModeChanged();
    void languageChanged();
    void startWithSystemChanged();
    void minimizeToTrayChanged();
    void autoScanGamesChanged();
    void autoScanIntervalChanged();
    void settingsChanged();
    
private slots:
    void onSettingsChanged();
    
private:
    Settings* m_settings;
};
