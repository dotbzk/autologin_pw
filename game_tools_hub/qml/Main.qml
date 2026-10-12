import QtQuick
import QtQuick.Controls.Basic
import QtQuick.Layouts
import QtQuick.Window

ApplicationWindow {
    id: root
    visible: true
    width: 940
    height: 620
    minimumWidth: 760
    minimumHeight: 500
    title: "Game Tools Hub"
    color: "#080d18"

    property color accent: "#42bfff"
    property color textPrimary: "#eef8ff"
    property color textMuted: "#91a6b8"

    component HubButton: Button {
        id: control
        implicitHeight: 40
        font.pixelSize: 12
        font.weight: Font.DemiBold
        contentItem: Text {
            text: control.text
            color: control.enabled ? "white" : "#617080"
            font: control.font
            horizontalAlignment: Text.AlignHCenter
            verticalAlignment: Text.AlignVCenter
        }
        background: Rectangle {
            radius: 7
            color: !control.enabled ? "#17212d"
                  : control.down ? "#147ea9"
                  : control.hovered ? "#168fbe" : "#126f96"
            border.width: control.hovered && control.enabled ? 2 : 1
            border.color: control.enabled ? "#67d3ff" : "#344251"
        }
    }

    Rectangle {
        anchors.fill: parent
        gradient: Gradient {
            GradientStop { position: 0.0; color: "#101d30" }
            GradientStop { position: 1.0; color: "#070b12" }
        }
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 34
        spacing: 22

        RowLayout {
            Layout.fillWidth: true
            ColumnLayout {
                spacing: 3
                Text {
                    text: "GAME TOOLS HUB"
                    color: root.textPrimary
                    font.pixelSize: 28
                    font.weight: Font.Bold
                    font.letterSpacing: 1.5
                }
                Text {
                    text: "Choose a tool. Each module runs independently."
                    color: root.textMuted
                    font.pixelSize: 13
                }
            }
            Item { Layout.fillWidth: true }
            ColumnLayout {
                spacing: 2
                Text {
                    text: "Hub v" + backend.version + "  •  Bundle v" + backend.releaseVersion
                    color: root.textMuted
                    font.pixelSize: 11
                    Layout.alignment: Qt.AlignRight
                }
                Text {
                    text: {
                        let values = []
                        for (let i = 0; i < backend.components.length; ++i) {
                            let component = backend.components[i]
                            values.push(component.name + " " + component.version)
                        }
                        return values.join("  •  ")
                    }
                    color: "#75ccec"
                    font.pixelSize: 9
                    Layout.alignment: Qt.AlignRight
                }
            }
            HubButton {
                objectName: "checkUpdatesButton"
                text: backend.updateBusy ? "CHECKING…" : "CHECK UPDATES"
                enabled: !backend.updateBusy
                Layout.preferredWidth: 150
                onClicked: backend.checkForUpdates()
            }
        }

        GridView {
            id: moduleGrid
            objectName: "moduleGrid"
            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            cellWidth: Math.max(330, width / Math.max(1, Math.floor(width / 390)))
            cellHeight: 250
            model: backend.modules
            delegate: Item {
                required property var modelData
                width: moduleGrid.cellWidth
                height: moduleGrid.cellHeight
                Rectangle {
                    anchors.fill: parent
                    anchors.margins: 9
                    radius: 14
                    color: cardMouse.containsMouse ? "#e4182a3e" : "#d8111d2b"
                    border.width: cardMouse.containsMouse ? 2 : 1
                    border.color: cardMouse.containsMouse ? root.accent : "#31475b"

                    MouseArea {
                        id: cardMouse
                        anchors.fill: parent
                        hoverEnabled: true
                        acceptedButtons: Qt.NoButton
                    }

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 18
                        spacing: 10
                        RowLayout {
                            Layout.fillWidth: true
                            Rectangle {
                                width: 48; height: 48; radius: 12
                                color: "#173b55"
                                border.color: "#4ecaff"
                                Text {
                                    anchors.centerIn: parent
                                    text: modelData.shortName
                                    color: "#c9f1ff"
                                    font.pixelSize: 16
                                    font.bold: true
                                }
                            }
                            Text {
                                text: modelData.name
                                color: root.textPrimary
                                font.pixelSize: 18
                                font.weight: Font.DemiBold
                                Layout.fillWidth: true
                                elide: Text.ElideRight
                            }
                            Text {
                                text: "v" + modelData.version
                                color: root.textMuted
                                font.pixelSize: 10
                            }
                            Rectangle {
                                implicitWidth: statusText.implicitWidth + 16
                                implicitHeight: 25
                                radius: 12
                                color: modelData.canLaunch ? "#173f37" : "#302d35"
                                border.color: modelData.canLaunch ? "#55d5a7" : "#6f6571"
                                Text {
                                    id: statusText
                                    anchors.centerIn: parent
                                    text: modelData.statusText
                                    color: modelData.canLaunch ? "#8aefca" : "#b9aeba"
                                    font.pixelSize: 9
                                    font.bold: true
                                }
                            }
                        }
                        Text {
                            text: modelData.description
                            color: root.textMuted
                            font.pixelSize: 12
                            wrapMode: Text.Wrap
                            Layout.fillWidth: true
                        }
                        Text {
                            text: modelData.capabilities.join("  •  ")
                            color: "#75ccec"
                            font.pixelSize: 10
                            wrapMode: Text.Wrap
                            Layout.fillWidth: true
                        }
                        Item { Layout.fillHeight: true }
                        HubButton {
                            objectName: "moduleLaunchButton_" + modelData.id
                            Layout.fillWidth: true
                            text: modelData.status === "planned" ? "COMING SOON" : "OPEN"
                            enabled: modelData.canLaunch
                            onClicked: backend.launchModule(modelData.id)
                        }
                    }
                }
            }
        }
    }

    Popup {
        id: updatePopup
        objectName: "hubUpdatePopup"
        modal: true
        focus: true
        closePolicy: Popup.CloseOnEscape
        x: (root.width - width) / 2
        y: (root.height - height) / 2
        width: Math.min(520, root.width * 0.76)
        padding: 20
        property string releaseVersion: ""
        property string details: ""
        background: Rectangle {
            color: "#f00e1825"
            radius: 10
            border.width: 2
            border.color: root.accent
        }
        contentItem: ColumnLayout {
            spacing: 14
            Text {
                text: "BUNDLE UPDATE " + updatePopup.releaseVersion
                color: root.textPrimary
                font.pixelSize: 18
                font.bold: true
            }
            Text {
                text: updatePopup.details
                color: root.textMuted
                font.pixelSize: 12
                wrapMode: Text.Wrap
                Layout.fillWidth: true
            }
            Text {
                text: "The updater will replace Game Tools Hub, Game Launcher Bot, and PW Account Hub together."
                color: "#75ccec"
                font.pixelSize: 11
                wrapMode: Text.Wrap
                Layout.fillWidth: true
            }
            RowLayout {
                Layout.alignment: Qt.AlignRight
                HubButton { text: "CANCEL"; onClicked: updatePopup.close() }
                HubButton {
                    objectName: "installUpdateButton"
                    text: "INSTALL UPDATE"
                    onClicked: {
                        updatePopup.close()
                        backend.installPendingUpdate()
                    }
                }
            }
        }
    }

    Popup {
        id: alertPopup
        objectName: "hubAlertPopup"
        modal: true
        focus: true
        closePolicy: Popup.CloseOnEscape
        x: (root.width - width) / 2
        y: (root.height - height) / 2
        width: Math.min(480, root.width * 0.72)
        padding: 20
        property string dialogTitle: ""
        property string message: ""
        background: Rectangle {
            color: "#f00e1825"
            radius: 10
            border.width: 2
            border.color: root.accent
        }
        contentItem: ColumnLayout {
            spacing: 14
            Text { text: alertPopup.dialogTitle; color: root.textPrimary; font.pixelSize: 18; font.bold: true }
            Text { text: alertPopup.message; color: root.textMuted; font.pixelSize: 12; wrapMode: Text.Wrap; Layout.fillWidth: true }
            HubButton { text: "OK"; Layout.alignment: Qt.AlignRight; onClicked: alertPopup.close() }
        }
    }

    Connections {
        target: backend
        function onAlertRequested(title, message, kind) {
            alertPopup.dialogTitle = title
            alertPopup.message = message
            alertPopup.open()
        }
        function onUpdateConfirmationRequested(version, details) {
            updatePopup.releaseVersion = version
            updatePopup.details = details
            updatePopup.open()
        }
    }
}
