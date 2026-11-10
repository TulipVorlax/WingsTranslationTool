Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms

Public Class LangChoice
    Public Path As String
    Public Label As String

    Public Overrides Function ToString() As String
        Return Label
    End Function
End Class

Public Class frmPair
    Inherits Form

    Private cboRef As ComboBox
    Private cboTgt As ComboBox

    Public Sub New(choices As List(Of LangChoice), selRef As Integer, selTgt As Integer)
        Text = Localization.T("Choose the file pair")
        FormBorderStyle = FormBorderStyle.FixedDialog
        MinimizeBox = False
        MaximizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(470, 168)
        Font = New Font("Segoe UI", 9.0F)

        Dim lblRef As New Label With {
            .Text = Localization.T("Reference file (usually the English version):"),
            .AutoSize = True, .Location = New Point(12, 12)}
        cboRef = New ComboBox With {
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .Location = New Point(12, 32), .Width = 446,
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right}

        Dim lblTgt As New Label With {
            .Text = Localization.T("File to translate (the language you are working on):"),
            .AutoSize = True, .Location = New Point(12, 62)}
        cboTgt = New ComboBox With {
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .Location = New Point(12, 82), .Width = 446,
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right}

        Dim btnOk As New Button With {
            .Text = "OK", .DialogResult = DialogResult.OK,
            .Location = New Point(288, 128), .Size = New Size(86, 28),
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right}
        Dim btnCancel As New Button With {
            .Text = Localization.T("Cancel"), .DialogResult = DialogResult.Cancel,
            .Location = New Point(380, 128), .Size = New Size(86, 28),
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right}
        AcceptButton = btnOk
        CancelButton = btnCancel

        Controls.Add(lblRef)
        Controls.Add(cboRef)
        Controls.Add(lblTgt)
        Controls.Add(cboTgt)
        Controls.Add(btnOk)
        Controls.Add(btnCancel)

        If choices IsNot Nothing Then
            For Each c As LangChoice In choices
                cboRef.Items.Add(c)
                cboTgt.Items.Add(c)
            Next
        End If
        If selRef >= 0 AndAlso selRef < cboRef.Items.Count Then cboRef.SelectedIndex = selRef
        If selTgt >= 0 AndAlso selTgt < cboTgt.Items.Count Then cboTgt.SelectedIndex = selTgt
    End Sub

    Public ReadOnly Property ReferencePath As String
        Get
            If cboRef.SelectedIndex < 0 Then Return ""
            Return DirectCast(cboRef.SelectedItem, LangChoice).Path
        End Get
    End Property

    Public ReadOnly Property TargetPath As String
        Get
            If cboTgt.SelectedIndex < 0 Then Return ""
            Return DirectCast(cboTgt.SelectedItem, LangChoice).Path
        End Get
    End Property
End Class