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
    connect(m_settings, &Settings::settingsChanged, this, &SettingsModel::onSettingsChanged);
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
    m_settings->exportSettings(filePath);
}

bool SettingsModel::importSettings(const QString& filePath)
{
    return m_settings->importSettings(filePath);
}

void SettingsModel::save()
{
    m_settings->save();
}

QVariant SettingsModel::getValue(const QString& key, const QVariant& defaultValue)
{
    return m_settings->getValue(key, defaultValue);
}

void SettingsModel::setValue(const QString& key, const QVariant& value)
{
    m_settings->setValue(key, value);
}

void SettingsModel::onSettingsChanged()
{
    emit settingsChanged();
}