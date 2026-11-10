Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms

Public Class frmSettings
    Inherits Form

    Private chkBackup As CheckBox
    Private numCount As NumericUpDown
    Private txtFolder As TextBox
    Private btnBrowse As Button
    Private cboLang As ComboBox
    Private btnOk As Button
    Private btnCancel As Button

    Private _langCodes As New List(Of String)

    Public Sub New()
        Text = Localization.T("Settings")
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(480, 280)
        Font = New Font("Segoe UI", 9.0F)

        Dim grp As New GroupBox With {.Text = Localization.T("Backups"), .Bounds = New Rectangle(12, 12, 456, 138)}

        chkBackup = New CheckBox With {
            .Text = Localization.T("Create a backup before saving"),
            .Location = New Point(14, 24), .AutoSize = True}

        Dim lblCount As New Label With {
            .Text = Localization.T("Maximum number of backups kept:"),
            .Location = New Point(30, 54), .AutoSize = True}
        numCount = New NumericUpDown With {
            .Location = New Point(330, 50), .Width = 60, .Minimum = 1, .Maximum = 99}

        Dim lblFolder As New Label With {
            .Text = Localization.T("Backup folder:"),
            .Location = New Point(30, 84), .AutoSize = True}
        txtFolder = New TextBox With {.Location = New Point(30, 102), .Width = 330}
        btnBrowse = New Button With {
            .Text = Localization.T("Browse…"), .Location = New Point(366, 100), .Size = New Size(76, 24)}

        grp.Controls.Add(chkBackup)
        grp.Controls.Add(lblCount)
        grp.Controls.Add(numCount)
        grp.Controls.Add(lblFolder)
        grp.Controls.Add(txtFolder)
        grp.Controls.Add(btnBrowse)

        Dim grpLang As New GroupBox With {.Text = Localization.T("Language"), .Bounds = New Rectangle(12, 156, 456, 70)}
        Dim lblLang As New Label With {
            .Text = Localization.T("Interface language:"),
            .Location = New Point(14, 28), .AutoSize = True}
        cboLang = New ComboBox With {
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .Location = New Point(210, 24), .Width = 200}
        grpLang.Controls.Add(lblLang)
        grpLang.Controls.Add(cboLang)

        btnOk = New Button With {.Text = "OK", .DialogResult = DialogResult.OK, .Location = New Point(288, 240), .Size = New Size(86, 26)}
        btnCancel = New Button With {.Text = Localization.T("Cancel"), .DialogResult = DialogResult.Cancel, .Location = New Point(382, 240), .Size = New Size(86, 26)}
        AcceptButton = btnOk
        CancelButton = btnCancel

        Controls.Add(grp)
        Controls.Add(grpLang)
        Controls.Add(btnOk)
        Controls.Add(btnCancel)

        AddHandler btnBrowse.Click, AddressOf OnBrowse
        AddHandler btnOk.Click, AddressOf OnOk

        ' valeur initiales depuis la configuration courante
        chkBackup.Checked = LangSettings.BackupEnabled
        numCount.Value = LangSettings.BackupCount
        txtFolder.Text = LangSettings.BackupFolder
        FillLanguages()
    End Sub

    Private Sub FillLanguages()
        cboLang.Items.Clear()
        _langCodes.Clear()
        For Each code As String In Localization.AvailableLangs()
            _langCodes.Add(code)
            cboLang.Items.Add(Localization.LanguageName(code))
        Next
        Dim idx As Integer = -1
        For i As Integer = 0 To _langCodes.Count - 1
            If _langCodes(i).Equals(LangSettings.Language, StringComparison.OrdinalIgnoreCase) Then idx = i : Exit For
        Next
        If idx >= 0 Then cboLang.SelectedIndex = idx
    End Sub

    Private Sub OnBrowse(sender As Object, e As EventArgs)
        Dim dlg As New FolderBrowserDialog With {.Description = Localization.T("Choose the backup folder (leave empty for the backups_WingsLangTool folder next to the file)"), .ShowNewFolderButton = True}
        If txtFolder.Text <> "" Then dlg.SelectedPath = txtFolder.Text
        If dlg.ShowDialog(Me) = DialogResult.OK Then txtFolder.Text = dlg.SelectedPath
    End Sub

    Private Sub OnOk(sender As Object, e As EventArgs)
        LangSettings.BackupEnabled = chkBackup.Checked
        LangSettings.BackupCount = CInt(numCount.Value)
        LangSettings.BackupFolder = txtFolder.Text.Trim()
        If cboLang.SelectedIndex >= 0 AndAlso cboLang.SelectedIndex < _langCodes.Count Then
            LangSettings.Language = _langCodes(cboLang.SelectedIndex)
        End If
        LangSettings.Save()
        Localization.Load(LangSettings.Language)
    End Sub
End Class