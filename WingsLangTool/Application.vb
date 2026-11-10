Imports System.Windows.Forms

Public Module ApplicationMain
    <STAThread()>
    Public Sub Main()
        Try
            LangSettings.Load()
            Localization.Load(LangSettings.Language)
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            Application.Run(New frmMain())
        Catch ex As Exception
            MessageBox.Show(Localization.T("Error at startup:") & vbCrLf & vbCrLf & ex.ToString(), Localization.T("WingsLangTool"), MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
End Module