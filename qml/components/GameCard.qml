import QtQuick 2.15
import QtQuick.Controls 2.15
import QtQuick.Layouts 1.15
import QtQuick.Effects

Item {
    id: root
    
    // Public properties
    property string gameTitle: ""
    property string platform: ""
    property string coverArt: ""
    property string playtime: ""
    property bool isFavorite: false
    property bool isInstalled: true
    property color accentColor: "#0078d4"
    
    // Size properties
    property real cardWidth: 200
    property real cardHeight: 280
    property real imageHeight: 200
    property real infoHeight: 80
    
    // Internal state
    property bool hovered: mouseArea.containsMouse
    property bool pressed: mouseArea.pressed
    
    // Signals
    signal clicked()
    signal rightClicked()
    signal playRequested()
    signal favoriteToggled()
    
    width: cardWidth
    height: cardHeight
    
    // Main card container
    Rectangle {
        id: cardBackground
        anchors.fill: parent
        radius: 8
        color: Qt.rgba(1, 1, 1, hovered ? 0.08 : 0.04)
        border.color: Qt.rgba(1, 1, 1, hovered ? 0.2 : 0.1)
        border.width: 1
        
        // Hover elevation effect
        layer.enabled: hovered
        layer.effect: MultiEffect {
            shadowEnabled: true
            shadowColor: Qt.rgba(0, 0, 0, 0.3)
            shadowBlur: 0.2
            shadowOffsetX: 0
            shadowOffsetY: 4
        }
        
        // Smooth transitions
        Behavior on color {
            ColorAnimation { duration: 200 }
        }
        
        Behavior on border.color {
            ColorAnimation { duration: 200 }
        }
        
        // Scale animation on press
        scale: pressed ? 0.98 : (hovered ? 1.02 : 1.0)
        
        Behavior on scale {
            NumberAnimation {
                duration: 150
                easing.type: Easing.OutQuart
            }
        }
        
        Column {
            anchors.fill: parent
            spacing: 0
            
            // Cover art section
            Rectangle {
                width: parent.width
                height: imageHeight
                radius: 8
                color: Qt.rgba(0, 0, 0, 0.1)
                clip: true
                
                // Cover art image
                Image {
                    id: coverImage
                    anchors.fill: parent
                    source: coverArt || "qrc:/resources/images/default-cover.png"
                    fillMode: Image.PreserveAspectCrop
                    smooth: true
                    asynchronous: true
                    
                    // Loading placeholder
                    Rectangle {
                        anchors.fill: parent
                        color: Qt.rgba(0.5, 0.5, 0.5, 0.2)
                        visible: coverImage.status === Image.Loading
                        
                        BusyIndicator {
                            anchors.centerIn: parent
                            width: 32
                            height: 32
                            running: parent.visible
                        }
                    }
                    
                    // Error placeholder
                    Rectangle {
                        anchors.fill: parent
                        color: Qt.rgba(0.3, 0.3, 0.3, 0.5)
                        visible: coverImage.status === Image.Error
                        
                        Text {
                            anchors.centerIn: parent
                            text: "No Image"
                            color: "white"
                            font.pixelSize: 12
                        }
                    }
                }
                
                // Platform badge
                Rectangle {
                    anchors.top: parent.top
                    anchors.left: parent.left
                    anchors.margins: 8
                    width: platformIcon.width + 12
                    height: 24
                    radius: 12
                    color: Qt.rgba(0, 0, 0, 0.7)
                    visible: platform !== ""
                    
                    Row {
                        anchors.centerIn: parent
                        spacing: 4
                        
                        Image {
                            id: platformIcon
                            width: 16
                            height: 16
                            source: getPlatformIcon(platform)
                            smooth: true
                        }
                        
                        Text {
                            text: platform
                            color: "white"
                            font.pixelSize: 10
                            font.weight: Font.Medium
                            visible: false // Hide text to save space, icon is enough
                        }
                    }
                }
                
                // Favorite indicator
                Rectangle {
                    anchors.top: parent.top
                    anchors.right: parent.right
                    anchors.margins: 8
                    width: 24
                    height: 24
                    radius: 12
                    color: Qt.rgba(0, 0, 0, 0.7)
                    visible: isFavorite
                    
                    Text {
                        anchors.centerIn: parent
                        text: "★"
                        color: "#ffd700"
                        font.pixelSize: 14
                    }
                }
                
                // Play button overlay (appears on hover)
                Rectangle {
                    anchors.centerIn: parent
                    width: 56
                    height: 56
                    radius: 28
                    color: Qt.rgba(0, 0, 0, 0.8)
                    visible: hovered && isInstalled
                    opacity: hovered ? 1.0 : 0.0
                    
                    Behavior on opacity {
                        NumberAnimation { duration: 200 }
                    }
                    
                    Text {
                        anchors.centerIn: parent
                        text: "▶"
                        color: "white"
                        font.pixelSize: 20
                    }
                    
                    MouseArea {
                        anchors.fill: parent
                        onClicked: {
                            playRequested()
                            mouse.accepted = true
                        }
                    }
                }
                
                // Installation status overlay
                Rectangle {
                    anchors.fill: parent
                    color: Qt.rgba(0, 0, 0, 0.6)
                    visible: !isInstalled
                    
                    Column {
                        anchors.centerIn: parent
                        spacing: 8
                        
                        Text {
                            anchors.horizontalCenter: parent.horizontalCenter
                            text: "Not Installed"
                            color: "white"
                            font.pixelSize: 14
                            font.weight: Font.Medium
                        }
                        
                        Button {
                            anchors.horizontalCenter: parent.horizontalCenter
                            text: "Install"
                            
                            background: Rectangle {
                                color: accentColor
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
                                // Handle install action
                                mouse.accepted = true
                            }
                        }
                    }
                }
            }
            
            // Game info section
            Rectangle {
                width: parent.width
                height: infoHeight
                color: "transparent"
                
                Column {
                    anchors.fill: parent
                    anchors.margins: 12
                    spacing: 4
                    
                    // Game title
                    Text {
                        width: parent.width
                        text: gameTitle
                        color: "white"
                        font.family: "Segoe UI Variable"
                        font.pixelSize: 14
                        font.weight: Font.Medium
                        elide: Text.ElideRight
                        maximumLineCount: 2
                        wrapMode: Text.WordWrap
                    }
                    
                    // Playtime
                    Text {
                        width: parent.width
                        text: playtime || "Never played"
                        color: Qt.rgba(1, 1, 1, 0.7)
                        font.family: "Segoe UI Variable"
                        font.pixelSize: 11
                        elide: Text.ElideRight
                    }
                }
            }
        }
    }
    
    // Main mouse area for card interactions
    MouseArea {
        id: mouseArea
        anchors.fill: parent
        hoverEnabled: true
        acceptedButtons: Qt.LeftButton | Qt.RightButton
        
        onClicked: function(mouse) {
            if (mouse.button === Qt.LeftButton) {
                root.clicked()
            } else if (mouse.button === Qt.RightButton) {
                root.rightClicked()
            }
        }
        
        onDoubleClicked: {
            if (isInstalled) {
                playRequested()
            }
        }
    }
    
    // Context menu (could be implemented here or externally)
    Menu {
        id: contextMenu
        
        MenuItem {
            text: isInstalled ? "Play" : "Install"
            enabled: isInstalled
            onTriggered: playRequested()
        }
        
        MenuSeparator {}
        
        MenuItem {
            text: isFavorite ? "Remove from Favorites" : "Add to Favorites"
            onTriggered: favoriteToggled()
        }
        
        MenuItem {
            text: "Properties"
            onTriggered: {
                // Show game properties dialog
            }
        }
        
        MenuSeparator {}
        
        MenuItem {
            text: "Remove from Library"
            onTriggered: {
                // Handle removal
            }
        }
    }
    
    // Helper functions
    function getPlatformIcon(platformName) {
        switch(platformName.toLowerCase()) {
            case "steam": return "qrc:/resources/icons/steam-icon.png"
            case "epic games": return "qrc:/resources/icons/epic-icon.png"
            case "gog": return "qrc:/resources/icons/gog-icon.png"
            case "ea app": return "qrc:/resources/icons/ea-icon.png"
            case "minecraft": return "qrc:/resources/icons/minecraft-icon.png"
            case "roblox": return "qrc:/resources/icons/roblox-icon.png"
            default: return "qrc:/resources/icons/default-platform.png"
        }
    }
    
    // Show context menu on right click
    Connections {
        target: root
        function onRightClicked() {
            contextMenu.popup()
        }
    }
}