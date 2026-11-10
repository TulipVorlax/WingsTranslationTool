Imports System.Collections.Generic
Imports System.IO
Imports System.Windows.Forms

' Localisation légère : les clefs SONT le texte anglais (langue par défaut).
' Une langue supplémentaire = un fichier "lang\xx.txt" (clé=valeur, UTF-8) placé à côté de
' l'exécutable : chargé au démarrage, il surcharge l'anglais. Aucune recompilation nécessaire.
Public Module Localization
    Public CurrentLang As String = "en"

    Private _overrides As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    Private Function LangDir() As String
        Try
            Return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "lang")
        Catch
            Return "lang"
        End Try
    End Function

    ' Codes de langue disponibles : toujours "en" + les fichiers présents dans le dossier lang.
    Public Function AvailableLangs() As List(Of String)
        Dim res As New List(Of String)
        res.Add("en")
        Try
            For Each f As String In Directory.GetFiles(LangDir(), "*.txt")
                Dim code = Path.GetFileNameWithoutExtension(f).ToLowerInvariant()
                If code <> "en" AndAlso Not res.Contains(code) Then res.Add(code)
            Next
        Catch
        End Try
        Return res
    End Function

    ' Nom natif affiché d'une langue (dans la fenêtre Paramètres).
    Public Function LanguageName(code As String) As String
        Select Case code.ToLowerInvariant()
            Case "en" : Return "English"
            Case "fr" : Return "Français"
            Case "de" : Return "Deutsch"
            Case "es" : Return "Español"
            Case "it" : Return "Italiano"
            Case "pt" : Return "Português"
            Case "nl" : Return "Nederlands"
            Case "pl" : Return "Polski"
            Case "ru" : Return "Русский"
            Case "ja" : Return "日本語"
            Case "ko" : Return "한국어"
            Case "zh" : Return "中文"
            Case Else : Return code
        End Select
    End Function

    Public Sub Load(lang As String)
        Dim code = lang.ToLowerInvariant()
        _overrides.Clear()
        CurrentLang = code
        If code = "en" Then Return
        Try
            Dim langPath As String = Path.Combine(LangDir(), code & ".txt")
            If Not File.Exists(langPath) Then Return
            For Each line As String In File.ReadAllLines(langPath)
                Dim i = line.IndexOf("="c)
                If i <= 0 Then Continue For
                _overrides(line.Substring(0, i).Trim()) = line.Substring(i + 1).Trim()
            Next
        Catch
            ' fichier de langue illisible : on reste en anglais
        End Try
    End Sub

    ' Traduction directe (retourne la clef = texte anglais si aucune traduction disponible).
    Public Function T(key As String) As String
        Dim v As String = Nothing
        If _overrides.TryGetValue(key, v) Then Return v
        Return key
    End Function

    ' Traduction avec formatage ; se replie sur la clef en cas de template invalide.
    Public Function Fmt(key As String, ParamArray args() As Object) As String
        Try
            Return String.Format(T(key), args)
        Catch
            Return T(key)
        End Try
    End Function
End Module