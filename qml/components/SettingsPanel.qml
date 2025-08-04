import QtQuick 2.15
import QtQuick.Controls 2.15
import QtQuick.Layouts 1.15

Rectangle {
    id: root
    
    signal closeRequested()
    
    color: Qt.rgba(0.1, 0.1, 0.1, 0.95)
    border.color: Qt.rgba(1, 1, 1, 0.1)
    border.width: 1
    
    // Backdrop blur effect would be applied here in a real implementation
    
    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 20
        spacing: 0
        
        // Header
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 50
            color: "transparent"
            
            RowLayout {
                anchors.fill: parent
                
                Text {
                    text: "Settings"
                    font.family: "Segoe UI Variable"
                    font.pixelSize: 20
                    font.weight: Font.Medium
                    color: "#ffffff"
                }
                
                Item { Layout.fillWidth: true }
                
                Button {
                    Layout.preferredWidth: 32
                    Layout.preferredHeight: 32
                    
                    background: Rectangle {
                        color: parent.hovered ? Qt.rgba(1, 1, 1, 0.1) : "transparent"
                        radius: 4
                    }
                    
                    contentItem: Text {
                        text: "✕"
                        color: "#ffffff"
                        font.pixelSize: 14
                        horizontalAlignment: Text.AlignHCenter
                        verticalAlignment: Text.AlignVCenter
                    }
                    
                    onClicked: root.closeRequested()
                }
            }
        }
        
        // Settings content
        ScrollView {
            Layout.fillWidth: true
            Layout.fillHeight: true
            
            ScrollBar.horizontal.policy: ScrollBar.AlwaysOff
            
            ColumnLayout {
                width: root.width - 40
                spacing: 30
                
                // Appearance section
                SettingsSection {
                    title: "Appearance"
                    icon: "🎨"
                    
                    SettingsRow {
                        label: "Theme"
                        description: "Choose between light and dark themes"
                        
                        ComboBox {
                            model: ["System", "Light", "Dark"]
                            currentIndex: {
                                if (!appSettings) return 2
                                return appSettings.isDarkMode ? 2 : 1
                            }
                            
                            background: Rectangle {
                                color: Qt.rgba(1, 1, 1, 0.08)
                                border.color: Qt.rgba(1, 1, 1, 0.2)
                                border.width: 1
                                radius: 4
                            }
                            
                            contentItem: Text {
                                text: parent.displayText
                                color: "#ffffff"
                                font.pixelSize: 13
                                leftPadding: 10
                                verticalAlignment: Text.AlignVCenter
                            }
                            
                            onCurrentTextChanged: {
                                if (appSettings) {
                                    appSettings.isDarkMode = (currentText === "Dark")
                                }
                            }
                        }
                    }
                    
                    SettingsRow {
                        label: "Language"
                        description: "Select your preferred language"
                        
                        ComboBox {
                            model: ["English", "Español", "Français", "Deutsch", "中文"]
                            currentIndex: 0
                            
                            background: Rectangle {
                                color: Qt.rgba(1, 1, 1, 0.08)
                                border.color: Qt.rgba(1, 1, 1, 0.2)
                                border.width: 1
                                radius: 4
                            }
                            
                            contentItem: Text {
                                text: parent.displayText
                                color: "#ffffff"
                                font.pixelSize: 13
                                leftPadding: 10
                                verticalAlignment: Text.AlignVCenter
                            }
                        }
                    }
                }
                
                // Behavior section
                SettingsSection {
                    title: "Behavior"
                    icon: "⚙️"
                    
                    SettingsRow {
                        label: "Start with system"
                        description: "Launch Mystical when Windows starts"
                        
                        Switch {
                            checked: appSettings ? appSettings.startWithSystem : false
                            
                            onToggled: {
                                if (appSettings) {
                                    appSettings.startWithSystem = checked
                                }
                            }
                        }
                    }
                    
                    SettingsRow {
                        label: "Minimize to tray"
                        description: "Hide to system tray when window is closed"
                        
                        Switch {
                            checked: appSettings ? appSettings.minimizeToTray : true
                            
                            onToggled: {
                                if (appSettings) {
                                    appSettings.minimizeToTray = checked
                                }
                            }
                        }
                    }
                }
                
                // Game Library section
                SettingsSection {
                    title: "Game Library"
                    icon: "🎮"
                    
                    SettingsRow {
                        label: "Auto-scan for games"
                        description: "Automatically detect new games periodically"
                        
                        Switch {
                            checked: appSettings ? appSettings.autoScanGames : true
                            
                            onToggled: {
                                if (appSettings) {
                                    appSettings.autoScanGames = checked
                                }
                            }
                        }
                    }
                    
                    SettingsRow {
                        label: "Scan interval"
                        description: "How often to check for new games (minutes)"
                        enabled: appSettings ? appSettings.autoScanGames : true
                        
                        SpinBox {
                            from: 5
                            to: 240
                            value: appSettings ? appSettings.autoScanInterval : 30
                            stepSize: 5
                            
                            background: Rectangle {
                                color: Qt.rgba(1, 1, 1, 0.08)
                                border.color: Qt.rgba(1, 1, 1, 0.2)
                                border.width: 1
                                radius: 4
                            }
                            
                            contentItem: TextInput {
                                text: parent.textFromValue(parent.value, parent.locale)
                                font.pixelSize: 13
                                color: "#ffffff"
                                horizontalAlignment: Qt.AlignHCenter
                                verticalAlignment: Qt.AlignVCenter
                                readOnly: !parent.editable
                                validator: parent.validator
                                inputMethodHints: parent.inputMethodHints
                            }
                            
                            onValueChanged: {
                                if (appSettings) {
                                    appSettings.autoScanInterval = value
                                }
                            }
                        }
                    }
                    
                    SettingsRow {
                        label: "Game Library Actions"
                        description: "Manage your game library"
                        
                        Column {
                            spacing: 10
                            
                            Button {
                                text: "Scan Now"
                                
                                background: Rectangle {
                                    color: "#0078d4"
                                    radius: 4
                                    opacity: parent.hovered ? 0.8 : 1.0
                                }
                                
                                contentItem: Text {
                                    text: parent.text
                                    color: "white"
                                    font.pixelSize: 12
                                    horizontalAlignment: Text.AlignHCenter
                                    verticalAlignment: Text.AlignVCenter
                                }
                                
                                onClicked: {
                                    if (gameLibrary) {
                                        gameLibrary.startFullScan()
                                    }
                                }
                            }
                            
                            Button {
                                text: "Export Library"
                                
                                background: Rectangle {
                                    color: Qt.rgba(1, 1, 1, 0.1)
                                    border.color: Qt.rgba(1, 1, 1, 0.2)
                                    border.width: 1
                                    radius: 4
                                    opacity: parent.hovered ? 0.8 : 1.0
                                }
                                
                                contentItem: Text {
                                    text: parent.text
                                    color: "white"
                                    font.pixelSize: 12
                                    horizontalAlignment: Text.AlignHCenter
                                    verticalAlignment: Text.AlignVCenter
                                }
                                
                                onClicked: {
                                    // Handle export
                                    console.log("Export library clicked")
                                }
                            }
                        }
                    }
                }
                
                // About section
                SettingsSection {
                    title: "About"
                    icon: "ℹ️"
                    
                    Column {
                        spacing: 15
                        width: parent.width
                        
                        Text {
                            text: "Mystical Game Library v1.0.0"
                            font.family: "Segoe UI Variable"
                            font.pixelSize: 16
                            font.weight: Font.Medium
                            color: "#ffffff"
                        }
                        
                        Text {
                            width: parent.width
                            text: "A unified game library manager that brings all your games together in one beautiful interface. Built with Qt and modern design principles."
                            font.family: "Segoe UI Variable"
                            font.pixelSize: 13
                            color: Qt.rgba(1, 1, 1, 0.7)
                            wrapMode: Text.WordWrap
                        }
                        
                        Row {
                            spacing: 15
                            
                            Button {
                                text: "GitHub"
                                flat: true
                                
                                contentItem: Text {
                                    text: parent.text
                                    color: "#0078d4"
                                    font.pixelSize: 12
                                    horizontalAlignment: Text.AlignHCenter
                                    verticalAlignment: Text.AlignVCenter
                                }
                                
                                onClicked: {
                                    Qt.openUrlExternally("https://github.com/mystical-game-library")
                                }
                            }
                            
                            Button {
                                text: "License"
                                flat: true
                                
                                contentItem: Text {
                                    text: parent.text
                                    color: "#0078d4"
                                    font.pixelSize: 12
                                    horizontalAlignment: Text.AlignHCenter
                                    verticalAlignment: Text.AlignVCenter
                                }
                                
                                onClicked: {
                                    // Show license dialog
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    
    // Custom components for settings layout
    component SettingsSection: Column {
        property string title: ""
        property string icon: ""
        
        Layout.fillWidth: true
        spacing: 15
        
        Row {
            spacing: 10
            
            Text {
                text: icon
                font.pixelSize: 16
                anchors.verticalCenter: parent.verticalCenter
            }
            
            Text {
                text: title
                font.family: "Segoe UI Variable"
                font.pixelSize: 16
                font.weight: Font.Medium
                color: "#ffffff"
                anchors.verticalCenter: parent.verticalCenter
            }
        }
        
        Rectangle {
            width: parent.width
            height: 1
            color: Qt.rgba(1, 1, 1, 0.1)
        }
    }
    
    component SettingsRow: RowLayout {
        property string label: ""
        property string description: ""
        property bool enabled: true
        
        Layout.fillWidth: true
        spacing: 15
        opacity: enabled ? 1.0 : 0.5
        
        Column {
            Layout.fillWidth: true
            spacing: 4
            
            Text {
                text: label
                font.family: "Segoe UI Variable"
                font.pixelSize: 14
                font.weight: Font.Medium
                color: "#ffffff"
            }
            
            Text {
                text: description
                font.family: "Segoe UI Variable"
                font.pixelSize: 12
                color: Qt.rgba(1, 1, 1, 0.6)
                wrapMode: Text.WordWrap
            }
        }
    }
}