import QtQuick 2.15
import QtQuick.Controls 2.15
import QtQuick.Layouts 1.15

Rectangle {
    id: root
    
    color: "transparent"
    
    // Signals
    signal settingChanged(string key, var value)
    signal resetRequested()
    signal importRequested()
    signal exportRequested()
    
    // Navigation state
    property string currentSection: "general"
    property var sectionList: [
        { key: "general", name: "General", icon: "⚙️" },
        { key: "appearance", name: "Appearance", icon: "🎨" },
        { key: "library", name: "Game Library", icon: "🎮" },
        { key: "platforms", name: "Platforms", icon: "🔧" },
        { key: "performance", name: "Performance", icon: "⚡" },
        { key: "privacy", name: "Privacy", icon: "🔒" },
        { key: "advanced", name: "Advanced", icon: "🔬" },
        { key: "about", name: "About", icon: "ℹ️" }
    ]
    
    RowLayout {
        anchors.fill: parent
        spacing: 0
        
        // Sidebar navigation
        Rectangle {
            Layout.preferredWidth: 240
            Layout.fillHeight: true
            color: Qt.rgba(1, 1, 1, 0.03)
            border.color: Qt.rgba(1, 1, 1, 0.08)
            border.width: 1
            
            ColumnLayout {
                anchors.fill: parent
                anchors.margins: 20
                spacing: 0
                
                // Header
                Text {
                    text: "Settings"
                    font.family: "Segoe UI Variable"
                    font.pixelSize: 20
                    font.weight: Font.Medium
                    color: "#ffffff"
                    Layout.bottomMargin: 20
                }
                
                // Navigation list
                ListView {
                    id: navigationList
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    
                    model: root.sectionList
                    spacing: 2
                    
                    delegate: Rectangle {
                        width: navigationList.width
                        height: 44
                        radius: 6
                        color: modelData.key === root.currentSection ? 
                               Qt.rgba(1, 1, 1, 0.1) : 
                               (mouseArea.containsMouse ? Qt.rgba(1, 1, 1, 0.05) : "transparent")
                        border.color: modelData.key === root.currentSection ? 
                                     Qt.rgba(1, 1, 1, 0.2) : "transparent"
                        border.width: 1
                        
                        Behavior on color {
                            ColorAnimation { duration: 150 }
                        }
                        
                        Behavior on border.color {
                            ColorAnimation { duration: 150 }
                        }
                        
                        RowLayout {
                            anchors.fill: parent
                            anchors.leftMargin: 12
                            anchors.rightMargin: 12
                            spacing: 12
                            
                            Text {
                                text: modelData.icon
                                font.pixelSize: 16
                                color: "#ffffff"
                            }
                            
                            Text {
                                text: modelData.name
                                font.family: "Segoe UI Variable"
                                font.pixelSize: 14
                                color: "#ffffff"
                                Layout.fillWidth: true
                            }
                        }
                        
                        MouseArea {
                            id: mouseArea
                            anchors.fill: parent
                            hoverEnabled: true
                            
                            onClicked: {
                                root.currentSection = modelData.key
                            }
                        }
                    }
                }
                
                // Footer actions
                Rectangle {
                    Layout.fillWidth: true
                    height: 1
                    color: Qt.rgba(1, 1, 1, 0.1)
                    Layout.topMargin: 15
                    Layout.bottomMargin: 15
                }
                
                Column {
                    Layout.fillWidth: true
                    spacing: 8
                    
                    Button {
                        width: parent.width
                        text: "Reset to Defaults"
                        
                        background: Rectangle {
                            color: parent.hovered ? Qt.rgba(1, 0, 0, 0.1) : "transparent"
                            border.color: Qt.rgba(1, 0, 0, 0.3)
                            border.width: 1
                            radius: 4
                        }
                        
                        contentItem: Text {
                            text: parent.text
                            color: "#ff6b6b"
                            font.pixelSize: 12
                            horizontalAlignment: Text.AlignHCenter
                            verticalAlignment: Text.AlignVCenter
                        }
                        
                        onClicked: root.resetRequested()
                    }
                }
            }
        }
        
        // Main content area
        Rectangle {
            Layout.fillWidth: true
            Layout.fillHeight: true
            color: "transparent"
            
            ScrollView {
                anchors.fill: parent
                anchors.margins: 30
                
                ScrollBar.horizontal.policy: ScrollBar.AlwaysOff
                ScrollBar.vertical.policy: ScrollBar.AsNeeded
                
                StackLayout {
                    id: contentStack
                    width: parent.width
                    currentIndex: {
                        for (var i = 0; i < root.sectionList.length; i++) {
                            if (root.sectionList[i].key === root.currentSection) {
                                return i
                            }
                        }
                        return 0
                    }
                    
                    // General Settings
                    SettingsSection {
                        title: "General Settings"
                        
                        SettingsGroup {
                            title: "Startup Behavior"
                            
                            SettingsRow {
                                label: "Start with Windows"
                                description: "Launch Mystical automatically when Windows starts"
                                
                                Switch {
                                    checked: appSettings ? appSettings.startWithSystem : false
                                    onToggled: {
                                        if (appSettings) {
                                            appSettings.startWithSystem = checked
                                        }
                                        root.settingChanged("startWithSystem", checked)
                                    }
                                }
                            }
                            
                            SettingsRow {
                                label: "Minimize to system tray"
                                description: "Hide to system tray instead of closing when window is closed"
                                
                                Switch {
                                    checked: appSettings ? appSettings.minimizeToTray : true
                                    onToggled: {
                                        if (appSettings) {
                                            appSettings.minimizeToTray = checked
                                        }
                                        root.settingChanged("minimizeToTray", checked)
                                    }
                                }
                            }
                            
                            SettingsRow {
                                label: "Check for updates"
                                description: "Automatically check for application updates"
                                
                                Switch {
                                    checked: appSettings ? appSettings.checkForUpdates : true
                                    onToggled: {
                                        if (appSettings) {
                                            appSettings.checkForUpdates = checked
                                        }
                                        root.settingChanged("checkForUpdates", checked)
                                    }
                                }
                            }
                        }
                        
                        SettingsGroup {
                            title: "Interface"
                            
                            SettingsRow {
                                label: "Language"
                                description: "Select your preferred language"
                                
                                ComboBox {
                                    model: ["English", "Español", "Français", "Deutsch", "中文", "日本語", "한국어"]
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
                                    
                                    onCurrentTextChanged: {
                                        root.settingChanged("language", currentText)
                                    }
                                }
                            }
                        }
                    }
                    
                    // Appearance Settings
                    SettingsSection {
                        title: "Appearance Settings"
                        
                        SettingsGroup {
                            title: "Theme"
                            
                            SettingsRow {
                                label: "Color theme"
                                description: "Choose between light, dark, or system theme"
                                
                                ComboBox {
                                    model: [
                                        { text: "System", value: "system" },
                                        { text: "Light", value: "light" },
                                        { text: "Dark", value: "dark" }
                                    ]
                                    textRole: "text"
                                    valueRole: "value"
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
                                    
                                    onCurrentValueChanged: {
                                        if (appSettings) {
                                            appSettings.isDarkMode = (currentValue === "dark")
                                        }
                                        root.settingChanged("theme", currentValue)
                                    }
                                }
                            }
                            
                            SettingsRow {
                                label: "Accent color"
                                description: "Customize the application accent color"
                                
                                Row {
                                    spacing: 10
                                    
                                    Rectangle {
                                        width: 32
                                        height: 24
                                        radius: 4
                                        color: "#0078d4"
                                        border.color: Qt.rgba(1, 1, 1, 0.3)
                                        border.width: 1
                                    }
                                    
                                    Button {
                                        text: "Change Color"
                                        
                                        background: Rectangle {
                                            color: Qt.rgba(1, 1, 1, 0.08)
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
                                            // Open color picker dialog
                                            console.log("Color picker requested")
                                        }
                                    }
                                }
                            }
                        }
                        
                        SettingsGroup {
                            title: "Visual Effects"
                            
                            SettingsRow {
                                label: "Enable animations"
                                description: "Show smooth transitions and animations"
                                
                                Switch {
                                    checked: true
                                    onToggled: root.settingChanged("enableAnimations", checked)
                                }
                            }
                            
                            SettingsRow {
                                label: "Transparency effects"
                                description: "Enable blur and transparency effects (may impact performance)"
                                
                                Switch {
                                    checked: true
                                    onToggled: root.settingChanged("enableTransparency", checked)
                                }
                            }
                        }
                    }
                    
                    // Game Library Settings
                    SettingsSection {
                        title: "Game Library Settings"
                        
                        SettingsGroup {
                            title: "Auto-Detection"
                            
                            SettingsRow {
                                label: "Auto-scan for games"
                                description: "Automatically detect new games periodically"
                                
                                Switch {
                                    checked: appSettings ? appSettings.autoScanGames : true
                                    onToggled: {
                                        if (appSettings) {
                                            appSettings.autoScanGames = checked
                                        }
                                        root.settingChanged("autoScanGames", checked)
                                    }
                                }
                            }
                            
                            SettingsRow {
                                label: "Scan interval"
                                description: "How often to check for new games (minutes)"
                                enabled: appSettings ? appSettings.autoScanGames : true
                                
                                SpinBox {
                                    from: 5
                                    to: 1440
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
                                        root.settingChanged("autoScanInterval", value)
                                    }
                                }
                            }
                        }
                        
                        SettingsGroup {
                            title: "Library Management"
                            
                            SettingsRow {
                                label: "Actions"
                                description: "Manage your game library"
                                
                                Row {
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
                                        text: "Clear Cache"
                                        
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
                                            console.log("Clear cache requested")
                                        }
                                    }
                                }
                            }
                            
                            SettingsRow {
                                label: "Import/Export"
                                description: "Backup or restore your game library"
                                
                                Row {
                                    spacing: 10
                                    
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
                                        
                                        onClicked: root.exportRequested()
                                    }
                                    
                                    Button {
                                        text: "Import Library"
                                        
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
                                        
                                        onClicked: root.importRequested()
                                    }
                                }
                            }
                        }
                    }
                    
                    // Platform Settings
                    SettingsSection {
                        title: "Platform Settings"
                        
                        Text {
                            text: "Configure platform-specific detection and launching settings"
                            font.pixelSize: 14
                            color: Qt.rgba(1, 1, 1, 0.7)
                            Layout.bottomMargin: 20
                        }
                        
                        SettingsGroup {
                            title: "Platform Detection"
                            
                            Repeater {
                                model: [
                                    { name: "Steam", icon: "🔧", enabled: true },
                                    { name: "Epic Games", icon: "🏪", enabled: true },
                                    { name: "GOG Galaxy", icon: "🎯", enabled: true },
                                    { name: "EA App", icon: "🎮", enabled: true },
                                    { name: "Minecraft", icon: "🧊", enabled: true },
                                    { name: "Roblox", icon: "🔷", enabled: false }
                                ]
                                
                                SettingsRow {
                                    label: modelData.name
                                    description: "Enable detection for " + modelData.name + " games"
                                    
                                    Row {
                                        spacing: 10
                                        
                                        Text {
                                            text: modelData.icon
                                            font.pixelSize: 16
                                            anchors.verticalCenter: parent.verticalCenter
                                        }
                                        
                                        Switch {
                                            checked: modelData.enabled
                                            onToggled: {
                                                root.settingChanged("platform_" + modelData.name, checked)
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    // Performance Settings
                    SettingsSection {
                        title: "Performance Settings"
                        
                        SettingsGroup {
                            title: "Resource Usage"
                            
                            SettingsRow {
                                label: "Background scanning"
                                description: "Allow background game scanning (may use more CPU)"
                                
                                Switch {
                                    checked: true
                                    onToggled: root.settingChanged("backgroundScanning", checked)
                                }
                            }
                            
                            SettingsRow {
                                label: "Image cache size"
                                description: "Maximum size for game cover art cache (MB)"
                                
                                SpinBox {
                                    from: 50
                                    to: 2000
                                    value: 500
                                    stepSize: 50
                                    
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
                                        root.settingChanged("imageCacheSize", value)
                                    }
                                }
                            }
                        }
                        
                        SettingsGroup {
                            title: "Hardware Acceleration"
                            
                            SettingsRow {
                                label: "GPU acceleration"
                                description: "Use GPU for UI rendering (restart required)"
                                
                                Switch {
                                    checked: true
                                    onToggled: root.settingChanged("gpuAcceleration", checked)
                                }
                            }
                        }
                    }
                    
                    // Privacy Settings
                    SettingsSection {
                        title: "Privacy Settings"
                        
                        SettingsGroup {
                            title: "Data Collection"
                            
                            SettingsRow {
                                label: "Anonymous usage statistics"
                                description: "Help improve Mystical by sending anonymous usage data"
                                
                                Switch {
                                    checked: false
                                    onToggled: root.settingChanged("usageStatistics", checked)
                                }
                            }
                            
                            SettingsRow {
                                label: "Crash reports"
                                description: "Automatically send crash reports to help fix issues"
                                
                                Switch {
                                    checked: true
                                    onToggled: root.settingChanged("crashReports", checked)
                                }
                            }
                        }
                        
                        SettingsGroup {
                            title: "Online Features"
                            
                            SettingsRow {
                                label: "Check for cover art online"
                                description: "Download missing game cover art from online databases"
                                
                                Switch {
                                    checked: true
                                    onToggled: root.settingChanged("onlineCoverArt", checked)
                                }
                            }
                            
                            SettingsRow {
                                label: "Game metadata"
                                description: "Download additional game information (descriptions, ratings, etc.)"
                                
                                Switch {
                                    checked: true
                                    onToggled: root.settingChanged("gameMetadata", checked)
                                }
                            }
                        }
                    }
                    
                    // Advanced Settings
                    SettingsSection {
                        title: "Advanced Settings"
                        
                        SettingsGroup {
                            title: "Debugging"
                            
                            SettingsRow {
                                label: "Enable debug logging"
                                description: "Create detailed logs for troubleshooting (restart required)"
                                
                                Switch {
                                    checked: false
                                    onToggled: root.settingChanged("debugLogging", checked)
                                }
                            }
                            
                            SettingsRow {
                                label: "Show developer tools"
                                description: "Enable developer options in the interface"
                                
                                Switch {
                                    checked: false
                                    onToggled: root.settingChanged("developerMode", checked)
                                }
                            }
                        }
                        
                        SettingsGroup {
                            title: "Custom Paths"
                            
                            SettingsRow {
                                label: "Additional game directories"
                                description: "Add custom directories to scan for games"
                                
                                Column {
                                    spacing: 10
                                    width: parent.width
                                    
                                    Button {
                                        text: "Add Directory"
                                        
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
                                            // Open directory picker
                                            console.log("Add directory requested")
                                        }
                                    }
                                    
                                    // List of custom directories would go here
                                    Text {
                                        text: "No custom directories added"
                                        color: Qt.rgba(1, 1, 1, 0.5)
                                        font.pixelSize: 12
                                        font.style: Font.Italic
                                    }
                                }
                            }
                        }
                    }
                    
                    // About Section
                    SettingsSection {
                        title: "About Mystical"
                        
                        Column {
                            spacing: 30
                            width: parent.width
                            
                            // App info
                            Row {
                                spacing: 20
                                
                                Image {
                                    source: "qrc:/resources/icons/mystical-icon.png"
                                    width: 64
                                    height: 64
                                    smooth: true
                                }
                                
                                Column {
                                    spacing: 8
                                    anchors.verticalCenter: parent.verticalCenter
                                    
                                    Text {
                                        text: "Mystical Game Library"
                                        font.family: "Segoe UI Variable"
                                        font.pixelSize: 20
                                        font.weight: Font.Medium
                                        color: "#ffffff"
                                    }
                                    
                                    Text {
                                        text: "Version 1.0.0"
                                        font.family: "Segoe UI Variable"
                                        font.pixelSize: 14
                                        color: Qt.rgba(1, 1, 1, 0.7)
                                    }
                                    
                                    Text {
                                        text: "Built with Qt " + Qt.version
                                        font.family: "Segoe UI Variable"
                                        font.pixelSize: 12
                                        color: Qt.rgba(1, 1, 1, 0.5)
                                    }
                                }
                            }
                            
                            // Description
                            Text {
                                width: parent.width
                                text: "A unified game library manager that brings all your games together in one beautiful interface. Built with Qt and modern design principles for Windows 11."
                                font.family: "Segoe UI Variable"
                                font.pixelSize: 14
                                color: Qt.rgba(1, 1, 1, 0.7)
                                wrapMode: Text.WordWrap
                            }
                            
                            // Links and actions
                            SettingsGroup {
                                title: "Links & Support"
                                
                                Row {
                                    spacing: 15
                                    
                                    Button {
                                        text: "GitHub Repository"
                                        flat: true
                                        
                                        contentItem: Text {
                                            text: parent.text
                                            color: "#0078d4"
                                            font.pixelSize: 13
                                            horizontalAlignment: Text.AlignHCenter
                                            verticalAlignment: Text.AlignVCenter
                                        }
                                        
                                        onClicked: {
                                            Qt.openUrlExternally("https://github.com/mystical-game-library")
                                        }
                                    }
                                    
                                    Button {
                                        text: "Documentation"
                                        flat: true
                                        
                                        contentItem: Text {
                                            text: parent.text
                                            color: "#0078d4"
                                            font.pixelSize: 13
                                            horizontalAlignment: Text.AlignHCenter
                                            verticalAlignment: Text.AlignVCenter
                                        }
                                        
                                        onClicked: {
                                            Qt.openUrlExternally("https://mystical-docs.example.com")
                                        }
                                    }
                                    
                                    Button {
                                        text: "Report Issue"
                                        flat: true
                                        
                                        contentItem: Text {
                                            text: parent.text
                                            color: "#0078d4"
                                            font.pixelSize: 13
                                            horizontalAlignment: Text.AlignHCenter
                                            verticalAlignment: Text.AlignVCenter
                                        }
                                        
                                        onClicked: {
                                            Qt.openUrlExternally("https://github.com/mystical-game-library/issues")
                                        }
                                    }
                                }
                                
                                SettingsRow {
                                    label: "Check for Updates"
                                    description: "Check if a new version is available"
                                    
                                    Button {
                                        text: "Check Now"
                                        
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
                                            console.log("Check for updates requested")
                                        }
                                    }
                                }
                            }
                            
                            // License and legal
                            SettingsGroup {
                                title: "Legal"
                                
                                Text {
                                    width: parent.width
                                    text: "© 2024 Mystical Game Library. Licensed under the MIT License."
                                    font.family: "Segoe UI Variable"
                                    font.pixelSize: 12
                                    color: Qt.rgba(1, 1, 1, 0.5)
                                    wrapMode: Text.WordWrap
                                }
                                
                                Row {
                                    spacing: 15
                                    
                                    Button {
                                        text: "View License"
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
                                            console.log("Show license requested")
                                        }
                                    }
                                    
                                    Button {
                                        text: "Third-Party Licenses"
                                        flat: true
                                        
                                        contentItem: Text {
                                            text: parent.text
                                            color: "#0078d4"
                                            font.pixelSize: 12
                                            horizontalAlignment: Text.AlignHCenter
                                            verticalAlignment: Text.AlignVCenter
                                        }
                                        
                                        onClicked: {
                                            // Show third-party licenses
                                            console.log("Show third-party licenses requested")
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    
    // Custom components for consistent settings layout
    component SettingsSection: ColumnLayout {
        property string title: ""
        
        spacing: 25
        Layout.fillWidth: true
        
        Text {
            text: title
            font.family: "Segoe UI Variable"
            font.pixelSize: 24
            font.weight: Font.Light
            color: "#ffffff"
            Layout.bottomMargin: 10
        }
    }
    
    component SettingsGroup: ColumnLayout {
        property string title: ""
        
        spacing: 15
        Layout.fillWidth: true
        Layout.bottomMargin: 20
        
        Text {
            text: title
            font.family: "Segoe UI Variable"
            font.pixelSize: 16
            font.weight: Font.Medium
            color: "#ffffff"
            Layout.bottomMargin: 5
        }
        
        Rectangle {
            Layout.fillWidth: true
            height: 1
            color: Qt.rgba(1, 1, 1, 0.1)
            Layout.bottomMargin: 10
        }
    }
    
    component SettingsRow: RowLayout {
        property string label: ""
        property string description: ""
        property bool enabled: true
        
        Layout.fillWidth: true
        Layout.bottomMargin: 15
        spacing: 20
        opacity: enabled ? 1.0 : 0.5
        
        Column {
            Layout.fillWidth: true
            Layout.maximumWidth: 300
            spacing: 4
            
            Text {
                text: label
                font.family: "Segoe UI Variable"
                font.pixelSize: 14
                font.weight: Font.Medium
                color: "#ffffff"
            }
            
            Text {
                width: parent.width
                text: description
                font.family: "Segoe UI Variable"
                font.pixelSize: 12
                color: Qt.rgba(1, 1, 1, 0.6)
                wrapMode: Text.WordWrap
                visible: description !== ""
            }
        }
    }
}