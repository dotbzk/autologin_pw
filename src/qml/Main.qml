import QtQuick
import QtQuick.Controls.Basic
import QtQuick.Layouts
import QtQuick.Window

ApplicationWindow {
    id: root
    visible: true
    title: "Game Launcher Bot"
    color: "transparent"
    flags: Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint | Qt.Window
    height: Math.min(1000, Screen.desktopAvailableHeight * 0.94)
    width: height * 1122 / 1402
    minimumWidth: 610
    minimumHeight: minimumWidth * 1402 / 1122
    maximumWidth: 1122
    maximumHeight: 1402

    property color accent: "#ff3048"
    property color accentSoft: "#a82132"
    property color surface: "#dc100d10"
    property color surfaceHover: "#ed31151c"
    property color textPrimary: "#fff4f4"
    property color textMuted: "#c8a8aa"

    onClosing: function(event) {
        event.accepted = false
        backend.requestClose()
    }

    component GlowButton: Button {
        id: control
        property bool selected: false
        implicitHeight: 34
        implicitWidth: 100
        font.pixelSize: 12
        font.weight: Font.DemiBold
        contentItem: Text {
            text: control.text
            font: control.font
            color: control.enabled ? "#f4fcff" : "#668090"
            horizontalAlignment: Text.AlignHCenter
            verticalAlignment: Text.AlignVCenter
            elide: Text.ElideRight
        }
        background: Rectangle {
            radius: 5
            color: !control.enabled ? "#55101c29"
                  : control.down ? "#e0a51b2d"
                  : control.selected ? "#d0931828"
                  : control.hovered ? "#d15d1a26" : "#c11a1118"
            border.width: control.activeFocus || control.hovered || control.selected ? 2 : 1
            border.color: control.enabled ? (control.selected ? "#ff8392" : "#c8aeb1") : "#5d4b4d"
        }
    }

    component StopButton: GlowButton {
        id: stopControl
        background: Rectangle {
            radius: 5
            color: !stopControl.enabled ? "#55291622" : stopControl.down ? "#e32640" : stopControl.hovered ? "#bd2037" : "#8c1729"
            border.width: stopControl.hovered ? 2 : 1
            border.color: stopControl.enabled ? "#ff6578" : "#65404a"
        }
    }

    component StyledCheckBox: CheckBox {
        id: check
        spacing: 8
        font.pixelSize: 12
        indicator: Rectangle {
            implicitWidth: 19
            implicitHeight: 19
            x: check.leftPadding
            y: parent.height / 2 - height / 2
            radius: 4
            color: check.checked ? "#b91e34" : "#ba171014"
            border.width: check.hovered || check.activeFocus ? 2 : 1
            border.color: check.enabled ? "#e1b8bd" : "#665457"
            Text {
                anchors.centerIn: parent
                text: "✓"
                visible: check.checked
                color: "white"
                font.bold: true
                font.pixelSize: 13
            }
        }
        contentItem: Text {
            leftPadding: check.indicator.width + check.spacing
            text: check.text
            color: check.enabled ? root.textPrimary : "#637b89"
            font: check.font
            verticalAlignment: Text.AlignVCenter
            elide: Text.ElideRight
        }
    }

    component StyledField: TextField {
        id: field
        color: root.textPrimary
        selectionColor: root.accentSoft
        selectedTextColor: "white"
        placeholderTextColor: "#617f91"
        font.pixelSize: 12
        leftPadding: 10
        rightPadding: 10
        background: Rectangle {
            radius: 4
            color: "#d509182b"
            border.width: field.activeFocus ? 2 : 1
            border.color: field.activeFocus ? root.accent : "#76565a"
        }
    }

    component StyledCombo: ComboBox {
        id: combo
        font.pixelSize: 12
        implicitHeight: 34
        contentItem: Text {
            leftPadding: 10
            rightPadding: combo.indicator.width + 8
            text: combo.displayText
            color: combo.enabled ? root.textPrimary : "#637b89"
            font: combo.font
            verticalAlignment: Text.AlignVCenter
            elide: Text.ElideRight
        }
        indicator: Text {
            x: combo.width - width - 10
            anchors.verticalCenter: parent.verticalCenter
            text: "▾"
            color: root.accent
            font.pixelSize: 15
        }
        background: Rectangle {
            radius: 5
            color: combo.down ? "#e20c2d4a" : combo.hovered ? "#e00f2944" : "#dc09182b"
            border.width: combo.activeFocus || combo.hovered ? 2 : 1
            border.color: combo.enabled ? root.accentSoft : "#5e4b4e"
        }
        popup: Popup {
            y: combo.height + 2
            width: combo.width
            implicitHeight: Math.min(contentItem.implicitHeight + 4, 240)
            padding: 2
            contentItem: ListView {
                clip: true
                implicitHeight: contentHeight
                model: combo.popup.visible ? combo.delegateModel : null
                currentIndex: combo.highlightedIndex
                ScrollIndicator.vertical: ScrollIndicator { }
            }
            background: Rectangle {
                color: "#f009192d"
                border.color: root.accentSoft
                border.width: 1
                radius: 5
            }
        }
        delegate: ItemDelegate {
            id: comboDelegate
            required property int index
            required property var modelData
            width: combo.width - 4
            height: 32
            contentItem: Text {
                text: comboDelegate.modelData
                color: comboDelegate.highlighted ? "white" : root.textPrimary
                verticalAlignment: Text.AlignVCenter
                leftPadding: 8
                elide: Text.ElideRight
            }
            background: Rectangle { color: comboDelegate.highlighted ? "#b06f1726" : "transparent"; radius: 3 }
            highlighted: combo.highlightedIndex === comboDelegate.index
        }
    }

    Image {
        id: artwork
        anchors.fill: parent
        source: backend.backgroundUrl
        fillMode: Image.Stretch
        smooth: true
        mipmap: true
    }

    Item {
        id: panel
        x: root.width * 0.435
        y: root.height * 0.168
        width: root.width * 0.500
        height: root.height * 0.714

        Rectangle {
            anchors.fill: parent
            color: "#13000000"
            radius: 8
        }

        ColumnLayout {
            anchors.fill: parent
            anchors.margins: Math.max(10, root.width * 0.015)
            spacing: 6

            Item {
                Layout.fillWidth: true
                Layout.preferredHeight: 38
                Text {
                    anchors.left: parent.left
                    anchors.verticalCenter: parent.verticalCenter
                    text: "GAME LAUNCHER BOT"
                    color: root.textPrimary
                    font.pixelSize: 17
                    font.bold: true
                    font.letterSpacing: 1.2
                }
                Text {
                    anchors.left: parent.left
                    anchors.top: parent.verticalCenter
                    anchors.topMargin: 9
                    text: "v" + backend.version
                    color: root.textMuted
                    font.pixelSize: 9
                }
                Row {
                    anchors.right: parent.right
                    anchors.verticalCenter: parent.verticalCenter
                    spacing: 5
                    GlowButton {
                        objectName: "minimizeButton"
                        text: "—"
                        width: 34
                        height: 28
                        onClicked: root.showMinimized()
                    }
                    StopButton {
                        objectName: "closeButton"
                        text: "×"
                        width: 34
                        height: 28
                        onClicked: backend.requestClose()
                    }
                }
                MouseArea {
                    anchors.fill: parent
                    anchors.rightMargin: 82
                    cursorShape: Qt.SizeAllCursor
                    onPressed: root.startSystemMove()
                }
            }

            RowLayout {
                Layout.fillWidth: true
                Layout.preferredHeight: 34
                Text { text: "GROUP"; color: root.textMuted; font.pixelSize: 11; font.bold: true }
                StyledCombo {
                    id: groupCombo
                    objectName: "groupCombo"
                    Layout.fillWidth: true
                    model: backend.groups
                    currentIndex: Math.max(0, backend.groups.indexOf(backend.currentGroup))
                    onActivated: backend.selectGroup(currentText)
                }
            }

            Rectangle {
                Layout.fillWidth: true
                Layout.fillHeight: true
                Layout.minimumHeight: 150
                color: root.surface
                radius: 6
                border.color: "#72585b"
                border.width: 1
                ListView {
                    id: accountsList
                    objectName: "accountsList"
                    anchors.fill: parent
                    anchors.margins: 5
                    clip: true
                    spacing: 4
                    model: backend.accounts
                    ScrollBar.vertical: ScrollBar { policy: ScrollBar.AsNeeded }
                    delegate: Rectangle {
                        required property var modelData
                        width: accountsList.width - (accountsList.ScrollBar.vertical.visible ? 10 : 0)
                        height: 43
                        radius: 5
                        color: modelData.selected ? "#c1741726" : accountMouse.containsMouse ? root.surfaceHover : "#b3140e11"
                        border.width: modelData.selected || accountMouse.containsMouse ? 2 : 1
                        border.color: modelData.selected ? "#ff7183" : "#654b4e"
                        Image {
                            id: accountIcon
                            x: 7; width: 32; height: 32
                            anchors.verticalCenter: parent.verticalCenter
                            source: modelData.icon
                            fillMode: Image.PreserveAspectFit
                            smooth: true
                        }
                        Text {
                            anchors.left: accountIcon.right
                            anchors.leftMargin: 9
                            anchors.right: statusMark.left
                            anchors.rightMargin: 6
                            anchors.verticalCenter: parent.verticalCenter
                            text: modelData.name
                            color: root.textPrimary
                            font.pixelSize: 12
                            elide: Text.ElideRight
                        }
                        Rectangle {
                            id: statusMark
                            anchors.right: parent.right
                            anchors.rightMargin: 9
                            anchors.verticalCenter: parent.verticalCenter
                            width: 18; height: 18; radius: 9
                            color: modelData.selected ? "#c51e38" : "#2b1b1e"
                            border.color: modelData.selected ? "#ff9aa7" : "#74565a"
                            Text { anchors.centerIn: parent; text: modelData.selected ? "✓" : ""; color: "white"; font.bold: true }
                        }
                        MouseArea {
                            id: accountMouse
                            anchors.fill: parent
                            hoverEnabled: true
                            onClicked: backend.toggleAccount(modelData.name)
                        }
                    }
                }
                Text {
                    anchors.centerIn: parent
                    visible: backend.accounts.length === 0
                    text: "No clients in this group"
                    color: root.textMuted
                    font.pixelSize: 12
                }
            }

            RowLayout {
                Layout.fillWidth: true
                Layout.preferredHeight: 32
                spacing: 6
                GlowButton { objectName: "selectAllButton"; text: "SELECT ALL"; Layout.fillWidth: true; onClicked: backend.selectAll() }
                GlowButton { objectName: "unselectAllButton"; text: "UNSELECT ALL"; Layout.fillWidth: true; onClicked: backend.unselectAll() }
            }

            GlowButton {
                objectName: "settingsButton"
                text: "SETTINGS"
                font.pixelSize: 11
                Layout.fillWidth: true
                Layout.preferredHeight: 34
                onClicked: settingsMenuPopup.open()
            }

            StyledCheckBox {
                objectName: "memoryCleanupCheckBox"
                Layout.fillWidth: true
                Layout.preferredHeight: 27
                text: "Clean memory before each client"
                checked: backend.memoryCleanupEnabled
                onToggled: if (checked !== backend.memoryCleanupEnabled) backend.setMemoryCleanupEnabled(checked)
            }

            Text { text: "LOG"; color: root.textMuted; font.pixelSize: 11; font.bold: true }
            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: Math.max(104, panel.height * 0.17)
                color: "#e0061223"
                radius: 5
                border.color: "#684b50"
                border.width: 1
                ScrollView {
                    anchors.fill: parent
                    anchors.margins: 4
                    TextArea {
                        id: logArea
                        objectName: "logArea"
                        readOnly: true
                        text: backend.logText
                        color: "#ffe3e6"
                        font.family: "Consolas"
                        font.pixelSize: 10
                        wrapMode: TextEdit.Wrap
                        selectByMouse: true
                        background: null
                    }
                }
                Connections {
                    target: backend
                    function onLogTextChanged() { logArea.cursorPosition = logArea.length }
                }
            }

            ProgressBar {
                id: progressBar
                objectName: "progressBar"
                Layout.fillWidth: true
                Layout.preferredHeight: 9
                from: 0; to: 100; value: backend.progress
                background: Rectangle { radius: 4; color: "#801c1114"; border.color: "#64494d" }
                contentItem: Item {
                    Rectangle {
                        width: progressBar.visualPosition * parent.width
                        height: parent.height
                        radius: 4
                        color: root.accent
                    }
                }
            }

            RowLayout {
                Layout.fillWidth: true
                Layout.preferredHeight: 42
                spacing: 7
                GlowButton { objectName: "runButton"; text: backend.running ? "RUNNING..." : "RUN"; font.pixelSize: 12; selected: !backend.running; Layout.fillWidth: true; enabled: backend.ready && !backend.running; onClicked: backend.runBot() }
                StopButton { objectName: "stopButton"; text: "STOP"; font.pixelSize: 12; Layout.fillWidth: true; enabled: backend.running; onClicked: backend.stopBot() }
            }
        }
    }

    Popup {
        id: settingsMenuPopup
        objectName: "settingsMenuPopup"
        modal: true
        focus: true
        closePolicy: Popup.CloseOnEscape | Popup.CloseOnPressOutside
        x: (root.width - width) / 2
        y: (root.height - height) / 2
        width: root.width * 0.68
        padding: 18
        background: Rectangle { color: "#f00b080a"; radius: 10; border.width: 2; border.color: root.accentSoft }
        contentItem: ColumnLayout {
            spacing: 10
            RowLayout {
                Layout.fillWidth: true
                Text { text: "SETTINGS"; color: root.textPrimary; font.pixelSize: 18; font.bold: true; Layout.fillWidth: true }
                GlowButton { text: "×"; Layout.preferredWidth: 34; onClicked: settingsMenuPopup.close() }
            }
            GlowButton {
                objectName: "advancedSettingsButton"
                text: "APPLICATION SETTINGS"
                Layout.fillWidth: true
                enabled: !backend.running
                onClicked: { settingsMenuPopup.close(); settingsPopup.openEditor() }
            }
            GlowButton {
                objectName: "manageGroupsButton"
                text: "MANAGE GROUPS"
                Layout.fillWidth: true
                enabled: !backend.running
                onClicked: { settingsMenuPopup.close(); managePopup.openEditor() }
            }
            GlowButton {
                objectName: "updateButton"
                text: backend.updateBusy ? "CHECKING UPDATE..." : "CHECK UPDATE"
                Layout.fillWidth: true
                enabled: backend.ready && !backend.running && !backend.updateBusy
                onClicked: { settingsMenuPopup.close(); backend.checkForUpdates(false) }
            }
            GlowButton {
                objectName: "debugButton"
                text: "DEBUG: " + (backend.debugEnabled ? "ON" : "OFF")
                selected: backend.debugEnabled
                Layout.fillWidth: true
                onClicked: backend.toggleDebug()
            }
        }
    }

    Popup {
        id: managePopup
        objectName: "managePopup"
        modal: true
        focus: true
        closePolicy: Popup.CloseOnEscape
        x: (root.width - width) / 2
        y: (root.height - height) / 2
        width: root.width * 0.86
        height: root.height * 0.80
        padding: 18
        property string originalGroup: ""
        function loadGroup(name) {
            let data = backend.groupData(name)
            originalGroup = name
            groupName.text = data.name || ""
            groupRows.clear()
            for (let i = 0; i < data.accounts.length; ++i)
                groupRows.append({ accountName: data.accounts[i].name, characterClass: data.accounts[i].characterClass })
        }
        function newGroup() {
            originalGroup = ""
            groupName.text = ""
            groupRows.clear()
            groupRows.append({ accountName: "", characterClass: backend.classOptions.length ? backend.classOptions[0] : "" })
        }
        function openEditor() {
            if (backend.groups.length) loadGroup(backend.currentGroup || backend.groups[0]); else newGroup()
            open()
        }
        background: Rectangle { color: "#f00b080a"; radius: 10; border.width: 2; border.color: root.accentSoft }
        ListModel { id: groupRows }
        contentItem: ColumnLayout {
            spacing: 9
            RowLayout {
                Layout.fillWidth: true
                Text { text: "MANAGE ACCOUNT GROUPS"; color: root.textPrimary; font.pixelSize: 18; font.bold: true; Layout.fillWidth: true }
                GlowButton { text: "×"; Layout.preferredWidth: 34; onClicked: managePopup.close() }
            }
            RowLayout {
                Layout.fillWidth: true
                Text { text: "Open group"; color: root.textMuted; Layout.preferredWidth: 86 }
                StyledCombo { id: editGroupCombo; Layout.fillWidth: true; model: backend.groups; onActivated: managePopup.loadGroup(currentText) }
                GlowButton { text: "+ NEW GROUP"; font.pixelSize: 10; onClicked: managePopup.newGroup() }
            }
            RowLayout {
                Layout.fillWidth: true
                Text { text: "Group name"; color: root.textMuted; Layout.preferredWidth: 86 }
                StyledField { id: groupName; Layout.fillWidth: true; placeholderText: "group_name" }
            }
            RowLayout {
                Layout.fillWidth: true
                Text { text: "ACCOUNT NAME"; color: root.textMuted; font.bold: true; Layout.fillWidth: true }
                Text { text: "CLASS"; color: root.textMuted; font.bold: true; Layout.preferredWidth: 180 }
                Item { Layout.preferredWidth: 34 }
            }
            Rectangle {
                Layout.fillWidth: true
                Layout.fillHeight: true
                color: "#85120b0d"
                radius: 5
                border.color: "#684b50"
                ListView {
                    id: groupRowsView
                    anchors.fill: parent; anchors.margins: 5
                    model: groupRows; spacing: 5; clip: true
                    ScrollBar.vertical: ScrollBar { }
                    delegate: RowLayout {
                        id: groupRow
                        required property int index
                        required property string accountName
                        required property string characterClass
                        width: groupRowsView.width - 10
                        height: 38
                        StyledField {
                            Layout.fillWidth: true
                            text: groupRow.accountName
                            placeholderText: "account_name"
                            onTextChanged: groupRows.setProperty(groupRow.index, "accountName", text)
                        }
                        StyledCombo {
                            Layout.preferredWidth: 180
                            model: backend.classOptions
                            Component.onCompleted: currentIndex = Math.max(0, indexOfValue(groupRow.characterClass))
                            onActivated: groupRows.setProperty(groupRow.index, "characterClass", currentText)
                        }
                        StopButton { text: "×"; Layout.preferredWidth: 34; onClicked: groupRows.remove(groupRow.index) }
                    }
                }
            }
            RowLayout {
                Layout.fillWidth: true
                GlowButton { text: "+ ADD ACCOUNT"; font.pixelSize: 10; onClicked: groupRows.append({ accountName: "", characterClass: backend.classOptions.length ? backend.classOptions[0] : "" }) }
                Item { Layout.fillWidth: true }
                StopButton { text: "DELETE GROUP"; font.pixelSize: 10; enabled: managePopup.originalGroup !== ""; onClicked: deleteConfirm.open() }
                GlowButton {
                    text: "SAVE GROUP"
                    font.pixelSize: 10
                    onClicked: {
                        let rows = []
                        for (let i = 0; i < groupRows.count; ++i)
                            rows.push({ name: groupRows.get(i).accountName, characterClass: groupRows.get(i).characterClass })
                        if (backend.saveGroup(managePopup.originalGroup, groupName.text, rows)) {
                            managePopup.originalGroup = groupName.text
                            editGroupCombo.currentIndex = editGroupCombo.indexOfValue(groupName.text)
                        }
                    }
                }
            }
        }
    }

    Popup {
        id: settingsPopup
        objectName: "settingsPopup"
        modal: true; focus: true; closePolicy: Popup.CloseOnEscape
        x: (root.width - width) / 2; y: (root.height - height) / 2
        width: root.width * 0.88; height: root.height * 0.84; padding: 18
        function openEditor() {
            settingsModel.clear()
            let rows = backend.settingsData()
            for (let i = 0; i < rows.length; ++i) settingsModel.append(rows[i])
            open()
        }
        background: Rectangle { color: "#f00b080a"; radius: 10; border.width: 2; border.color: root.accentSoft }
        ListModel { id: settingsModel; dynamicRoles: true }
        contentItem: ColumnLayout {
            spacing: 8
            RowLayout {
                Layout.fillWidth: true
                Text { text: "SETTINGS"; color: root.textPrimary; font.pixelSize: 18; font.bold: true; Layout.fillWidth: true }
                GlowButton { text: "×"; Layout.preferredWidth: 34; onClicked: settingsPopup.close() }
            }
            Rectangle {
                Layout.fillWidth: true; Layout.fillHeight: true
                color: "#85120b0d"; radius: 5; border.color: "#684b50"
                ListView {
                    id: settingsView
                    anchors.fill: parent; anchors.margins: 7; clip: true; spacing: 4
                    model: settingsModel
                    ScrollBar.vertical: ScrollBar { }
                    delegate: Item {
                        id: settingRow
                        required property int index
                        required property string kind
                        required property string section
                        required property string key
                        required property string label
                        required property var value
                        required property string pickKind
                        required property string pickName
                        width: settingsView.width - 12
                        height: settingRow.kind === "header" ? 34 : 38
                        Text {
                            visible: settingRow.kind === "header"
                            anchors.left: parent.left; anchors.verticalCenter: parent.verticalCenter
                            text: settingRow.label; color: root.accent; font.bold: true; font.pixelSize: 13
                        }
                        RowLayout {
                            visible: settingRow.kind !== "header"
                            anchors.fill: parent
                            Text { visible: settingRow.kind === "value"; text: settingRow.label; color: root.textMuted; Layout.preferredWidth: 190; elide: Text.ElideRight }
                            StyledField {
                                visible: settingRow.kind === "value"
                                Layout.fillWidth: true
                                text: settingRow.value
                                onTextChanged: settingsModel.setProperty(settingRow.index, "value", text)
                            }
                            StyledCheckBox {
                                visible: settingRow.kind === "boolean"
                                Layout.fillWidth: true
                                text: settingRow.label
                                checked: settingRow.value === true
                                onToggled: settingsModel.setProperty(settingRow.index, "value", checked)
                            }
                            GlowButton {
                                visible: settingRow.pickKind !== ""
                                text: settingRow.pickKind === "point" ? "SELECT POINT" : "SELECT AREA"
                                Layout.preferredWidth: 112
                                onClicked: backend.pickScreenTarget(settingRow.section, settingRow.pickName)
                            }
                        }
                    }
                }
            }
            RowLayout {
                Layout.fillWidth: true
                Item { Layout.fillWidth: true }
                GlowButton {
                    text: "SAVE"
                    onClicked: {
                        let rows = []
                        for (let i = 0; i < settingsModel.count; ++i) rows.push(settingsModel.get(i))
                        backend.saveSettings(rows)
                    }
                }
            }
        }
    }

    Popup {
        id: alertPopup
        objectName: "alertPopup"
        modal: true; focus: true; closePolicy: Popup.CloseOnEscape
        x: (root.width - width) / 2; y: (root.height - height) / 2
        width: Math.min(root.width * 0.70, 500); padding: 18
        property string dialogTitle: ""
        property string message: ""
        background: Rectangle { color: "#f00b080a"; radius: 9; border.width: 2; border.color: root.accentSoft }
        contentItem: ColumnLayout {
            spacing: 14
            Text { text: alertPopup.dialogTitle; color: root.textPrimary; font.pixelSize: 17; font.bold: true; Layout.fillWidth: true; wrapMode: Text.Wrap }
            Text { text: alertPopup.message; color: "#ffe1e4"; font.pixelSize: 12; Layout.fillWidth: true; wrapMode: Text.Wrap }
            GlowButton { text: "OK"; Layout.alignment: Qt.AlignRight; onClicked: alertPopup.close() }
        }
    }

    Popup {
        id: updateConfirm
        modal: true; focus: true
        x: (root.width - width) / 2; y: (root.height - height) / 2
        width: root.width * 0.68; padding: 18
        property string updateVersion: ""
        background: Rectangle { color: "#f00b080a"; radius: 9; border.width: 2; border.color: root.accentSoft }
        contentItem: ColumnLayout {
            spacing: 14
            Text { text: "UPDATE AVAILABLE"; color: root.textPrimary; font.pixelSize: 17; font.bold: true }
            Text { text: "Version " + updateConfirm.updateVersion + " is available. Install and restart now?"; color: "#ffe1e4"; Layout.fillWidth: true; wrapMode: Text.Wrap }
            RowLayout {
                Layout.alignment: Qt.AlignRight
                GlowButton { text: "NOT NOW"; onClicked: updateConfirm.close() }
                GlowButton { text: "INSTALL"; onClicked: { updateConfirm.close(); backend.installPendingUpdate() } }
            }
        }
    }

    Popup {
        id: changelogPopup
        modal: true; focus: true
        x: (root.width - width) / 2; y: (root.height - height) / 2
        width: root.width * 0.76; height: root.height * 0.48; padding: 18
        property string updateVersion: ""
        property string notes: ""
        background: Rectangle { color: "#f00b080a"; radius: 9; border.width: 2; border.color: root.accentSoft }
        contentItem: ColumnLayout {
            spacing: 10
            Text { text: "WHAT'S NEW IN " + changelogPopup.updateVersion; color: root.textPrimary; font.pixelSize: 17; font.bold: true }
            ScrollView { Layout.fillWidth: true; Layout.fillHeight: true; TextArea { text: changelogPopup.notes; readOnly: true; wrapMode: TextEdit.Wrap; color: "#ffe1e4"; background: null } }
            GlowButton { text: "GOT IT"; Layout.alignment: Qt.AlignRight; onClicked: changelogPopup.close() }
        }
    }

    Popup {
        id: summaryPopup
        modal: true; focus: true
        x: (root.width - width) / 2; y: (root.height - height) / 2
        width: root.width * 0.65; padding: 18
        property int launched: 0
        property int total: 0
        property var failed: []
        property string group: ""
        background: Rectangle { color: "#f00b080a"; radius: 9; border.width: 2; border.color: root.accentSoft }
        contentItem: ColumnLayout {
            spacing: 14
            Text { text: "RUN COMPLETE"; color: root.textPrimary; font.pixelSize: 17; font.bold: true }
            Text { text: "Done " + summaryPopup.launched + "/" + summaryPopup.total + (summaryPopup.failed.length ? "\nFailed: " + summaryPopup.failed.length : ""); color: "#ffe1e4"; font.pixelSize: 13 }
            RowLayout {
                Layout.alignment: Qt.AlignRight
                GlowButton { visible: summaryPopup.failed.length > 0; text: "RETRY"; onClicked: { summaryPopup.close(); backend.retryFailed(summaryPopup.failed, summaryPopup.group) } }
                GlowButton { text: "CLOSE"; onClicked: summaryPopup.close() }
            }
        }
    }

    Popup {
        id: deleteConfirm
        modal: true; focus: true
        x: (root.width - width) / 2; y: (root.height - height) / 2
        width: root.width * 0.66; padding: 18
        background: Rectangle { color: "#f609192d"; radius: 9; border.width: 2; border.color: "#d84a61" }
        contentItem: ColumnLayout {
            spacing: 14
            Text { text: "DELETE GROUP"; color: root.textPrimary; font.pixelSize: 17; font.bold: true }
            Text { text: "Delete group '" + managePopup.originalGroup + "' and all its accounts?"; color: "#ffe1e4"; Layout.fillWidth: true; wrapMode: Text.Wrap }
            RowLayout {
                Layout.alignment: Qt.AlignRight
                GlowButton { text: "CANCEL"; onClicked: deleteConfirm.close() }
                StopButton { text: "DELETE"; onClicked: { if (backend.deleteGroup(managePopup.originalGroup)) { deleteConfirm.close(); managePopup.openEditor() } } }
            }
        }
    }

    Connections {
        target: backend
        function onAlertRequested(title, message, kind) {
            alertPopup.dialogTitle = title
            alertPopup.message = message
            alertPopup.open()
        }
        function onUpdateConfirmationRequested(version) { updateConfirm.updateVersion = version; updateConfirm.open() }
        function onChangelogRequested(version, notes) { changelogPopup.updateVersion = version; changelogPopup.notes = notes; changelogPopup.open() }
        function onSummaryRequested(launched, total, failed, group) { summaryPopup.launched = launched; summaryPopup.total = total; summaryPopup.failed = failed; summaryPopup.group = group; summaryPopup.open() }
        function onSettingPicked(section, key, value) {
            for (let i = 0; i < settingsModel.count; ++i) {
                let row = settingsModel.get(i)
                if (row.section === section && row.key === key) settingsModel.setProperty(i, "value", value)
            }
        }
        function onScreenPickingChanged(active) {
            if (active) {
                root.hide()
            } else {
                root.show()
                root.raise()
                root.requestActivate()
            }
        }
    }
}
