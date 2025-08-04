import QtQuick 2.15
import QtQuick.Controls 2.15
import QtQuick.Window 2.15
import QtQuick.Layouts 1.15
import QtQuick.Effects
import "components" as Components
import "pages" as Pages
import "styles"

ApplicationWindow {
    id: mainWindow
    
    width: 1200
    height: 800
    minimumWidth: 900
    minimumHeight: 600
    
    visible: true
    title: "Mystical Game Library"
    
    // Windows 11 styling
    flags: Qt.Window | Qt.WindowMinimizeButtonHint | Qt.WindowMaximizeButtonHint | Qt.WindowCloseButtonButtonHint
    color: "transparent"
    
    // Theme and settings
    property bool isDarkMode: appSettings ? appSettings.isDarkMode : true
    property color backgroundColor: isDarkMode ? "#1e1e1e" : "#f3f3f3"
    property color surfaceColor: isDarkMode ? "#2d2d30" : "#ffffff" 
    property color accentColor: "#0078d4"
    property color textColor: isDarkMode ? "#ffffff" : "#000000"
    property color secondaryTextColor: isDarkMode ? "#cccccc" : "#666666"
    
    // Animation properties
    property int animationDuration: 200
    property var easingCurve: Easing.OutQuart
    
    // Application state
    property bool isLoading: gameLibrary ? gameLibrary.isScanning : false
    property string currentPage: "library"
    property bool showSettings: false
    
    // Background with acrylic effect
    Rectangle {
        anchors.fill: parent
        color: backgroundColor
        
        // Acrylic backdrop effect
        Rectangle {
            anchors.fill: parent
            color: Qt.rgba(0.1, 0.1, 0.1, 0.3)
            visible: isDarkMode
            
            layer.enabled: true
            layer.effect: MultiEffect {
                blurEnabled: true
                blur: 0.8
                blurMax: 64
                autoPaddingEnabled: false
            }
        }
        
        // Subtle gradient overlay
        Rectangle {
            anchors.fill: parent
            gradient: Gradient {
                GradientStop { 
                    position: 0.0
                    color: Qt.rgba(1, 1, 1, isDarkMode ? 0.02 : 0.1) 
                }
                GradientStop { 
                    position: 1.0 
                    color: Qt.rgba(0, 0, 0, isDarkMode ? 0.1 : 0.02) 
                }
            }
        }
    }
    
    // Main content area
    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 20
        spacing: 0
        
        // Title bar area
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 60
            color: "transparent"
            
            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 10
                anchors.rightMargin: 10
                spacing: 20
                
                // App title and logo
                RowLayout {
                    spacing: 15
                    
                    Image {
                        source: "qrc:/resources/icons/mystical-icon.png"
                        Layout.preferredWidth: 32
                        Layout.preferredHeight: 32
                        smooth: true
                    }
                    
                    Text {
                        text: "Mystical"
                        font.family: "Segoe UI Variable"
                        font.pixelSize: 24
                        font.weight: Font.Light
                        color: textColor
                    }
                }
                
                // Search bar
                Components.SearchBar {
                    id: searchBar
                    Layout.fillWidth: true
                    Layout.maximumWidth: 400
                    Layout.preferredHeight: 36
                    
                    onSearchTextChanged: {
                        if (gameLibraryModel) {
                            gameLibraryModel.setFilterText(searchText)
                        }
                    }
                }
                
                // Action buttons
                RowLayout {
                    spacing: 10
                    
                    // Refresh button
                    Button {
                        id: refreshButton
                        Layout.preferredWidth: 36
                        Layout.preferredHeight: 36
                        
                        background: Rectangle {
                            color: parent.hovered ? Qt.rgba(1, 1, 1, 0.1) : "transparent"
                            radius: 4
                            border.color: parent.hovered ? Qt.rgba(1, 1, 1, 0.2) : "transparent"
                            border.width: 1
                            
                            Behavior on color {
                                ColorAnimation { duration: animationDuration }
                            }
                        }
                        
                        contentItem: Text {
                            text: "⟳"
                            font.pixelSize: 16
                            color: textColor
                            horizontalAlignment: Text.AlignHCenter
                            verticalAlignment: Text.AlignVCenter
                        }
                        
                        onClicked: {
                            if (gameLibrary) {
                                gameLibrary.startInitialScan()
                            }
                        }
                        
                        enabled: !isLoading
                        
                        ToolTip.visible: hovered
                        ToolTip.text: "Refresh game library"
                        ToolTip.delay: 1000
                    }
                    
                    // Settings button
                    Button {
                        id: settingsButton
                        Layout.preferredWidth: 36
                        Layout.preferredHeight: 36
                        
                        background: Rectangle {
                            color: parent.hovered ? Qt.rgba(1, 1, 1, 0.1) : "transparent"
                            radius: 4
                            border.color: parent.hovered ? Qt.rgba(1, 1, 1, 0.2) : "transparent"
                            border.width: 1
                            
                            Behavior on color {
                                ColorAnimation { duration: animationDuration }
                            }
                        }
                        
                        contentItem: Text {
                            text: "⚙"
                            font.pixelSize: 16
                            color: textColor
                            horizontalAlignment: Text.AlignHCenter
                            verticalAlignment: Text.AlignVCenter
                        }
                        
                        onClicked: showSettings = !showSettings
                        
                        ToolTip.visible: hovered  
                        ToolTip.text: "Settings"
                        ToolTip.delay: 1000
                    }
                }
            }
        }
        
        // Loading indicator
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 4
            color: "transparent"
            visible: isLoading
            
            Rectangle {
                id: loadingBar
                height: parent.height
                width: parent.width * 0.3
                color: accentColor
                radius: 2
                
                SequentialAnimation on x {
                    running: isLoading
                    loops: Animation.Infinite
                    
                    NumberAnimation {
                        from: -loadingBar.width
                        to: parent.width
                        duration: 2000
                        easing.type: Easing.InOutQuad
                    }
                }
            }
        }
        
        // Main content
        Item {
            Layout.fillWidth: true
            Layout.fillHeight: true
            
            // Library page
            Pages.LibraryPage {
                id: libraryPage
                anchors.fill: parent
                visible: currentPage === "library"
            }
            
            // Settings panel (overlay)
            Components.SettingsPanel {
                id: settingsPanel
                anchors.right: parent.right
                anchors.top: parent.top
                anchors.bottom: parent.bottom
                width: 320
                
                visible: showSettings
                
                Behavior on x {
                    NumberAnimation {
                        duration: animationDuration
                        easing.type: easingCurve
                    }
                }
                
                onCloseRequested: showSettings = false
            }
        }
    }
    
    // Loading overlay for initial startup
    Rectangle {
        anchors.fill: parent
        color: Qt.rgba(0, 0, 0, 0.7)
        visible: gameLibrary ? gameLibrary.isInitializing : true
        
        Column {
            anchors.centerIn: parent
            spacing: 20
            
            BusyIndicator {
                anchors.horizontalCenter: parent.horizontalCenter
                running: parent.parent.visible
                
                contentItem: Item {
                    implicitWidth: 48
                    implicitHeight: 48
                    
                    Item {
                        id: item
                        x: parent.width / 2 - 24
                        y: parent.height / 2 - 24
                        width: 48
                        height: 48
                        opacity: parent.parent.running ? 1 : 0
                        
                        Behavior on opacity {
                            OpacityAnimator {
                                duration: 250
                            }
                        }
                        
                        RotationAnimator {
                            target: item
                            running: parent.parent.running
                            from: 0
                            to: 360
                            loops: Animation.Infinite
                            duration: 1250
                        }
                        
                        Repeater {
                            id: repeater
                            model: 6
                            
                            Rectangle {
                                x: item.width / 2 - width / 2
                                y: item.height / 2 - height / 2
                                implicitWidth: 8
                                implicitHeight: 8
                                radius: 4
                                color: accentColor
                                transform: [
                                    Translate {
                                        y: -Math.min(item.width, item.height) * 0.5 + 8
                                    },
                                    Rotation {
                                        angle: index / repeater.count * 360
                                        origin.x: 4
                                        origin.y: 4
                                    }
                                ]
                            }
                        }
                    }
                }
            }
            
            Text {
                anchors.horizontalCenter: parent.horizontalCenter
                text: "Scanning for games..."
                font.family: "Segoe UI Variable"
                font.pixelSize: 16
                color: textColor
            }
        }
    }
    
    // Connection to backend signals
    Connections {
        target: gameLibrary
        
        function onScanningChanged() {
            // Update loading state
        }
        
        function onInitializationComplete() {
            // Hide loading overlay
        }
        
        function onErrorOccurred(message) {
            console.error("Game library error:", message)
            // Could show error dialog here
        }
    }
    
    // Window state management
    onClosing: {
        if (appSettings) {
            appSettings.save()
        }
    }
    
    Component.onCompleted: {
        console.log("Mystical Game Library initialized")
        
        // Apply saved window geometry if available
        if (appSettings) {
            if (appSettings.windowWidth > 0) {
                width = appSettings.windowWidth
            }
            if (appSettings.windowHeight > 0) {
                height = appSettings.windowHeight
            }
        }
    }
}