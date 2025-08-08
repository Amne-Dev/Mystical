#include "SettingsModel.h"
#include "../core/Settings.h"

SettingsModel::SettingsModel(Settings* settings, QObject* parent)
    : QObject(parent)
    , m_settings(settings)
{
    // Forward settings signals
    connect(m_settings, &Settings::isDarkModeChanged, this, &SettingsModel::isDarkModeChanged);
    connect(m_settings, &Settings::languageChanged, this, &SettingsModel::languageChanged);
    connect(m_settings, &Settings::startWithSystemChanged, this, &SettingsModel::startWithSystemChanged);
    connect(m_settings, &Settings::minimizeToTrayChanged, this, &SettingsModel::minimizeToTrayChanged);
    connect(m_settings, &Settings::autoScanGamesChanged, this, &SettingsModel::autoScanGamesChanged);
    connect(m_settings, &Settings::autoScanIntervalChanged, this, &SettingsModel::autoScanIntervalChanged);
    
    // Note: Removed settingsChanged signal connection as it doesn't exist in Settings class
    // If needed, this can be added to the Settings class later
}

bool SettingsModel::isDarkMode() const
{
    return m_settings->isDarkMode();
}

void SettingsModel::setIsDarkMode(bool dark)
{
    m_settings->setIsDarkMode(dark);
}

QString SettingsModel::language() const
{
    return m_settings->language();
}

void SettingsModel::setLanguage(const QString& language)
{
    m_settings->setLanguage(language);
}

bool SettingsModel::startWithSystem() const
{
    return m_settings->startWithSystem();
}

void SettingsModel::setStartWithSystem(bool start)
{
    m_settings->setStartWithSystem(start);
}

bool SettingsModel::minimizeToTray() const
{
    return m_settings->minimizeToTray();
}

void SettingsModel::setMinimizeToTray(bool minimize)
{
    m_settings->setMinimizeToTray(minimize);
}

bool SettingsModel::autoScanGames() const
{
    return m_settings->autoScanGames();
}

void SettingsModel::setAutoScanGames(bool autoScan)
{
    m_settings->setAutoScanGames(autoScan);
}

int SettingsModel::autoScanInterval() const
{
    return m_settings->autoScanInterval();
}

void SettingsModel::setAutoScanInterval(int minutes)
{
    m_settings->setAutoScanInterval(minutes);
}

QStringList SettingsModel::availableLanguages() const
{
    return QStringList() << "English" << "Français" << "Deutsch" << "Español" << "Italiano" << "русский" << "中文" << "日本語";
}

QStringList SettingsModel::availableThemes() const
{
    return QStringList() << "System" << "Light" << "Dark";
}

void SettingsModel::resetToDefaults()
{
    m_settings->resetToDefaults();
}

void SettingsModel::exportSettings(const QString& filePath)
{
    // Implementation placeholder - export functionality not available in Settings class
    Q_UNUSED(filePath)
    qWarning() << "Export settings functionality not implemented in Settings class";
}

bool SettingsModel::importSettings(const QString& filePath)
{
    // Implementation placeholder - import functionality not available in Settings class
    Q_UNUSED(filePath)
    qWarning() << "Import settings functionality not implemented in Settings class";
    return false;
}

void SettingsModel::save()
{
    m_settings->save();
}

QVariant SettingsModel::getValue(const QString& key, const QVariant& defaultValue)
{
    // This method likely needs to be implemented in the Settings class
    // For now, provide a placeholder implementation
    Q_UNUSED(key)
    Q_UNUSED(defaultValue)
    qWarning() << "getValue method not implemented in Settings class";
    return QVariant();
}

void SettingsModel::setValue(const QString& key, const QVariant& value)
{
    // This method likely needs to be implemented in the Settings class
    // For now, provide a placeholder implementation
    Q_UNUSED(key)
    Q_UNUSED(value)
    qWarning() << "setValue method not implemented in Settings class";
}

void SettingsModel::onSettingsChanged()
{
    emit settingsChanged();
}