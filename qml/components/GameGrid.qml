import QtQuick 2.15
import QtQuick.Controls 2.15
import QtQuick.Layouts 1.15

ScrollView {
    id: root
    
    // Public properties
    property alias model: gridView.model
    property alias delegate: gridView.delegate
    property alias cellWidth: gridView.cellWidth
    property alias cellHeight: gridView.cellHeight
    property alias spacing: gridView.spacing
    property alias count: gridView.count
    property alias currentIndex: gridView.currentIndex
    property alias currentItem: gridView.currentItem
    
    // Grid configuration
    property int columns: Math.floor(width / cellWidth)
    property real itemSpacing: 20
    property color backgroundColor: "transparent"
    
    // Selection and interaction
    property bool selectionEnabled: false
    property var selectedIndices: []
    
    // Signals
    signal itemClicked(int index, var item)
    signal itemDoubleClicked(int index, var item)
    signal itemRightClicked(int index, var item)
    signal selectionChanged(var selectedIndices)
    
    // Scrolling configuration
    ScrollBar.horizontal.policy: ScrollBar.AlwaysOff
    ScrollBar.vertical.policy: ScrollBar.AsNeeded
    
    // Custom scrollbar styling
    ScrollBar.vertical: ScrollBar {
        id: verticalScrollBar
        active: true
        
        background: Rectangle {
            color: Qt.rgba(1, 1, 1, 0.1)
            radius: 3
            implicitWidth: 6
            
            Behavior on opacity {
                NumberAnimation { duration: 200 }
            }
        }
        
        contentItem: Rectangle {
            color: Qt.rgba(1, 1, 1, parent.hovered ? 0.4 : 0.3)
            radius: 3
            implicitWidth: 6
            
            Behavior on color {
                ColorAnimation { duration: 150 }
            }
        }
    }
    
    // Main grid view
    GridView {
        id: gridView
        
        cellWidth: 220
        cellHeight: 300
        
        // Performance optimizations
        cacheBuffer: cellHeight * Math.max(4, Math.ceil(height / cellHeight))
        reuseItems: true
        
        // Animation configurations
        displaced: Transition {
            NumberAnimation {
                properties: "x,y"
                duration: 300
                easing.type: Easing.OutQuart
            }
        }
        
        add: Transition {
            ParallelAnimation {
                NumberAnimation {
                    properties: "opacity"
                    from: 0
                    to: 1
                    duration: 400
                    easing.type: Easing.OutQuart
                }
                NumberAnimation {
                    properties: "scale"
                    from: 0.8
                    to: 1.0
                    duration: 400
                    easing.type: Easing.OutBack
                    easing.overshoot: 1.2
                }
            }
        }
        
        remove: Transition {
            ParallelAnimation {
                NumberAnimation {
                    properties: "opacity"
                    from: 1
                    to: 0
                    duration: 250
                    easing.type: Easing.InQuart
                }
                NumberAnimation {
                    properties: "scale"
                    from: 1.0
                    to: 0.8
                    duration: 250
                    easing.type: Easing.InQuart
                }
            }
        }
        
        // Handle item interactions
        function handleItemClick(index, mouse) {
            if (selectionEnabled) {
                if (mouse.modifiers & Qt.ControlModifier) {
                    toggleSelection(index)
                } else if (mouse.modifiers & Qt.ShiftModifier && selectedIndices.length > 0) {
                    selectRange(Math.min(...selectedIndices), index)
                } else {
                    clearSelection()
                    addToSelection(index)
                }
            }
            
            root.itemClicked(index, gridView.itemAtIndex(index))
        }
        
        function handleItemDoubleClick(index) {
            root.itemDoubleClicked(index, gridView.itemAtIndex(index))
        }
        
        function handleItemRightClick(index) {
            if (selectionEnabled && selectedIndices.indexOf(index) === -1) {
                clearSelection()
                addToSelection(index)
            }
            root.itemRightClicked(index, gridView.itemAtIndex(index))
        }
    }
    
    // Background styling
    Rectangle {
        anchors.fill: parent
        color: backgroundColor
        z: -1
    }
    
    // Empty state placeholder
    Rectangle {
        id: emptyState
        anchors.centerIn: parent
        width: 300
        height: 200
        color: "transparent"
        visible: gridView.count === 0
        
        Column {
            anchors.centerIn: parent
            spacing: 20
            
            Text {
                anchors.horizontalCenter: parent.horizontalCenter
                text: "📦"
                font.pixelSize: 48
                color: Qt.rgba(1, 1, 1, 0.3)
            }
            
            Text {
                anchors.horizontalCenter: parent.horizontalCenter
                text: "No items to display"
                font.family: "Segoe UI Variable"
                font.pixelSize: 16
                font.weight: Font.Medium
                color: Qt.rgba(1, 1, 1, 0.6)
            }
            
            Text {
                anchors.horizontalCenter: parent.horizontalCenter
                width: 280
                text: "Items will appear here when available."
                font.family: "Segoe UI Variable"  
                font.pixelSize: 13
                color: Qt.rgba(1, 1, 1, 0.4)
                horizontalAlignment: Text.AlignHCenter
                wrapMode: Text.WordWrap
            }
        }
    }
    
    // Loading indicator overlay
    Rectangle {
        id: loadingOverlay
        anchors.fill: parent
        color: Qt.rgba(0, 0, 0, 0.3)
        visible: false
        
        property bool isLoading: false
        
        BusyIndicator {
            anchors.centerIn: parent
            running: parent.isLoading
            
            contentItem: Item {
                implicitWidth: 48
                implicitHeight: 48
                
                Item {
                    id: loadingItem
                    x: parent.width / 2 - 24
                    y: parent.height / 2 - 24
                    width: 48
                    height: 48
                    opacity: parent.parent.running ? 1 : 0
                    
                    Behavior on opacity {
                        OpacityAnimator { duration: 250 }
                    }
                    
                    RotationAnimator {
                        target: loadingItem
                        running: parent.parent.running
                        from: 0
                        to: 360
                        loops: Animation.Infinite
                        duration: 1250
                    }
                    
                    Repeater {
                        model: 6
                        
                        Rectangle {
                            x: loadingItem.width / 2 - width / 2
                            y: loadingItem.height / 2 - height / 2
                            implicitWidth: 8
                            implicitHeight: 8
                            radius: 4
                            color: "#0078d4"
                            transform: [
                                Translate {
                                    y: -Math.min(loadingItem.width, loadingItem.height) * 0.5 + 8
                                },
                                Rotation {
                                    angle: index / 6 * 360
                                    origin.x: 4
                                    origin.y: 4
                                }
                            ]
                        }
                    }
                }
            }
        }
    }
    
    // Selection management functions
    function clearSelection() {
        selectedIndices = []
        selectionChanged(selectedIndices)
    }
    
    function addToSelection(index) {
        if (selectedIndices.indexOf(index) === -1) {
            selectedIndices.push(index)
            selectedIndices = selectedIndices.slice() // Trigger property change
            selectionChanged(selectedIndices)
        }
    }
    
    function removeFromSelection(index) {
        var newSelection = selectedIndices.filter(function(i) { return i !== index })
        if (newSelection.length !== selectedIndices.length) {
            selectedIndices = newSelection
            selectionChanged(selectedIndices)
        }
    }
    
    function toggleSelection(index) {
        if (selectedIndices.indexOf(index) === -1) {
            addToSelection(index)
        } else {
            removeFromSelection(index)
        }
    }
    
    function selectRange(startIndex, endIndex) {
        clearSelection()
        var start = Math.min(startIndex, endIndex)
        var end = Math.max(startIndex, endIndex)
        
        for (var i = start; i <= end; i++) {
            addToSelection(i)
        }
    }
    
    function selectAll() {
        clearSelection()
        for (var i = 0; i < gridView.count; i++) {
            selectedIndices.push(i)
        }
        selectedIndices = selectedIndices.slice() // Trigger property change
        selectionChanged(selectedIndices)
    }
    
    function isSelected(index) {
        return selectedIndices.indexOf(index) !== -1
    }
    
    // Utility functions
    function scrollToIndex(index) {
        gridView.positionViewAtIndex(index, GridView.Contain)
    }
    
    function scrollToTop() {
        gridView.positionViewAtBeginning()
    }
    
    function scrollToBottom() {
        gridView.positionViewAtEnd()
    }
    
    function setLoading(loading) {
        loadingOverlay.isLoading = loading
        loadingOverlay.visible = loading
    }
    
    // Keyboard navigation
    Keys.onPressed: function(event) {
        if (!selectionEnabled) return
        
        switch (event.key) {
            case Qt.Key_A:
                if (event.modifiers & Qt.ControlModifier) {
                    selectAll()
                    event.accepted = true
                }
                break
            case Qt.Key_Escape:
                clearSelection()
                event.accepted = true
                break
        }
    }
    
    // Focus handling
    focus: true
    activeFocusOnTab: true
}