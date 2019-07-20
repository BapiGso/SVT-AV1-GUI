﻿Imports System.Threading

Public Class Form1
    Private Exiting As Boolean = False
    Private GUILoaded As Boolean = False
    Private Sub InputBrowseBtn_Click(sender As Object, e As EventArgs) Handles InputBrowseBtn.Click
        Dim InputBrowser As New OpenFileDialog With {
            .Title = "Browse for a video file",
            .FileName = "",
            .Filter = "All Files|*.*"
        }
        Dim OkAction As MsgBoxResult = InputBrowser.ShowDialog
        If OkAction = MsgBoxResult.Ok Then
            InputTxt.Text = InputBrowser.FileName
        End If
    End Sub

    Private Sub OutputBrowseBtn_Click(sender As Object, e As EventArgs) Handles OutputBrowseBtn.Click
        Dim OutputBrowser As New SaveFileDialog With {
            .Title = "Save Video File",
            .FileName = "",
            .Filter = "WebM|*.webm|Matroska|*.mkv"
        }
        Dim OkAction As MsgBoxResult = OutputBrowser.ShowDialog
        If OkAction = MsgBoxResult.Ok Then
            OutputTxt.Text = OutputBrowser.FileName
        End If
    End Sub
    Private Sub CheckForLockFile()
        If Not String.IsNullOrWhiteSpace(tempLocationPath.Text) Then
            Dim videoFound As Boolean = False
            Dim CheckTempFolder As String() = IO.Directory.GetFiles(tempLocationPath.Text)
            If CheckTempFolder.Count > 0 Then
                If CheckTempFolder.Contains(tempLocationPath.Text + "\lock") And CheckTempFolder.Contains(tempLocationPath.Text + "\y4m-video.y4m") Then
                    videoFound = True
                End If
            End If
            If videoFound Then
                Dim result As DialogResult = MsgBox("The temporary folder contains temporary files from a previous session. Do you want to continue the previous encoding session?", MsgBoxStyle.YesNo)
                If result = DialogResult.Yes Then
                    OutputTxt.Text = My.Computer.FileSystem.ReadAllText(tempLocationPath.Text + "\lock").TrimEnd
                    ResumePreviousEncodeSession()
                Else
                    Dim result2 As DialogResult = MsgBox("Do you want to clean the folder?", MsgBoxStyle.YesNo)
                    If result2 = DialogResult.Yes Then
                        For Each ItemToDelete In CheckTempFolder
                            If ItemToDelete.Contains(".ivf") Or ItemToDelete.Contains(".txt") Or ItemToDelete.Contains(".y4m") Or ItemToDelete.Contains(".opus") Then My.Computer.FileSystem.DeleteFile(ItemToDelete)
                        Next
                    End If
                End If
            End If
        End If
    End Sub
    Private Sub DisableElements()
        StartBtn.Enabled = False
        InputTxt.Enabled = False
        OutputTxt.Enabled = False
        InputBrowseBtn.Enabled = False
        OutputBrowseBtn.Enabled = False
        audioBitrate.Enabled = False
        quantizer.Enabled = False
        rows.Enabled = False
        columns.Enabled = False
        HME.Enabled = False
        HME0.Enabled = False
        HME1.Enabled = False
        HME2.Enabled = False
        AdditionalArguments.Enabled = False
        speed.Enabled = False
        tempLocationPath.Enabled = False
        BrowseTempLocation.Enabled = False
        SaveLogBtn.Enabled = False
        PauseResumeButton.Enabled = True
    End Sub
    Private Sub ResumePreviousEncodeSession()
        DisableElements()
        Dim StartTasks As New Thread(Sub() Part2())
        StartTasks.Start()
    End Sub
    Private Sub StartBtn_Click(sender As Object, e As EventArgs) Handles StartBtn.Click
        If String.IsNullOrWhiteSpace(InputTxt.Text) Then
            MsgBox("No input file has been specified. Please enter or browse for an input video file")
        ElseIf String.IsNullOrWhiteSpace(OutputTxt.Text) Then
            MsgBox("No output file has been specified. Please enter or browse for an output video file")
        ElseIf String.IsNullOrWhiteSpace(tempLocationPath.Text) Then
            MsgBox("Temporary folder has not been specified. Please enter or browse for a temporary path")
        Else
            Dim CheckTempFolder As String() = IO.Directory.GetFiles(tempLocationPath.Text)
            If CheckTempFolder.Count > 0 Then
                For Each item In CheckTempFolder
                    If item.Contains(".ivf") Or item.Contains(".y4m") Or item.Contains(".opus") Then
                        Dim result As DialogResult = MsgBox("The temporary folder contains temporary files. It is recommended that the folder is cleaned up for best results. Otherwise, you could get an incorrect AV1 file. Do you want to clean the folder?", MsgBoxStyle.YesNo)
                        If result = DialogResult.Yes Then
                            For Each ItemToDelete In CheckTempFolder
                                If ItemToDelete.Contains(".ivf") Or ItemToDelete.Contains(".y4m") Or ItemToDelete.Contains(".opus") Then My.Computer.FileSystem.DeleteFile(ItemToDelete)
                            Next
                        End If
                        Exit For
                    End If
                Next
            End If
            DisableElements()
            If Not IO.Path.GetExtension(OutputTxt.Text) = ".webm" And Not IO.Path.GetExtension(OutputTxt.Text) = ".mkv" Then
                OutputTxt.Text = My.Computer.FileSystem.GetParentPath(OutputTxt.Text) + "\" + IO.Path.GetFileNameWithoutExtension(OutputTxt.Text) + ".webm"
            End If
            My.Computer.FileSystem.WriteAllText(tempLocationPath.Text + "\lock", OutputTxt.Text, False)
            Dim StartTasks As New Thread(Sub() Part1())
            StartTasks.Start()
        End If
    End Sub

    Private Sub Part1()
        Dim PieceSeconds As Long = 0
        If split_video_file(InputTxt.Text, tempLocationPath.Text) Then
            If extract_audio(InputTxt.Text, My.Settings.AudioBitrate, tempLocationPath.Text) Then
                Part2()
            End If
        End If
    End Sub

    Private Sub Part2()
        Run_svtav1(tempLocationPath.Text + "\y4m-video.y4m", tempLocationPath.Text + "\ivf-video.ivf")
        merge_audio_video(OutputTxt.Text, tempLocationPath.Text)
        If RemoveTempFiles.Checked Then clean_temp_folder(tempLocationPath.Text) Else IO.File.Delete(tempLocationPath.Text + "\lock")
        StartBtn.BeginInvoke(Sub()
                                 StartBtn.Enabled = True
                                 audioBitrate.Enabled = True
                                 speed.Enabled = True
                                 quantizer.Enabled = True
                                 rows.Enabled = True
                                 columns.Enabled = True
                                 HME.Enabled = True
                                 HME0.Enabled = True
                                 HME1.Enabled = True
                                 HME2.Enabled = True
                                 AdditionalArguments.Enabled = True
                                 tempLocationPath.Enabled = True
                                 BrowseTempLocation.Enabled = True
                                 OutputTxt.Enabled = True
                                 InputTxt.Enabled = True
                                 InputBrowseBtn.Enabled = True
                                 OutputBrowseBtn.Enabled = True
                                 SaveLogBtn.Enabled = True
                                 PauseResumeButton.Enabled = False
                             End Sub)
        MsgBox("Finished")
    End Sub
    Private Function Run_svtav1(Input_File As String, Output_File As String)
        UpdateLog("Encoding Video")
        Using svtav1Process As New Process()
            svtav1Process.StartInfo.FileName = "SvtAv1EncApp.exe"
            Dim VideoBitrateString As String = "-enc-mode " + My.Settings.speed.ToString() + " -q " + My.Settings.quantizer.ToString() + " -tile-rows " + My.Settings.TilingRows.ToString() + " -tile-columns " + My.Settings.TilingColumns.ToString()
            If My.Settings.HME Then VideoBitrateString += " -hme 1 " Else VideoBitrateString += " -hme 0 "
            If My.Settings.HME0 Then VideoBitrateString += " -hme-l0 1 " Else VideoBitrateString += " -hme-l0 0 "
            If My.Settings.HME1 Then VideoBitrateString += " -hme-l1 1 " Else VideoBitrateString += " -hme-l1 0 "
            If My.Settings.HME2 Then VideoBitrateString += " -hme-l2 1 " Else VideoBitrateString += " -hme-l2 0 "
            svtav1Process.StartInfo.Arguments = VideoBitrateString + " " + My.Settings.AdditionalArguments + " -i """ + Input_File + """ -b """ + Output_File + """"
            svtav1Process.StartInfo.CreateNoWindow = True
            svtav1Process.StartInfo.RedirectStandardOutput = True
            svtav1Process.StartInfo.RedirectStandardError = True
            svtav1Process.StartInfo.RedirectStandardInput = True
            svtav1Process.StartInfo.UseShellExecute = False
            AddHandler svtav1Process.OutputDataReceived, New DataReceivedEventHandler(Sub(sender, e)
                                                                                          If Not e.Data = Nothing Then
                                                                                              UpdateLog(e.Data, IO.Path.GetFileName(Input_File))
                                                                                          End If
                                                                                      End Sub)
            AddHandler svtav1Process.ErrorDataReceived, New DataReceivedEventHandler(Sub(sender, e)
                                                                                         If Not e.Data = Nothing Then
                                                                                             UpdateLog(e.Data, IO.Path.GetFileName(Input_File))
                                                                                         End If
                                                                                     End Sub)
            svtav1Process.Start()
            svtav1Process.BeginOutputReadLine()
            svtav1Process.BeginErrorReadLine()
            svtav1Process.WaitForExit()
            UpdateLog("Video encoding complete.")
            If Not Exiting Then
                IO.File.Delete(Input_File)
            End If
        End Using
        Return True
    End Function
    Private Function split_video_file(input As String, tempFolder As String)
        Dim ffmpegProcessInfo As New ProcessStartInfo
        Dim ffmpegProcess As Process
        ffmpegProcessInfo.FileName = "ffmpeg.exe"
        UpdateLog("Encoding input video to Y4M")
        ffmpegProcessInfo.Arguments = "-i """ + input + """ """ + tempFolder + "/y4m-video.y4m"" -y"
        ffmpegProcessInfo.CreateNoWindow = True
        ffmpegProcessInfo.RedirectStandardOutput = False
        ffmpegProcessInfo.UseShellExecute = False
        ffmpegProcess = Process.Start(ffmpegProcessInfo)
        ffmpegProcess.WaitForExit()
        UpdateLog("Video file splitted")
        Return True
    End Function
    Private Function clean_temp_folder(tempFolder As String)
        For Each File As String In IO.Directory.GetFiles(tempFolder)
            If IO.Path.GetExtension(File) = ".y4m" Or IO.Path.GetExtension(File) = ".ivf" Or IO.Path.GetFileName(File) = "opus-audio.opus" Then
                My.Computer.FileSystem.DeleteFile(File)
            End If
        Next
        My.Computer.FileSystem.DeleteFile(tempFolder + "\lock")
        Return True
    End Function
    Private Function merge_audio_video(output As String, tempFolder As String)
        UpdateLog("Merging audio and video files")
        Dim ffmpegProcessInfo As New ProcessStartInfo
        Dim ffmpegProcess As Process
        ffmpegProcessInfo.FileName = "ffmpeg.exe"
        ffmpegProcessInfo.Arguments = "-i """ + tempFolder + "\ivf-video.ivf"" -i """ + tempFolder + "\opus-audio.opus"" -c:v copy -c:a copy """ + output + """ -y"
        ffmpegProcessInfo.CreateNoWindow = True
        ffmpegProcessInfo.RedirectStandardOutput = False
        ffmpegProcessInfo.UseShellExecute = False
        ffmpegProcess = Process.Start(ffmpegProcessInfo)
        ffmpegProcess.WaitForExit()
        UpdateLog("Merge complete")
        Return True
    End Function

    Private Function extract_audio(input As String, bitrate As Integer, tempFolder As String)
        UpdateLog("Extracting and encoding audio")
        Dim ffmpegProcessInfo As New ProcessStartInfo
        Dim ffmpegProcess As Process
        ffmpegProcessInfo.FileName = "ffmpeg.exe"
        ffmpegProcessInfo.Arguments = "-i """ + input + """ -c:a libopus -application audio -b:a " + bitrate.ToString() + "K -af aformat=channel_layouts=""7.1|5.1|stereo"" """ + tempFolder + "\opus-audio.opus"" -y"
        ffmpegProcessInfo.CreateNoWindow = True
        ffmpegProcessInfo.RedirectStandardOutput = False
        ffmpegProcessInfo.UseShellExecute = False
        ffmpegProcess = Process.Start(ffmpegProcessInfo)
        ffmpegProcess.WaitForExit()
        UpdateLog("Audio extracted and encoded")
        Return True
    End Function
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim ignoreLocations As Boolean = False
        Dim vars As String() = Environment.GetCommandLineArgs
        If vars.Count > 1 Then
            If vars.Contains("ignore_locations") Then ignoreLocations = True
            For var As Integer = 1 To vars.Count - 1
                If Not vars(var) = "ignore_locations" Then InputTxt.Text = vars(var)
            Next
        End If
        IO.Directory.SetCurrentDirectory(IO.Path.GetDirectoryName(Process.GetCurrentProcess.MainModule.FileName))
        quantizer.Value = My.Settings.quantizer
        speed.Value = My.Settings.speed
        rows.Value = My.Settings.TilingRows
        columns.Value = My.Settings.TilingColumns
        HME.Checked = My.Settings.HME
        HME0.Checked = My.Settings.HME0
        HME1.Checked = My.Settings.HME1
        HME2.Checked = My.Settings.HME2
        AdditionalArguments.Text = My.Settings.AdditionalArguments
        audioBitrate.Value = My.Settings.AudioBitrate
        tempLocationPath.Text = My.Settings.tempFolder
        RemoveTempFiles.Checked = My.Settings.removeTempFiles
        GetFfmpegVersion()
        GUILoaded = True
        If Not String.IsNullOrWhiteSpace(tempLocationPath.Text) Then CheckForLockFile()
    End Sub

    Private Delegate Sub UpdateLogInvoker(message As String, PartName As String)
    Private Sub UpdateLog(message As String, Optional PartName As String = "")
        If ProgressLog.InvokeRequired Then
            ProgressLog.Invoke(New UpdateLogInvoker(AddressOf UpdateLog), message, PartName)
        Else
            If Not PartName = "" Then
                ProgressLog.AppendText(Date.Now().ToString() + " || " + PartName + " || " + message + vbCrLf)
            Else
                ProgressLog.AppendText(Date.Now().ToString() + " || " + message + vbCrLf)
            End If
            ProgressLog.SelectionStart = ProgressLog.Text.Length - 1
            ProgressLog.ScrollToCaret()
        End If
    End Sub
    Private Sub GetFfmpegVersion()
        Try
            Dim ffmpegProcessInfo As New ProcessStartInfo
            Dim ffmpegProcess As Process
            ffmpegProcessInfo.FileName = "ffmpeg.exe"
            ffmpegProcessInfo.CreateNoWindow = True
            ffmpegProcessInfo.RedirectStandardError = True
            ffmpegProcessInfo.UseShellExecute = False
            ffmpegProcess = Process.Start(ffmpegProcessInfo)
            ffmpegProcess.WaitForExit()
            ffmpegVersionLabel.Text = ffmpegProcess.StandardError.ReadLine()
        Catch ex As Exception
            MessageBox.Show("ffmpeg.exe was not found. Exiting...")
            Process.Start("https://moisescardona.me/downloading-ffmpeg-svt-av1-gui/")
            Me.Close()
        End Try
    End Sub

    Private Sub tempLocationPath_TextChanged(sender As Object, e As EventArgs) Handles tempLocationPath.TextChanged
        If GUILoaded Then
            My.Settings.tempFolder = tempLocationPath.Text
            My.Settings.Save()
        End If
    End Sub

    Private Sub quantizer_ValueChanged(sender As Object, e As EventArgs) Handles quantizer.ValueChanged
        If GUILoaded Then
            My.Settings.quantizer = quantizer.Value
            My.Settings.Save()
        End If
    End Sub

    Private Sub speed_ValueChanged(sender As Object, e As EventArgs) Handles speed.ValueChanged
        If GUILoaded Then
            My.Settings.speed = speed.Value
            My.Settings.Save()
        End If
    End Sub

    Private Sub audioBitrate_ValueChanged(sender As Object, e As EventArgs) Handles audioBitrate.ValueChanged
        If GUILoaded Then
            My.Settings.AudioBitrate = audioBitrate.Value
            My.Settings.Save()
        End If
    End Sub

    Private Sub BrowseTempLocation_Click(sender As Object, e As EventArgs) Handles BrowseTempLocation.Click
        Dim TempFolderBrowser As New FolderBrowserDialog With {
           .ShowNewFolderButton = False
       }
        Dim OkAction As MsgBoxResult = TempFolderBrowser.ShowDialog
        If OkAction = MsgBoxResult.Ok Then
            tempLocationPath.Text = TempFolderBrowser.SelectedPath
        End If
    End Sub


    Private Sub RemoveTempFiles_CheckedChanged(sender As Object, e As EventArgs) Handles RemoveTempFiles.CheckedChanged
        If GUILoaded Then
            My.Settings.removeTempFiles = RemoveTempFiles.Checked
            My.Settings.Save()
        End If
    End Sub


    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        Exiting = True
        While True
            Try
                For Each SvtAV1EncApp_proc In Process.GetProcessesByName("SvtAV1EncApp")
                    SvtAV1EncApp_proc.Kill()
                Next
            Catch
            End Try
            Dim Processes As Array = Process.GetProcessesByName("SvtAV1EncApp")
            If Processes.Length = 0 Then
                Exit While
            End If
        End While
        For Each svtav1_gui_proc In Process.GetProcessesByName("svtav1_gui")
            If svtav1_gui_proc.Id = Process.GetCurrentProcess().Id Then svtav1_gui_proc.Kill()
        Next
    End Sub


    Private Sub ClearLogBtn_Click(sender As Object, e As EventArgs) Handles ClearLogBtn.Click
        ProgressLog.Clear()
    End Sub

    Private Sub SaveLogBtn_Click(sender As Object, e As EventArgs) Handles SaveLogBtn.Click
        Dim saveDialog As New SaveFileDialog With {
            .Filter = "Log File|*.log",
            .Title = "Browse to save the log file"}
        Dim dialogResult As DialogResult = saveDialog.ShowDialog()
        If DialogResult.OK Then
            My.Computer.FileSystem.WriteAllText(saveDialog.FileName, ProgressLog.Text, False)
        End If
    End Sub

    Private Sub Form1_DragEnter(sender As Object, e As DragEventArgs) Handles MyBase.DragEnter
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            e.Effect = DragDropEffects.Copy
        End If
    End Sub
    Private Sub Form1_DragDrop(sender As Object, e As DragEventArgs) Handles MyBase.DragDrop
        InputTxt.Text = CType(e.Data.GetData(DataFormats.FileDrop), String())(0)
    End Sub

    Private Sub PauseResumeButton_Click(sender As Object, e As EventArgs) Handles PauseResumeButton.Click
        If PauseResumeButton.Text = "Pause" Then
            UpdateLog("Pausing encode")
            Try
                For Each SvtAV1Enc_proc In Process.GetProcessesByName("SvtAV1EncApp")
                    SuspendResumeProcess.SuspendProcess(SvtAV1Enc_proc.Id)
                Next
            Catch
            End Try
            UpdateLog("Encode paused (Some progress may still be reported)")
            PauseResumeButton.Text = "Resume"
        Else
            UpdateLog("Resuming encode")
            Try
                For Each SvtAV1Enc_proc In Process.GetProcessesByName("SvtAV1EncApp")
                    SuspendResumeProcess.ResumeProcess(SvtAV1Enc_proc.Id)
                Next
            Catch
            End Try
            UpdateLog("Encode resumed")
            PauseResumeButton.Text = "Pause"
        End If
    End Sub

    Private Sub rows_ValueChanged(sender As Object, e As EventArgs) Handles rows.ValueChanged
        If GUILoaded Then
            My.Settings.TilingRows = rows.Value
            My.Settings.Save()
        End If
    End Sub

    Private Sub Columns_ValueChanged(sender As Object, e As EventArgs) Handles columns.ValueChanged
        If GUILoaded Then
            My.Settings.TilingColumns = columns.Value
            My.Settings.Save()
        End If
    End Sub

    Private Sub HME_CheckedChanged(sender As Object, e As EventArgs) Handles HME.CheckedChanged
        If GUILoaded Then
            My.Settings.HME = HME.Checked
            My.Settings.Save()
        End If
    End Sub

    Private Sub HME0_CheckedChanged(sender As Object, e As EventArgs) Handles HME0.CheckedChanged
        If GUILoaded Then
            My.Settings.HME0 = HME0.Checked
            My.Settings.Save()
        End If
    End Sub

    Private Sub HME1_CheckedChanged(sender As Object, e As EventArgs) Handles HME1.CheckedChanged
        If GUILoaded Then
            My.Settings.HME1 = HME1.Checked
            My.Settings.Save()
        End If
    End Sub

    Private Sub HME2_CheckedChanged(sender As Object, e As EventArgs) Handles HME2.CheckedChanged
        If GUILoaded Then
            My.Settings.HME2 = HME2.Checked
            My.Settings.Save()
        End If
    End Sub

    Private Sub AdditionalArguments_TextChanged(sender As Object, e As EventArgs) Handles AdditionalArguments.TextChanged
        If GUILoaded Then
            My.Settings.AdditionalArguments = AdditionalArguments.Text
            My.Settings.Save()
        End If
    End Sub
End Class
