import QtQuick 2.15
import QtQuick.Controls 2.15

Rectangle {
    id: root
    
    property alias searchText: textInput.text
    property alias placeholderText: textInput.placeholderText
    property bool showClearButton: true
    
    signal searchTextChanged(string text)
    signal searchSubmitted(string text)
    signal cleared()
    
    height: 36
    radius: 18
    color: Qt.rgba(1, 1, 1, 0.08)
    border.color: textInput.activeFocus ? "#0078d4" : Qt.rgba(1, 1, 1, 0.2)
    border.width: textInput.activeFocus ? 2 : 1
    
    Behavior on border.color {
        ColorAnimation { duration: 200 }
    }
    
    Behavior on border.width {
        NumberAnimation { duration: 200 }
    }
    
    Row {
        anchors.fill: parent
        anchors.leftMargin: 15
        anchors.rightMargin: 15
        spacing: 10
        
        // Search icon
        Text {
            anchors.verticalCenter: parent.verticalCenter
            text: "🔍"
            font.pixelSize: 14
            color: Qt.rgba(1, 1, 1, 0.6)
        }
        
        // Text input
        TextInput {
            id: textInput
            anchors.verticalCenter: parent.verticalCenter
            width: parent.width - 50 - (clearButton.visible ? clearButton.width + 10 : 0)
            
            font.family: "Segoe UI Variable"
            font.pixelSize: 14
            color: "#ffffff"
            selectionColor: "#0078d4"
            selectedTextColor: "#ffffff"
            
            placeholderText: "Search games..."
            
            Text {
                anchors.fill: parent
                text: textInput.placeholderText
                font: textInput.font
                color: Qt.rgba(1, 1, 1, 0.5)
                visible: !textInput.text && !textInput.activeFocus
                verticalAlignment: Text.AlignVCenter
            }
            
            onTextChanged: {
                root.searchTextChanged(text)
            }
            
            onAccepted: {
                root.searchSubmitted(text)
            }
            
            Keys.onEscapePressed: {
                text = ""
                focus = false
            }
        }
        
        // Clear button
        Rectangle {
            id: clearButton
            anchors.verticalCenter: parent.verticalCenter
            width: 20
            height: 20
            radius: 10
            color: mouseArea.containsMouse ? Qt.rgba(1, 1, 1, 0.2) : "transparent"
            visible: showClearButton && textInput.text.length > 0
            
            Text {
                anchors.centerIn: parent
                text: "✕"
                font.pixelSize: 10
                color: Qt.rgba(1, 1, 1, 0.7)
            }
            
            MouseArea {
                id: mouseArea
                anchors.fill: parent
                hoverEnabled: true
                
                onClicked: {
                    textInput.text = ""
                    textInput.focus = true
                    root.cleared()
                }
            }
            
            Behavior on color {
                ColorAnimation { duration: 150 }
            }
        }
    }
    
    // Ripple effect on focus
    Rectangle {
        anchors.fill: parent
        radius: parent.radius
        color: "transparent"
        border.color: "#0078d4"
        border.width: 2
        opacity: textInput.activeFocus ? 0.3 : 0.0
        
        Behavior on opacity {
            NumberAnimation { duration: 200 }
        }
    }
    
    // Focus management
    MouseArea {
        anchors.fill: parent
        onClicked: {
            textInput.forceActiveFocus()
        }
    }
    
    function clear() {
        textInput.text = ""
    }
    
    function focus() {
        textInput.forceActiveFocus()
    }
}