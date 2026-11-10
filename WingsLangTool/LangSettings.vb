Imports System.Collections.Generic
Imports System.IO
Imports System.Text

' Configuration persistée de l'outil (fichier settings.ini dans %APPDATA%\WingsLangTool).
Public Module LangSettings
    Public BackupEnabled As Boolean = True
    Public BackupCount As Integer = 5
    Public BackupFolder As String = ""
    Public Language As String = "en"

    Private _backedUp As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private _backupLock As New Object

    Private Function SettingsPath() As String
        Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WingsLangTool", "settings.ini")
    End Function

    Public Sub Load()
        Try
            Dim p = SettingsPath()
            If Not File.Exists(p) Then Return
            For Each line As String In File.ReadAllLines(p)
                Dim i = line.IndexOf("="c)
                If i <= 0 Then Continue For
                Dim k = line.Substring(0, i).Trim()
                Dim v = line.Substring(i + 1).Trim()
                Select Case k
                    Case "BackupEnabled" : Boolean.TryParse(v, BackupEnabled)
                    Case "BackupCount"
                        Dim n As Integer
                        If Integer.TryParse(v, n) AndAlso n >= 1 Then BackupCount = n
                    Case "BackupFolder" : BackupFolder = v
                    Case "Language" : Language = v
                End Select
            Next
        Catch
            ' configuration illisible : on garde les valeurs par défaut
        End Try
    End Sub

    Public Sub Save()
        Try
            Dim p = SettingsPath()
            Dim dir = Path.GetDirectoryName(p)
            If Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)
            Dim sb As New StringBuilder
            sb.AppendLine("BackupEnabled=" & BackupEnabled)
            sb.AppendLine("BackupCount=" & BackupCount)
            sb.AppendLine("BackupFolder=" & BackupFolder)
            sb.AppendLine("Language=" & Language)
            File.WriteAllText(p, sb.ToString(), New UTF8Encoding(False))
        Catch
            ' pas critique
        End Try
    End Sub

    ' Crée une sauvegarde horodatée du fichier avant sa première modification de la session.
    ' Retourne le chemin du backup créé (chaîne vide si rien à faire).
    Public Function EnsureBackup(filePath As String) As String
        If Not BackupEnabled Then Return ""
        If filePath = "" OrElse Not File.Exists(filePath) Then Return ""

        SyncLock _backupLock
            If _backedUp.Contains(filePath) Then Return ""
            _backedUp.Add(filePath)
        End SyncLock

        Dim backupPath As String = TryCreateBackup(filePath)
        If backupPath = "" Then
            ' échec (ex. dossier protégé) : on retente dans %APPDATA%
            backupPath = TryCreateBackupInAppData(filePath)
        End If
        If backupPath = "" Then Return ""

        PruneBackups(filePath)
        Return backupPath
    End Function

    Private Function BackupBaseName(filePath As String) As String
        Dim n = Path.GetFileNameWithoutExtension(filePath)
        Dim stamp = DateTime.Now.ToString("yyyyMMdd-HHmmssfff")
        Return n & "_" & stamp & Path.GetExtension(filePath)
    End Function

    Private Function TryCreateBackup(filePath As String) As String
        Try
            Dim folder As String = BackupFolder
            If folder = "" Then
                folder = Path.Combine(Path.GetDirectoryName(filePath), "backups_WingsLangTool")
            End If
            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)
            Dim dest = Path.Combine(folder, BackupBaseName(filePath))
            File.Copy(filePath, dest, True)
            Return dest
        Catch
            Return ""
        End Try
    End Function

    Private Function TryCreateBackupInAppData(filePath As String) As String
        Try
            Dim folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WingsLangTool", "backups")
            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)
            Dim dest = Path.Combine(folder, BackupBaseName(filePath))
            File.Copy(filePath, dest, True)
            Return dest
        Catch
            Return ""
        End Try
    End Function

    ' Ne conserve que les BackupCount sauvegardes les plus récentes du fichier concerné.
    Private Sub PruneBackups(filePath As String)
        Dim folder As String = ""
        Try
            If BackupFolder <> "" Then folder = BackupFolder
        Catch
        End Try
        Dim dirs As New List(Of String)
        Try
            Dim near = Path.Combine(Path.GetDirectoryName(filePath), "backups_WingsLangTool")
            If Directory.Exists(near) Then dirs.Add(near)
        Catch
        End Try
        Try
            If folder <> "" AndAlso Directory.Exists(folder) Then dirs.Add(folder)
        Catch
        End Try
        Try
            Dim app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WingsLangTool", "backups")
            If Directory.Exists(app) Then dirs.Add(app)
        Catch
        End Try

        Dim base = Path.GetFileNameWithoutExtension(filePath)
        Dim ext = Path.GetExtension(filePath)
        For Each d As String In dirs.Distinct().ToList()
            Try
                Dim files As New List(Of FileInfo)
                files = New DirectoryInfo(d).EnumerateFiles(base & "_*" & ext).ToList()
                If files.Count > BackupCount Then
                    files.Sort(Function(a, b) b.LastWriteTime.CompareTo(a.LastWriteTime))
                    For i As Integer = BackupCount To files.Count - 1
                        Try : files(i).Delete() : Catch
                        End Try
                    Next
                End If
            Catch
            End Try
        Next
    End Sub
End Module