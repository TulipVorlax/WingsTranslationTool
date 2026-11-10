Imports System.Collections.Generic
Imports System.IO
Imports System.Drawing
Imports System.Text
Imports System.Windows.Forms
Imports Microsoft.Win32
Imports WingsLang.Core

Public Class frmMain
    Inherits Form

    Private _en As LangFile
    Private _fr As LangFile
    Private _report As LangComparer.Report
    Private _frDirty As Boolean = False

    ' Filtres
    Private chkAll As CheckBox
    Private chkMissing As CheckBox
    Private chkDifferent As CheckBox
    Private chkIdentical As CheckBox
    Private chkOnlyFr As CheckBox
    Private chkOrder As CheckBox
    Private txtFilter As TextBox

    ' Arbre et détail
    Private tree As TreeView
    Private lblPath As Label
    Private lblEntryInfo As Label
    Private txtEnValue As TextBox
    Private txtFrValue As TextBox
    Private btnCopy As Button
    Private btnMerge As Button
    Private lblCounts As ToolStripStatusLabel
    Private lblStatus As ToolStripStatusLabel

    Private _asSyncFilter As Boolean = False
    Private _suppressTree As Boolean = False
    Private _selectionKey As String = ""
    Private _currentDiff As DiffEntry
    Private _split As SplitContainer
    Private _splitEdit As SplitContainer

    ' ------------------------------------------------------------------ init

    Public Sub New()
        LangSettings.Load()
        Localization.Load(LangSettings.Language)

        Text = Localization.T("Wings 3D Translation Comparison Tool")
        ClientSize = New Size(1080, 720)
        MinimumSize = New Size(780, 480)
        Font = New Font("Segoe UI", 9.0F)
        StartPosition = FormStartPosition.CenterScreen

        BuildUI()
        RefreshStatus()
    End Sub

    Private Sub BuildUI()
        BuildTree()
        BuildStatusBar()
        BuildFilters()
        BuildMenu()
        BuildDetail()
        RebuildTree()
    End Sub

    ' Reconstruit toute l'interface après un changement de langue.
    Private Sub ApplyLanguage()
        Localization.Load(LangSettings.Language)
        Text = Localization.T("Wings 3D Translation Comparison Tool")
        Controls.Clear()
        BuildUI()
        UpdateCounts()
        RefreshStatus()
    End Sub

    Private Sub RefreshStatus()
        If _en IsNot Nothing AndAlso _fr IsNot Nothing Then
            lblStatus.Text = Localization.Fmt("Reference: {0}   ·   Target: {1}", enName(), frName())
        Else
            lblStatus.Text = Localization.T("Open a languages folder: File → Open Folder…")
        End If
    End Sub

    Private Sub BuildFilters()
        Dim pan As New Panel With {.Dock = DockStyle.Top, .Height = 56, .Padding = New Padding(8, 3, 8, 3)}
        Dim flow As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .WrapContents = True}

        chkAll = New CheckBox With {.Text = Localization.T("All"), .Checked = True, .AutoSize = True, .Margin = New Padding(6, 4, 4, 0)}
        chkMissing = New CheckBox With {.Text = Localization.T("Missing"), .AutoSize = True, .Margin = New Padding(8, 4, 4, 0)}
        chkDifferent = New CheckBox With {.Text = Localization.T("Different"), .AutoSize = True, .Margin = New Padding(8, 4, 4, 0)}
        chkIdentical = New CheckBox With {.Text = Localization.T("Identical"), .AutoSize = True, .Margin = New Padding(8, 4, 4, 0)}
        chkOnlyFr = New CheckBox With {.Text = Localization.T("Only in target"), .AutoSize = True, .Margin = New Padding(8, 4, 4, 0)}
        chkOrder = New CheckBox With {.Text = Localization.T("Moved order"), .AutoSize = True, .Margin = New Padding(8, 4, 4, 0)}
        Dim lblFind As New Label With {.Text = Localization.T("Search:"), .AutoSize = True, .Margin = New Padding(14, 5, 2, 0)}
        txtFilter = New TextBox With {.Width = 170, .Margin = New Padding(2, 4, 0, 0)}
        Dim legend As New Label With {
            .Text = Localization.T("Colors: red missing · orange different · gray identical · blue only in target"),
            .AutoSize = True, .Margin = New Padding(16, 5, 0, 0), .ForeColor = Color.Gray}

        flow.Controls.Add(chkAll)
        flow.Controls.Add(chkMissing)
        flow.Controls.Add(chkDifferent)
        flow.Controls.Add(chkIdentical)
        flow.Controls.Add(chkOnlyFr)
        flow.Controls.Add(chkOrder)
        flow.Controls.Add(lblFind)
        flow.Controls.Add(txtFilter)
        flow.Controls.Add(legend)

        pan.Controls.Add(flow)

        AddHandler chkAll.CheckedChanged, AddressOf OnFilterChanged
        AddHandler chkMissing.CheckedChanged, AddressOf OnFilterChanged
        AddHandler chkDifferent.CheckedChanged, AddressOf OnFilterChanged
        AddHandler chkIdentical.CheckedChanged, AddressOf OnFilterChanged
        AddHandler chkOnlyFr.CheckedChanged, AddressOf OnFilterChanged
        AddHandler chkOrder.CheckedChanged, AddressOf OnFilterChanged
        AddHandler txtFilter.TextChanged, AddressOf OnFilterChanged

        Controls.Add(pan)
    End Sub

    Private Sub BuildTree()
        _split = New SplitContainer With {.Dock = DockStyle.Fill, .SplitterDistance = 430, .Panel1MinSize = 260, .FixedPanel = FixedPanel.Panel1}
        tree = New TreeView With {
            .Dock = DockStyle.Fill, .HideSelection = False, .FullRowSelect = True,
            .ShowNodeToolTips = True, .BorderStyle = BorderStyle.FixedSingle}
        AddHandler tree.AfterSelect, AddressOf OnTreeAfterSelect
        _split.Panel1.Controls.Add(tree)

        Controls.Add(_split)
    End Sub

    Private Sub BuildDetail()
        Dim pan As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(0) }
        Dim grp As New GroupBox With {.Dock = DockStyle.Fill, .Text = Localization.T("Entry details"), .Padding = New Padding(6)}

        lblPath = New Label With {.Dock = DockStyle.Top, .Height = 24, .Font = New Font(Font, FontStyle.Bold), .AutoEllipsis = True}
        lblEntryInfo = New Label With {.Dock = DockStyle.Top, .Height = 20, .AutoEllipsis = True}

        Dim btnRow As New Panel With {.Dock = DockStyle.Top, .Height = 34}
        btnCopy = New Button With {.Text = Localization.T("Copy reference → target"), .AutoSize = False, .Width = 150, .Height = 26, .Location = New Point(0, 3), .Enabled = False}
        btnMerge = New Button With {.Text = Localization.T("Complete Target from Reference"), .AutoSize = False, .Width = 210, .Height = 26, .Location = New Point(158, 3), .Enabled = True}
        AddHandler btnCopy.Click, AddressOf DoCopy
        AddHandler btnMerge.Click, AddressOf DoMerge
        btnRow.Controls.Add(btnCopy)
        btnRow.Controls.Add(btnMerge)

        Dim splitEdit As New SplitContainer With {.Dock = DockStyle.Fill, .Orientation = Orientation.Vertical, .SplitterDistance = 230, .FixedPanel = FixedPanel.Panel1}
        _splitEdit = splitEdit

        Dim lblEnCap As New Label With {.Dock = DockStyle.Top, .Height = 18, .Text = Localization.T("Reference (read-only)"), .BackColor = Color.FromArgb(255, 255, 225)}
        txtEnValue = New TextBox With {.Dock = DockStyle.Fill, .Multiline = True, .ScrollBars = ScrollBars.Vertical, .ReadOnly = True, .BackColor = Color.FromArgb(255, 255, 238), .BorderStyle = BorderStyle.FixedSingle}
        splitEdit.Panel1.Controls.Add(txtEnValue)
        splitEdit.Panel1.Controls.Add(lblEnCap)

        Dim lblFrCap As New Label With {.Dock = DockStyle.Top, .Height = 18, .Text = Localization.T("Translation (editable, applied when the field loses focus)"), .BackColor = Color.FromArgb(225, 255, 225)}
        txtFrValue = New TextBox With {.Dock = DockStyle.Fill, .Multiline = True, .ScrollBars = ScrollBars.Vertical, .BorderStyle = BorderStyle.FixedSingle}
        AddHandler txtFrValue.Leave, AddressOf OnFrLeave
        splitEdit.Panel2.Controls.Add(txtFrValue)
        splitEdit.Panel2.Controls.Add(lblFrCap)

        grp.Controls.Add(splitEdit)
        grp.Controls.Add(btnRow)
        grp.Controls.Add(lblEntryInfo)
        grp.Controls.Add(lblPath)
        pan.Controls.Add(grp)

        ' Le détail est placé dans le panneau droit du split principal créé dans BuildTree.
        If _split IsNot Nothing Then _split.Panel2.Controls.Add(pan) Else Controls.Add(pan)
    End Sub

    Private Sub BuildStatusBar()
        Dim bar As New StatusStrip With {.SizingGrip = False}
        lblCounts = New ToolStripStatusLabel With {.Spring = True, .TextAlign = ContentAlignment.MiddleLeft}
        lblStatus = New ToolStripStatusLabel With {.BorderSides = ToolStripStatusLabelBorderSides.Left}
        bar.Items.Add(lblCounts)
        bar.Items.Add(lblStatus)
        Controls.Add(bar)
    End Sub

    Private Sub BuildMenu()
        Dim ms = New MenuStrip With {.Dock = DockStyle.Top}
        Dim mFichier = New ToolStripMenuItem(Localization.T("File"))
        Dim mOuvrir = New ToolStripMenuItem(Localization.T("Open Languages Folder…"))
        mOuvrir.ShortcutKeys = Keys.Control Or Keys.O
        mOuvrir.Tag = "folder"
        Dim mFr = New ToolStripMenuItem(Localization.T("Load File to Translate…"))
        mFr.Tag = "tgt"
        Dim mEn = New ToolStripMenuItem(Localization.T("Load Reference File…"))
        mEn.Tag = "ref"
        Dim mSave = New ToolStripMenuItem(Localization.T("Save Translated File"))
        mSave.Tag = "save"
        mSave.ShortcutKeys = Keys.Control Or Keys.S
        Dim mSaveAs = New ToolStripMenuItem(Localization.T("Save Translated File As…"))
        mSaveAs.Tag = "saveas"
        Dim mExport = New ToolStripMenuItem(Localization.T("Export Text Report…"))
        mExport.Tag = "export"
        Dim mQuit = New ToolStripMenuItem(Localization.T("Exit"))
        mQuit.Tag = "quit"
        mFichier.DropDownItems.AddRange(New ToolStripItem() {mOuvrir, mFr, mEn, New ToolStripSeparator(), mSave, mSaveAs, mExport, New ToolStripSeparator(), mQuit})

        Dim mOutils = New ToolStripMenuItem(Localization.T("Tools"))
        Dim mCopy = New ToolStripMenuItem(Localization.T("Copy Reference → Target (Current Entry)"))
        mCopy.Tag = "copy"
        Dim mMerge = New ToolStripMenuItem(Localization.T("Complete Target from Reference"))
        mMerge.Tag = "merge"
        Dim mRefresh = New ToolStripMenuItem(Localization.T("Recompare"))
        mRefresh.ShortcutKeys = Keys.F5
        mRefresh.Tag = "refresh"
        Dim mSettings = New ToolStripMenuItem(Localization.T("Settings…"))
        mSettings.Tag = "settings"
        mOutils.DropDownItems.AddRange(New ToolStripItem() {mCopy, mMerge, New ToolStripSeparator(), mRefresh, New ToolStripSeparator(), mSettings})

        Dim mAide = New ToolStripMenuItem(Localization.T("Help"))
        Dim mAPropos = New ToolStripMenuItem(Localization.T("About…"))
        mAPropos.Tag = "about"
        mAide.DropDownItems.Add(mAPropos)

        ms.Items.AddRange(New ToolStripItem() {mFichier, mOutils, mAide})
        For Each it As ToolStripItem In AllMenuItems(ms)
            AddHandler it.Click, AddressOf OnMenu
        Next
        Controls.Add(ms)
    End Sub

    Private Function AllMenuItems(ms As MenuStrip) As List(Of ToolStripItem)
        Dim res As New List(Of ToolStripItem)
        For Each top As ToolStripItem In ms.Items
            res.Add(top)
            Dim subD = TryCast(top, ToolStripMenuItem)
            If subD IsNot Nothing Then
                res.AddRange(AllDropItems(subD))
            End If
        Next
        Return res
    End Function

    Private Function AllDropItems(m As ToolStripMenuItem) As List(Of ToolStripItem)
        Dim res As New List(Of ToolStripItem)
        For Each it As ToolStripItem In m.DropDownItems
            res.Add(it)
            Dim subD = TryCast(it, ToolStripMenuItem)
            If subD IsNot Nothing Then
                res.AddRange(AllDropItems(subD))
            End If
        Next
        Return res
    End Function

    ' ------------------------------------------------------------------ menu

    Private Sub OnMenu(sender As Object, e As EventArgs)
        Dim mi = DirectCast(sender, ToolStripMenuItem)
        Select Case mi.Tag
            Case "folder" : OpenFolder()
            Case "tgt" : OpenSingleFile(False)
            Case "ref" : OpenSingleFile(True)
            Case "save" : SaveFr(False)
            Case "saveas" : SaveFr(True)
            Case "export" : ExportReport()
            Case "quit" : Close()
            Case "copy" : DoCopy()
            Case "merge" : DoMerge()
            Case "refresh" : RefreshComparison(_selectionKey)
            Case "settings"
                Using s As New frmSettings()
                    If s.ShowDialog(Me) = DialogResult.OK Then ApplyLanguage()
                End Using
            Case "about" : MessageBox.Show(Localization.T("Wings 3D translation comparison tool, usable for any language (reference -> translation)") & vbCrLf & vbCrLf & Localization.T("Core: WingsLangCore (Erlang .lang parser, exact round-trip)") & vbCrLf & Localization.T("UI: VB.NET WinForms"), Localization.T("About…"), MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Select
    End Sub

    ' ------------------------------------------------------------------ fichier

    ' Retrouve le dossier d'installation de Wings 3D via le registre
    ' (HKLM\SOFTWARE\WOW6432Node\Wings 3D : DefaultVersion puis clé de version).
    ' Peut être vide si l'installation n'est pas détectée.
    Private Shared Function WingsInstallDir() As String
        Dim rootKey As String = "Software\Wings 3D"
        For Each view As RegistryView In {RegistryView.Registry32, RegistryView.Registry64}
            Try
                Using base = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view)
                    Using def = base.OpenSubKey(rootKey, False)
                        If def Is Nothing Then Continue For
                        Dim versionKey As RegistryKey = def.OpenSubKey("DefaultVersion", False)
                        If versionKey Is Nothing Then Continue For
                        Dim version As String = TryCast(versionKey.GetValue(""), String)
                        versionKey.Close()
                        If version = "" Then Continue For
                        Using ver = def.OpenSubKey(version, False)
                            If ver Is Nothing Then Continue For
                            Dim dirPath As String = TryCast(ver.GetValue(""), String)
                            If dirPath <> "" AndAlso Directory.Exists(dirPath) Then Return dirPath
                        End Using
                    End Using
                End Using
            Catch
                ' registre inaccessible : on essaie la vue suivante
            End Try
        Next
        Return ""
    End Function

    Private Sub OpenFolder()
        Dim wingsDir As String = WingsInstallDir()
        Dim dlg As New FolderBrowserDialog With {
            .Description = If(wingsDir <> "",
                Localization.Fmt("Wings 3D folder detected: {0}", wingsDir) & vbCrLf &
                Localization.Fmt("Choose the folder containing the .lang files (usually {0}\lib\wings-{1}).", wingsDir, Path.GetFileName(wingsDir).Replace("wings3d_", "")),
                Localization.T("Choose the folder containing the .lang files (e.g. ...\wings3d_2.4.1\lib\wings-2.4.1)")),
            .ShowNewFolderButton = False}
        If wingsDir <> "" Then dlg.SelectedPath = wingsDir
        If dlg.ShowDialog(Me) <> DialogResult.OK Then Return

        Dim choices = CollectLangFiles(dlg.SelectedPath)
        If choices.Count = 0 Then
            MessageBox.Show(Localization.T("No .lang file found in this folder."), _
                            Localization.T("No File Found"), MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        ' Pré-sélection : référence = un fichier « *_en.lang », traduction = le même module en « _fr »,
        ' sinon un autre suffixe, sinon un autre fichier quelconque.
        Dim selRef As Integer = 0
        Dim selTgt As Integer = If(choices.Count > 1, 1, 0)
        Dim enIndex As Integer = IndexOfLang(choices, "en")
        If enIndex >= 0 Then
            selRef = enIndex
            Dim baseRef = BaseDirName(choices(enIndex).Path).ToLowerInvariant()
            Dim frIndex As Integer = -1
            Dim otherIndex As Integer = -1
            For i As Integer = 0 To choices.Count - 1
                If i = enIndex Then Continue For
                If BaseDirName(choices(i).Path).ToLowerInvariant() = baseRef Then
                    If choices(i).Path.ToLowerInvariant().EndsWith("_fr.lang") AndAlso frIndex = -1 Then frIndex = i
                    If otherIndex = -1 Then otherIndex = i
                End If
            Next
            If frIndex >= 0 Then selTgt = frIndex Else If otherIndex >= 0 Then selTgt = otherIndex
        End If

        Dim p As New frmPair(choices, selRef, selTgt)
        If p.ShowDialog(Me) <> DialogResult.OK Then Return
        If p.ReferencePath = "" OrElse p.TargetPath = "" Then Return
        If String.Equals(p.ReferencePath, p.TargetPath, StringComparison.OrdinalIgnoreCase) Then
            MessageBox.Show(Localization.T("Choose two different files (reference and translation)."), _
                            Localization.T("Selection"), MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        LoadPair(p.ReferencePath, p.TargetPath)
    End Sub

    Private Shared Function IndexOfLang(choices As List(Of LangChoice), lang As String) As Integer
        For i As Integer = 0 To choices.Count - 1
            If choices(i).Path.ToLowerInvariant().EndsWith("_" & lang & ".lang") Then Return i
        Next
        Return -1
    End Function

    Private Shared Function BaseDirName(path As String) As String
        Dim n = System.IO.Path.GetFileNameWithoutExtension(path)
        Dim i = n.LastIndexOf("_"c)
        If i > 0 Then Return n.Substring(0, i)
        Return n
    End Function

    Private Function CollectLangFiles(dir As String) As List(Of LangChoice)
        Dim res As New List(Of LangChoice)
        For Each p As String In Directory.GetFiles(dir, "*.lang", SearchOption.AllDirectories)
            Dim rel As String = ""
            If p.ToLowerInvariant().StartsWith(dir.ToLowerInvariant()) Then
                rel = p.Substring(dir.Length).TrimStart("\"c, "/"c)
            End If
            Dim folder = System.IO.Path.GetDirectoryName(rel)
            res.Add(New LangChoice With {
                .Path = p,
                .Label = LangNameOf(p) & If(folder = "", "", "  —  " & folder)})
        Next
        res.Sort(AddressOf CompareChoices)
        Return res
    End Function

    Private Shared Function LangNameOf(path As String) As String
        Dim n = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant()
        Dim i = n.LastIndexOf("_"c)
        If i > 0 AndAlso n.Length - i - 1 >= 2 AndAlso n.Length - i - 1 <= 8 Then
            Return n.Substring(0, i) & " (" & n.Substring(i + 1) & ")"
        End If
        Return n
    End Function

    Private Shared Function CompareChoices(a As LangChoice, b As LangChoice) As Integer
        Return String.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub OpenSingleFile(isReference As Boolean)
        Dim dlg As New OpenFileDialog With {
            .Filter = Localization.T("Lang files (*.lang)|*.lang|All files (*.*)|*.*"),
            .Title = If(isReference, Localization.T("Choose the reference file"), Localization.T("Choose the file to translate"))}
        If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
        Try
            If isReference Then
                _en = LangFile.Load(dlg.FileName)
            Else
                _fr = LangFile.Load(dlg.FileName)
                _frDirty = False
            End If
            RefreshStatus()
            RefreshComparison("")
        Catch ex As Exception
            MessageBox.Show(Localization.Fmt("Load error: {0}", ex.Message), Localization.T("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub LoadPair(refPath As String, tgtPath As String)
        Try
            _en = LangFile.Load(refPath)
            _fr = LangFile.Load(tgtPath)
            _frDirty = False
            lblStatus.Text = Localization.Fmt("Reference: {0}   ·   Target: {1}", refPath, tgtPath)
            RefreshComparison("")
        Catch ex As Exception
            MessageBox.Show(Localization.Fmt("Load error: {0}", ex.Message), Localization.T("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Function enName() As String
        If _en Is Nothing OrElse _en.FilePath = "" Then Return "—"
        Return System.IO.Path.GetFileName(_en.FilePath)
    End Function

    Private Function frName() As String
        If _fr Is Nothing OrElse _fr.FilePath = "" Then Return "—"
        Return System.IO.Path.GetFileName(_fr.FilePath)
    End Function

    Private Sub SaveFr(saveAs As Boolean)
        If _fr Is Nothing Then
            MessageBox.Show(Localization.T("No file to translate loaded."), Localization.T("Save"), MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If saveAs OrElse _fr.FilePath = "" OrElse Not File.Exists(_fr.FilePath) Then
            Dim dlg As New SaveFileDialog With {
                .Filter = Localization.T("Lang files (*.lang)|*.lang"),
                .FileName = If(_fr.FilePath <> "", System.IO.Path.GetFileName(_fr.FilePath), Localization.T("translation.lang"))}
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            _fr.FilePath = dlg.FileName
        End If
        Try
            Dim bak As String = LangSettings.EnsureBackup(_fr.FilePath)
            _fr.Save()
            _frDirty = False
            lblStatus.Text = If(bak = "", Localization.Fmt("Saved: {0}", _fr.FilePath), Localization.Fmt("Saved: {0}   ·   backup: {1}", _fr.FilePath, bak))
        Catch ex As Exception
            MessageBox.Show(Localization.T("Could not save:") & vbCrLf & ex.Message & vbCrLf & vbCrLf & _
                            Localization.T("The file may be in a protected folder: use ""Save As…"" to save to an accessible location."), _
                            Localization.T("Save Error"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ExportReport()
        If _report Is Nothing Then
            MessageBox.Show(Localization.T("Compare files first (File menu)."), Localization.T("Export Report"), MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim dlg As New SaveFileDialog With {
            .Filter = Localization.T("Text files (*.txt)|*.txt"),
            .FileName = Localization.T("translation_report.txt")}
        If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
        File.WriteAllText(dlg.FileName, BuildReportText(), New UTF8Encoding(False))
        lblStatus.Text = Localization.Fmt("Report written: {0}", dlg.FileName)
    End Sub

    Private Function BuildReportText() As String
        Dim sb As New StringBuilder
        sb.AppendLine(Localization.T("Translation comparison report (reference -> target)"))
        sb.AppendLine("================================================================")
        sb.AppendLine(Localization.Fmt("Generated {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm")))
        sb.AppendLine(Localization.Fmt("Reference: {0}", enName()))
        sb.AppendLine(Localization.Fmt("Target: {0}", frName()))
        sb.AppendLine()
        sb.AppendLine(Localization.T("Summary:"))
        sb.AppendLine("  " & Localization.Fmt("Entries (reference): {0}", _report.EnCount))
        sb.AppendLine("  " & Localization.Fmt("Entries (target): {0}", _report.FrCount))
        sb.AppendLine("  " & Localization.Fmt("Identical: {0}   (check if intentional)", _report.Identical))
        sb.AppendLine("  " & Localization.Fmt("Different: {0}", _report.Different))
        sb.AppendLine("  " & Localization.Fmt("Missing in target: {0}", _report.MissingEntries))
        sb.AppendLine("  " & Localization.Fmt("Only in target: {0}", _report.OnlyFrEntries))
        sb.AppendLine("  " & Localization.Fmt("Missing groups: {0}", _report.MissingGroups))
        sb.AppendLine("  " & Localization.Fmt("Groups only in target: {0}", _report.OnlyFrGroups))
        sb.AppendLine("  " & Localization.Fmt("Missing modules: {0}", _report.MissingModules))
        sb.AppendLine()

        For Each dm As DiffModule In _report.Modules
            If dm.Groups.Count = 0 Then Continue For
            sb.AppendLine(Localization.Fmt("Module {0}{1}  (reference line {2}{3})", dm.Key, If(dm.OrderDiffer, "  " & Localization.T("[ORDER DIFFERENT]"), ""), dm.EnLine, If(dm.FrLine > 0, Localization.Fmt(", target line {0}", dm.FrLine), "")))
            For Each dg As DiffGroup In dm.Groups
                sb.AppendLine("  " & Localization.Fmt("Group {0}  ({1})", dg.Key, StatusGroupName(dg.Status)))
                For Each de As DiffEntry In dg.Entries
                    If de.Status = EntryStatus.Same Then Continue For
                    sb.AppendLine("    " & Localization.Fmt("- {0} [{1}]", de.Key, StatusName(de.Status)))
                    sb.AppendLine("        " & Localization.Fmt("reference (line {0}): {1}", de.EnLine, ShowVal(de.EnValue)))
                    If de.Status = EntryStatus.Different Then
                        sb.AppendLine("        " & Localization.Fmt("target (line {0}): {1}", de.FrLine, ShowVal(de.FrValue)))
                    End If
                Next
            Next
            sb.AppendLine()
        Next
        Return sb.ToString()
    End Function

    Private Shared Function ShowVal(v As String) As String
        If v Is Nothing Then Return Localization.T("(empty)")
        Dim s = v.Replace(vbCr, "\r").Replace(vbLf, "\n").Replace(vbTab, "\t")
        s = "\""" & s & "\"""
        If s.Length > 125 Then s = s.Substring(0, 122) & "..."
        Return s
    End Function

    Private Shared Function StatusName(s As EntryStatus) As String
        Select Case s
            Case EntryStatus.Same : Return Localization.T("Identical entry")
            Case EntryStatus.Different : Return Localization.T("Different entry")
            Case EntryStatus.MissingInTarget : Return Localization.T("Missing entry")
            Case Else : Return Localization.T("Only in target")
        End Select
    End Function

    Private Shared Function StatusGroupName(s As GroupStatus) As String
        Select Case s
            Case GroupStatus.MissingInTarget : Return Localization.T("Missing group")
            Case GroupStatus.OnlyInTarget : Return Localization.T("Only in target")
            Case Else : Return Localization.T("Common")
        End Select
    End Function

    ' ------------------------------------------------------------------ actions

    Private Sub DoCopy()
        Dim de = _currentDiff
        If de Is Nothing Then Return
        If Not de.CanEdit Then Return
        If de.EnEntry Is Nothing Then
            MessageBox.Show(Localization.T("This entry has no match in the reference."), Localization.T("Copy"), MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Try
            de.FrEntry.Value = de.EnEntry.Value
        Catch ex As Exception
            MessageBox.Show(Localization.Fmt("Could not copy: {0}", ex.Message), Localization.T("Copy"), MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try
        _frDirty = True
        RefreshComparison(KeyOf(de))
    End Sub

    Private Sub DoMerge()
        If _en Is Nothing OrElse _fr Is Nothing Then
            MessageBox.Show(Localization.T("Load the reference and translation files first."), Localization.T("Complete Target"), MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim added = _fr.AddMissingFrom(_en)
        If added > 0 Then _frDirty = True
        RefreshComparison("")
        MessageBox.Show(Localization.Fmt("Done: {0} entry(ies) added to the translation file.", added) & vbCrLf & _
                        Localization.T("Remember to save (Ctrl+S)."), Localization.T("Complete Target"), MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    ' ------------------------------------------------------------------ filtres

    Private Sub OnFilterChanged(sender As Object, e As EventArgs)
        If _asSyncFilter Then Return
        _asSyncFilter = True
        SyncFilterCheckboxes()
        _asSyncFilter = False
        RebuildTree()
    End Sub

    Private Sub SyncFilterCheckboxes()
        ' Cocher « Tout » désélectionne les filtres, et inversement.
        Dim explicit As Boolean = chkMissing.Checked OrElse chkDifferent.Checked OrElse _
            chkIdentical.Checked OrElse chkOnlyFr.Checked OrElse chkOrder.Checked
        If explicit Then
            chkAll.Checked = False
        ElseIf Not chkAll.Checked Then
            chkAll.Checked = True
        End If
    End Sub

    Private Function EntryShown(de As DiffEntry, dm As DiffModule) As Boolean
        If chkAll.Checked Then Return TextMatches(de)
        Dim keep As Boolean = False
        If chkMissing.Checked AndAlso de.Status = EntryStatus.MissingInTarget Then keep = True
        If chkDifferent.Checked AndAlso de.Status = EntryStatus.Different Then keep = True
        If chkIdentical.Checked AndAlso de.Status = EntryStatus.Same Then keep = True
        If chkOnlyFr.Checked AndAlso de.Status = EntryStatus.OnlyInTarget Then keep = True
        If chkOrder.Checked AndAlso dm.OrderDiffer Then keep = True
        If Not keep Then Return False
        Return TextMatches(de)
    End Function

    Private Function TextMatches(de As DiffEntry) As Boolean
        Dim f = txtFilter.Text.Trim()
        If f = "" Then Return True
        Return f.IndexOf(de.Key, StringComparison.OrdinalIgnoreCase) >= 0 OrElse _
               f.IndexOf(de.ModuleKey, StringComparison.OrdinalIgnoreCase) >= 0 OrElse _
               f.IndexOf(de.GroupKey, StringComparison.OrdinalIgnoreCase) >= 0 OrElse _
               f.IndexOf(de.EnValue, StringComparison.OrdinalIgnoreCase) >= 0 OrElse _
               f.IndexOf(de.FrValue, StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    ' ------------------------------------------------------------------ arbre

    Private Sub RebuildTree()
        tree.BeginUpdate()
        tree.Nodes.Clear()

        If _report Is Nothing Then
            tree.EndUpdate()
            Return
        End If

        Dim prevSel = _selectionKey

        For Each dm As DiffModule In _report.Modules
            Dim mn As New TreeNode With {.Text = dm.Key, .Tag = dm}
            If dm.OrderDiffer Then mn.ForeColor = Color.FromArgb(180, 20, 0)
            Dim moduleHasNodes As Boolean = False

            For Each dg As DiffGroup In dm.Groups
                Dim gn As New TreeNode With {.Text = dg.Key, .Tag = dg}
                Select Case dg.Status
                    Case GroupStatus.MissingInTarget : gn.ForeColor = Color.Red
                    Case GroupStatus.OnlyInTarget : gn.ForeColor = Color.RoyalBlue
                End Select
                Dim groupHasNodes As Boolean = False

                For Each de As DiffEntry In dg.Entries
                    If Not EntryShown(de, dm) Then Continue For
                    Dim en As New TreeNode With {.Text = EntryLabel(de), .Tag = de}
                    Select Case de.Status
                        Case EntryStatus.Same : en.ForeColor = Color.Gray
                        Case EntryStatus.Different : en.ForeColor = Color.DarkOrange
                        Case EntryStatus.MissingInTarget : en.ForeColor = Color.Red
                        Case Else : en.ForeColor = Color.RoyalBlue
                    End Select
                    en.ToolTipText = Localization.Fmt("Reference: {0}", de.EnValue) & vbCrLf & Localization.Fmt("Target: {0}", de.FrValue)
                    gn.Nodes.Add(en)
                    groupHasNodes = True
                Next

                If groupHasNodes Then
                    mn.Nodes.Add(gn)
                    moduleHasNodes = True
                End If
            Next

            If moduleHasNodes Then
                tree.Nodes.Add(mn)
            End If
        Next

        tree.EndUpdate()

        If prevSel <> "" Then SelectNodeByKey(prevSel)
    End Sub

    Private Shared Function EntryLabel(de As DiffEntry) As String
        Dim v = de.FrValue
        If de.Status = EntryStatus.MissingInTarget Then v = de.EnValue
        Dim s = de.Key
        If v IsNot Nothing AndAlso v <> "" Then
            Dim p = v
            If p.Length > 34 Then p = p.Substring(0, 31) & "…"
            s = s & " : " & p
        End If
        Return s
    End Function

    Private Sub SelectNodeByKey(key As String)
        Dim target As TreeNode = Nothing
        FindNodeRec(tree.Nodes, key, target)
        If target Is Nothing Then Return
        _suppressTree = True
        tree.SelectedNode = target
        target.EnsureVisible()
        _suppressTree = False
    End Sub

    Private Sub FindNodeRec(col As TreeNodeCollection, key As String, ByRef target As TreeNode)
        For Each n As TreeNode In col
            If target IsNot Nothing Then Exit For
            If TypeOf n.Tag Is DiffEntry AndAlso KeyOf(DirectCast(n.Tag, DiffEntry)) = key Then
                target = n
                Exit For
            End If
            FindNodeRec(n.Nodes, key, target)
        Next
    End Sub

    Private Shared Function KeyOf(de As DiffEntry) As String
        Return de.ModuleKey & "|" & de.GroupKey & "|" & de.Key
    End Function

    Private Sub OnTreeAfterSelect(sender As Object, e As TreeViewEventArgs)
        If _suppressTree Then Return
        Dim n = e.Node
        If n Is Nothing Then Return
        If TypeOf n.Tag Is DiffEntry Then
            Dim de = DirectCast(n.Tag, DiffEntry)
            _selectionKey = KeyOf(de)
            ShowEntry(de)
        ElseIf TypeOf n.Tag Is DiffGroup Then
            ShowGroup(DirectCast(n.Tag, DiffGroup))
        ElseIf TypeOf n.Tag Is DiffModule Then
            ShowModule(DirectCast(n.Tag, DiffModule))
        End If
    End Sub

    ' ------------------------------------------------------------------ détail

    Private Sub ShowEntry(de As DiffEntry)
        _currentDiff = de
        lblPath.Text = de.ModuleKey & "  ›  " & de.GroupKey & "  ›  " & de.Key
        lblEntryInfo.Text = StatusName(de.Status) & "   ·   " & Localization.Fmt("reference line {0}", de.EnLine) & "   ·   " & Localization.Fmt("target line {0}", de.FrLine)
        txtEnValue.Text = de.EnValue

        Dim editable As Boolean = de.CanEdit
        txtFrValue.ReadOnly = Not editable
        txtFrValue.BackColor = If(editable, Color.White, Color.FromArgb(240, 240, 240))
        btnCopy.Enabled = editable AndAlso de.EnEntry IsNot Nothing
        txtFrValue.Text = de.FrValue
    End Sub

    Private Sub ShowGroup(dg As DiffGroup)
        _currentDiff = Nothing
        lblPath.Text = dg.ModuleKey & "  ›  " & Localization.T("Group") & " " & dg.Key
        lblEntryInfo.Text = StatusGroupName(dg.Status) & "   ·   " & Localization.Fmt("groups: {0} entry(ies)", dg.Entries.Count)
        Dim sb As New StringBuilder
        sb.AppendLine(Localization.Fmt("Group: {0}", dg.Key))
        sb.AppendLine(Localization.Fmt("Group status: {0}", StatusGroupName(dg.Status)))
        sb.AppendLine()
        For Each de As DiffEntry In dg.Entries
            sb.AppendLine("    " & Localization.Fmt("- {0} [{1}]", de.Key, StatusName(de.Status)))
            sb.AppendLine("    " & Localization.Fmt("Reference value: {0}", de.EnValue))
            sb.AppendLine("    " & Localization.Fmt("Target value: {0}", de.FrValue))
        Next
        txtEnValue.Text = sb.ToString()
        txtFrValue.Text = ""
        txtFrValue.ReadOnly = True
        txtFrValue.BackColor = Color.FromArgb(240, 240, 240)
        btnCopy.Enabled = False
    End Sub

    Private Sub ShowModule(dm As DiffModule)
        _currentDiff = Nothing
        lblPath.Text = Localization.Fmt("Module {0}", dm.Key)
        lblEntryInfo.Text = If(dm.OrderDiffer, Localization.T("Group order different from reference"), Localization.T("Same order")) & _
                            "   ·   " & Localization.Fmt("group(s) displayed: {0}", dm.Groups.Count)
        txtEnValue.Text = ""
        txtFrValue.Text = ""
        txtFrValue.ReadOnly = True
        txtFrValue.BackColor = Color.FromArgb(240, 240, 240)
        btnCopy.Enabled = False
    End Sub

    Private Sub OnFrLeave(sender As Object, e As EventArgs)
        CommitFrValue()
    End Sub

    Private Sub CommitFrValue()
        Dim de = _currentDiff
        If de Is Nothing OrElse de.FrEntry Is Nothing Then Return
        If Not txtFrValue.ReadOnly AndAlso txtFrValue.Text <> de.FrValue Then
            Try
                de.FrEntry.Value = txtFrValue.Text
            Catch ex As Exception
                MessageBox.Show(Localization.Fmt("Could not apply: {0}", ex.Message), Localization.T("Editing"), MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End Try
            de.FrValue = txtFrValue.Text
            _frDirty = True
            RefreshComparison(_selectionKey)
        End If
    End Sub

    Private Sub RefreshComparison(selectKey As String)
        _selectionKey = selectKey
        If _en Is Nothing OrElse _fr Is Nothing Then
            _report = Nothing
            RebuildTree()
            UpdateCounts()
            Return
        End If
        _report = LangComparer.Compare(_en, _fr)
        RebuildTree()
        UpdateCounts()
    End Sub

    ' ------------------------------------------------------------------ état

    Private Sub UpdateCounts()
        If _report Is Nothing Then
            lblCounts.Text = Localization.T("— no file loaded —")
            Return
        End If
        Dim order = 0
        For Each dm As DiffModule In _report.Modules
            If dm.OrderDiffer Then order += 1
        Next
        lblCounts.Text = Localization.Fmt("Identical: {0}    Different: {1}    Missing: {2}    Only in target: {3}    Moved order: {4} module(s)    Reference: {5} / Target: {6}",
                                       _report.Identical, _report.Different, _report.MissingEntries, _report.OnlyFrEntries,
                                       order, _report.EnCount, _report.FrCount)
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        If _split IsNot Nothing Then _split.SplitterDistance = 430
        If _splitEdit IsNot Nothing Then _splitEdit.SplitterDistance = 230
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If _frDirty Then
            Dim r = MessageBox.Show(Localization.T("The translation file contains unsaved changes.") & vbCrLf & _
                                    Localization.T("Quit without saving?"), Localization.T("Unsaved changes"), _
                                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
            If r = DialogResult.No Then
                e.Cancel = True
                Return
            End If
        End If
        MyBase.OnFormClosing(e)
    End Sub
End Class