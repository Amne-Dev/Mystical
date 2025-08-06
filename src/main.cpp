#include <QApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QIcon>
#include <QDir>
#include <QStandardPaths>
#include <QLoggingCategory>
#include <QDebug>

// Core classes
#include "core/GameLibrary.h"
#include "core/Settings.h"

// Models
#include "models/GameLibraryModel.h"
#include "models/SettingsModel.h"

// UI
#include "ui/MysticalStyle.h"

// Utils
#include "utils/ImageCache.h"

Q_LOGGING_CATEGORY(mystical, "mystical")

int main(int argc, char *argv[])
{
    // Enable high DPI support (automatic in Qt 6.9+)
    QApplication app(argc, argv);
    
    // Set application properties
    app.setApplicationName("Mystical");
    app.setApplicationVersion("1.0.0");
    app.setOrganizationName("Mystical Game Library");
    app.setOrganizationDomain("mystical-game-library.com");
    app.setApplicationDisplayName("Mystical Game Library");
    
    // Set application icon
    app.setWindowIcon(QIcon(":/resources/icons/mystical-icon.png"));
    
    // Apply custom style for Windows 11 integration
    auto *mysticalStyle = new MysticalStyle();
    app.setStyle(mysticalStyle);
    
    // Initialize core components
    auto *settings     = new Settings(&app);
    auto *gameLibrary  = new GameLibrary(&app);
    auto *imageCache   = new ImageCache(&app);
    
    // Initialize models — pass the core objects directly
    auto *gameLibraryModel = new GameLibraryModel(gameLibrary, &app);
    auto *settingsModel    = new SettingsModel(settings,    &app);
    
    // (No calls to setGameLibrary() or setSettings() — these methods do not exist)
    
    // Create QML engine
    QQmlApplicationEngine engine;
    
    // Register types for QML (if you need to instantiate in QML)
    qmlRegisterType<GameLibraryModel>("Mystical", 1, 0, "GameLibraryModel");
    qmlRegisterType<SettingsModel>   ("Mystical", 1, 0, "SettingsModel");
    
    // Expose C++ objects to QML
    auto *rootContext = engine.rootContext();
    rootContext->setContextProperty("gameLibrary",      gameLibrary);
    rootContext->setContextProperty("gameLibraryModel", gameLibraryModel);
    rootContext->setContextProperty("appSettings",      settingsModel);
    rootContext->setContextProperty("imageCache",       imageCache);
    
    // Set up resource paths
    rootContext->setContextProperty("resourcePath", "qrc:/resources/");
    
    // Load main QML file
    const QUrl url(QStringLiteral("qrc:/qml/main.qml"));
    QObject::connect(&engine, &QQmlApplicationEngine::objectCreated,
                     &app, [url](QObject *obj, const QUrl &objUrl) {
        if (!obj && url == objUrl) {
            qCritical() << "Failed to load main QML file";
            QCoreApplication::exit(-1);
        }
    }, Qt::QueuedConnection);
    
    QObject::connect(&engine, &QQmlApplicationEngine::warnings,
                     &app, [](const QList<QQmlError> &warnings) {
        for (const auto &warning : warnings) {
            qWarning() << "QML Warning:" << warning.toString();
        }
    });
    
    engine.load(url);
    if (engine.rootObjects().isEmpty()) {
        qCritical() << "No root objects found in QML";
        return -1;
    }
    
    // Save settings on exit
    QObject::connect(&app, &QApplication::aboutToQuit, [&]() {
        qDebug() << "Application shutting down...";
        settings->save();
    });
    
    // Start initial game scan
    gameLibrary->startInitialScan();
    
    qDebug() << "Mystical Game Library started successfully";
    return app.exec();
}
