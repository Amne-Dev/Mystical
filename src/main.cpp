#include <QApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QQuickStyle>
#include <QIcon>
#include <QDir>
#include <QStandardPaths>
#include <QLoggingCategory>

#include "core/GameLibrary.h"
#include "core/Settings.h"
#include "models/GameLibraryModel.h"
#include "models/SettingsModel.h"
#include "ui/MysticalStyle.h"

Q_LOGGING_CATEGORY(mystical, "mystical")

int main(int argc, char *argv[])
{
    QApplication app(argc, argv);
    
    // Set application properties
    app.setApplicationName("Mystical");
    app.setApplicationVersion("1.0.0");
    app.setOrganizationName("Mystical Games");
    app.setApplicationDisplayName("Mystical Game Library");
    
    // Set application icon
    app.setWindowIcon(QIcon(":/resources/icons/mystical-icon.png"));
    
    // Apply custom Windows 11 style
    app.setStyle(new MysticalStyle());
    
    // Configure Qt Quick style for Windows 11
    QQuickStyle::setStyle("Windows");
    
    // Enable high DPI support
    app.setAttribute(Qt::AA_EnableHighDpiScaling);
    app.setAttribute(Qt::AA_UseHighDpiPixmaps);
    
    // Initialize core components
    Settings* settings = new Settings(&app);
    GameLibrary* gameLibrary = new GameLibrary(&app);
    
    // Initialize models
    GameLibraryModel* gameLibraryModel = new GameLibraryModel(gameLibrary, &app);
    SettingsModel* settingsModel = new SettingsModel(settings, &app);
    
    // Setup QML engine
    QQmlApplicationEngine engine;
    
    // Register types with QML
    qmlRegisterType<GameLibraryModel>("Mystical", 1, 0, "GameLibraryModel");
    qmlRegisterType<SettingsModel>("Mystical", 1, 0, "SettingsModel");
    
    // Expose models to QML context
    engine.rootContext()->setContextProperty("gameLibraryModel", gameLibraryModel);
    engine.rootContext()->setContextProperty("settingsModel", settingsModel);
    engine.rootContext()->setContextProperty("gameLibrary", gameLibrary);
    engine.rootContext()->setContextProperty("appSettings", settings);
    
    // Set up resource paths
    engine.addImportPath(":/qml");
    engine.addImportPath("qrc:/qml");
    
    // Load main QML file
    const QUrl url(QStringLiteral("qrc:/qml/main.qml"));
    QObject::connect(&engine, &QQmlApplicationEngine::objectCreated,
                     &app, [url](QObject *obj, const QUrl &objUrl) {
        if (!obj && url == objUrl) {
            qCritical(mystical) << "Failed to load QML file:" << url;
            QApplication::exit(-1);
        }
    }, Qt::QueuedConnection);
    
    engine.load(url);
    
    if (engine.rootObjects().isEmpty()) {
        qCritical(mystical) << "No root objects found in QML";
        return -1;
    }
    
    // Initialize game library (scan for games)
    QObject::connect(&app, &QApplication::aboutToQuit, [&]() {
        qInfo(mystical) << "Application shutting down";
        settings->save();
    });
    
    // Start background game detection
    gameLibrary->startInitialScan();
    
    qInfo(mystical) << "Mystical Game Library started successfully";
    
    return app.exec();
}