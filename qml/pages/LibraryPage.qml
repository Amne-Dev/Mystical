import QtQuick 2.15
import QtQuick.Controls 2.15
import QtQuick.Layouts 1.15
import "../components" as Components

Rectangle {
    id: root
    color: "transparent"
    
    property alias gameGrid: gameGridView
    property string currentFilter: "all"
    property string sortBy: "title" // "title", "platform", "playtime", "recent"
    property bool sortAscending: true
    
    // Filter options
    readonly property var filterOptions: [
        { key: "all", name: "All Games", icon: "🎮" },
        { key: "favorites", name: "Favorites", icon: "★" },
        { key: "recent", name: "Recently Played", icon: "🕐" },
        { key: "steam", name: "Steam", icon: "🔧" },
        { key: "epic", name: "Epic Games", icon: "🏪" },
        { key: "gog", name: "GOG", icon: "🎯" },
        { key: "custom", name: "Custom", icon: "📁" }
    ]
    
    ColumnLayout {
        anchors.fill: parent
        spacing: 20
        
        // Filter and sort bar
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 50
            color: Qt.rgba(1, 1, 1, 0.05)
            radius: 8
            border.color: Qt.rgba(1, 1, 1, 0.1)
            border.width: 1
            
            RowLayout {
                anchors.fill: parent
                anchors.margins: 15
                spacing: 15
                
                // Filter dropdown
                ComboBox {
                    id: filterComboBox
                    Layout.preferredWidth: 150
                    
                    model: root.filterOptions
                    textRole: "name"
                    valueRole: "key"
                    currentIndex: 0
                    
                    delegate: ItemDelegate {
                        width: filterComboBox.width
                        
                        contentItem: Row {
                            spacing: 8
                            leftPadding: 10
                            
                            Text {
                                text: modelData.icon
                                font.pixelSize: 14
                                anchors.verticalCenter: parent.verticalCenter
                            }
                            
                            Text {
                                text: modelData.name
                                color: "#ffffff"
                                font.pixelSize: 13
                                anchors.verticalCenter: parent.verticalCenter
                            }
                        }
                        
                        background: Rectangle {
                            color: parent.hovered ? Qt.rgba(1, 1, 1, 0.1) : "transparent"
                            radius: 4
                        }
                    }
                    
                    background: Rectangle {
                        color: Qt.rgba(1, 1, 1, 0.08)
                        border.color: Qt.rgba(1, 1, 1, 0.2)
                        border.width: 1
                        radius: 4
                    }
                    
                    contentItem: Row {
                        spacing: 8
                        leftPadding: 10
                        
                        Text {
                            text: filterComboBox.currentIndex >= 0 ? 
                                  filterOptions[filterComboBox.currentIndex].icon : ""
                            font.pixelSize: 14
                            anchors.verticalCenter: parent.verticalCenter
                        }
                        
                        Text {
                            text: filterComboBox.currentIndex >= 0 ? 
                                  filterOptions[filterComboBox.currentIndex].name : ""
                            color: "#ffffff"
                            font.pixelSize: 13
                            anchors.verticalCenter: parent.verticalCenter
                        }
                    }
                    
                    onCurrentValueChanged: {
                        root.currentFilter = currentValue
                        updateGameFilter()
                    }
                }
                
                // Sort options
                Row {
                    spacing: 10
                    
                    Text {
                        text: "Sort by:"
                        color: Qt.rgba(1, 1, 1, 0.7)
                        font.pixelSize: 12
                        anchors.verticalCenter: parent.verticalCenter
                    }
                    
                    ComboBox {
                        id: sortComboBox
                        width: 120
                        
                        model: [
                            { key: "title", name: "Title" },
                            { key: "platform", name: "Platform" },
                            { key: "playtime", name: "Playtime" },
                            { key: "recent", name: "Last Played" }
                        ]
                        textRole: "name"
                        valueRole: "key"
                        currentIndex: 0
                        
                        background: Rectangle {
                            color: Qt.rgba(1, 1, 1, 0.08)
                            border.color: Qt.rgba(1, 1, 1, 0.2)
                            border.width: 1
                            radius: 4
                        }
                        
                        contentItem: Text {
                            text: sortComboBox.displayText
                            color: "#ffffff"
                            font.pixelSize: 12
                            leftPadding: 8
                            verticalAlignment: Text.AlignVCenter
                        }
                        
                        onCurrentValueChanged: {
                            root.sortBy = currentValue
                            updateGameSorting()
                        }
                    }
                    
                    Button {
                        width: 30
                        height: 30
                        
                        background: Rectangle {
                            color: parent.hovered ? Qt.rgba(1, 1, 1, 0.1) : Qt.rgba(1, 1, 1, 0.05)
                            border.color: Qt.rgba(1, 1, 1, 0.2)
                            border.width: 1
                            radius: 4
                        }
                        
                        contentItem: Text {
                            text: root.sortAscending ? "↑" : "↓"
                            color: "#ffffff"
                            font.pixelSize: 14
                            horizontalAlignment: Text.AlignHCenter
                            verticalAlignment: Text.AlignVCenter
                        }
                        
                        onClicked: {
                            root.sortAscending = !root.sortAscending
                            updateGameSorting()
                        }
                        
                        ToolTip.visible: hovered
                        ToolTip.text: sortAscending ? "Sort descending" : "Sort ascending"
                    }
                }
                
                Item { Layout.fillWidth: true }
                
                // View options
                Row {
                    spacing: 5
                    
                    Text {
                        text: gameGridView.count + " games"
                        color: Qt.rgba(1, 1, 1, 0.7)
                        font.pixelSize: 12
                        anchors.verticalCenter: parent.verticalCenter
                    }
                }
            }
        }
        
        // Games grid
        ScrollView {
            Layout.fillWidth: true
            Layout.fillHeight: true
            
            ScrollBar.horizontal.policy: ScrollBar.AlwaysOff
            ScrollBar.vertical.policy: ScrollBar.AsNeeded
            
            ScrollBar.vertical: ScrollBar {
                active: true
                
                background: Rectangle {
                    color: Qt.rgba(1, 1, 1, 0.1)
                    radius: 3
                    implicitWidth: 6
                }
                
                contentItem: Rectangle {
                    color: Qt.rgba(1, 1, 1, 0.3)
                    radius: 3
                    implicitWidth: 6
                }
            }
            
            GridView {
                id: gameGridView
                
                cellWidth: 220
                cellHeight: 300
                
                model: gameLibraryModel
                
                delegate: Components.GameCard {
                    gameTitle: model.title || ""
                    platform: model.platform || ""
                    coverArt: model.coverArt || ""
                    playtime: model.playtime || ""
                    isFavorite: model.isFavorite || false
                    isInstalled: model.isInstalled !== undefined ? model.isInstalled : true
                    
                    onClicked: {
                        // Handle single click - could show details
                        console.log("Game clicked:", gameTitle)
                    }
                    
                    onPlayRequested: {
                        if (gameLibrary) {
                            gameLibrary.launchGameByIndex(index)
                        }
                    }
                    
                    onFavoriteToggled: {
                        if (gameLibraryModel) {
                            gameLibraryModel.setGameFavorite(index, !isFavorite)
                        }
                    }
                }
                
                // Empty state
                Rectangle {
                    anchors.centerIn: parent
                    width: 300
                    height: 200
                    color: "transparent"
                    visible: gameGridView.count === 0 && !gameLibrary.isScanning
                    
                    Column {
                        anchors.centerIn: parent
                        spacing: 20
                        
                        Text {
                            anchors.horizontalCenter: parent.horizontalCenter
                            text: "🎮"
                            font.pixelSize: 48
                            color: Qt.rgba(1, 1, 1, 0.3)
                        }
                        
                        Text {
                            anchors.horizontalCenter: parent.horizontalCenter
                            text: "No games found"
                            font.family: "Segoe UI Variable"
                            font.pixelSize: 18
                            font.weight: Font.Medium
                            color: Qt.rgba(1, 1, 1, 0.7)
                        }
                        
                        Text {
                            anchors.horizontalCenter: parent.horizontalCenter
                            width: 280
                            text: "Games will appear here once detected from installed platforms like Steam, Epic Games, GOG, and others."
                            font.family: "Segoe UI Variable"
                            font.pixelSize: 14
                            color: Qt.rgba(1, 1, 1, 0.5)
                            horizontalAlignment: Text.AlignHCenter
                            wrapMode: Text.WordWrap
                        }
                        
                        Button {
                            anchors.horizontalCenter: parent.horizontalCenter
                            text: "Scan for Games"
                            
                            background: Rectangle {
                                color: "#0078d4"
                                radius: 4
                                opacity: parent.hovered ? 0.8 : 1.0
                            }
                            
                            contentItem: Text {
                                text: parent.text
                                color: "white"
                                font.pixelSize: 13
                                horizontalAlignment: Text.AlignHCenter
                                verticalAlignment: Text.AlignVCenter
                            }
                            
                            onClicked: {
                                if (gameLibrary) {
                                    gameLibrary.startFullScan()
                                }
                            }
                        }
                    }
                }
                
                // Add subtle grid animations
                add: Transition {
                    NumberAnimation {
                        properties: "opacity"
                        from: 0
                        to: 1
                        duration: 300
                        easing.type: Easing.OutQuart
                    }
                    NumberAnimation {
                        properties: "scale"
                        from: 0.8
                        to: 1.0
                        duration: 300
                        easing.type: Easing.OutBack
                    }
                }
                
                remove: Transition {
                    NumberAnimation {
                        properties: "opacity"
                        from: 1
                        to: 0
                        duration: 200
                    }
                    NumberAnimation {
                        properties: "scale"
                        from: 1.0
                        to: 0.8
                        duration: 200
                    }
                }
            }
        }
    }
    
    // Filter and sorting functions
    function updateGameFilter() {
        if (!gameLibraryModel) return
        
        switch (currentFilter) {
            case "all":
                gameLibraryModel.setFilterPlatform("")
                gameLibraryModel.setFilterFavorites(false)
                gameLibraryModel.setFilterRecent(false)
                break
            case "favorites":
                gameLibraryModel.setFilterPlatform("")
                gameLibraryModel.setFilterFavorites(true)
                gameLibraryModel.setFilterRecent(false)
                break
            case "recent":
                gameLibraryModel.setFilterPlatform("")
                gameLibraryModel.setFilterFavorites(false)
                gameLibraryModel.setFilterRecent(true)
                break
            default:
                gameLibraryModel.setFilterPlatform(currentFilter)
                gameLibraryModel.setFilterFavorites(false)
                gameLibraryModel.setFilterRecent(false)
                break
        }
    }
    
    function updateGameSorting() {
        if (!gameLibraryModel) return
        
        gameLibraryModel.setSortField(sortBy)
        gameLibraryModel.setSortAscending(sortAscending)
    }
    
    Component.onCompleted: {
        updateGameFilter()
        updateGameSorting()
    }
}