Imports System.IO
Imports System.Data
Imports Microsoft.Win32
Imports Infragistics.Windows.DataPresenter
Imports Infragistics.Windows.DataPresenter.Events
Imports System.ComponentModel
Imports System.Data.SqlClient
Imports System.Security.Principal
Imports Infragistics.Documents.Excel
'This is the latest version
Class MainWindow
    Dim CommentStarted As Boolean = False
    Dim Linenum As Integer
    ReadOnly DTCfg As New DataTable("Cfg")
    ReadOnly DVdups As New DataView(DTCfg)
    ReadOnly DTDups As New DataTable("Dups")
    ReadOnly DTErrors As New DataTable("Errors")
    ReadOnly DTRaw As New DataTable("Raw")
    ReadOnly DTDosing As New DataTable("Dosing")
    ReadOnly DTCompare As New DataTable("Compare")
    ReadOnly DVChanged As New DataView(DTCfg)

    Dim dtEdits As New DataTable("Edits")

    Dim WithEvents LoadFile_bgw As New BackgroundWorker
    Dim CFGFileName As String
    Dim fs As FileStream
    Dim FileAction As String

    Dim GridList() As XamDataGrid
    Dim TabList() As TabItem
    ReadOnly DTCFGFileList As New DataTable
    ReadOnly MissingRecordsList As New List(Of String)

    Dim Scan_Duplicates, Scan_Steppers As Boolean

    Dim DataWorkbook As Workbook

    'Dim DataLoaded As Boolean = False

    'TODO:
    'Add a boolean column for in_use for all tables
    'anything that the name end is a node is not in use
    'Add a culumn for Node for all tables - should be NXXX%-%%
    'Handle IO_Link status as a 2nd inport for the analog node - node should include _STATUS for IOLink
    'For DI, adda boolean column for safety - SDI is safety, DI is not in the address
    'No scaling column for digital i/o
    'Add a column for control box = Outputs.control box_ or Inputs.Control box_
    'Add a tab for combined table
    'Add invert
    'Add IO type - 
    'Relay code may need special handling

    '11/8 TODO
    'Remove Safety, SDI is descriptive enough

    Private Sub MainWindow_Initialized(sender As Object, e As EventArgs) Handles Me.Initialized
        DTCfg.Columns.Add("Line", GetType(Integer))
        DTCfg.Columns.Add("IO", GetType(String))
        DTCfg.Columns.Add("Name", GetType(String))
        DTCfg.Columns.Add("Address", GetType(String))
        DTCfg.Columns.Add("Type", GetType(String))
        DTCfg.Columns.Add("Scaling", GetType(String))
        DTCfg.Columns.Add("Node", GetType(String))
        DTCfg.Columns.Add("In Use", GetType(Boolean))
        DTCfg.Columns.Add("Invert", GetType(String))
        DTCfg.Columns.Add("Bit", GetType(String))
        DTCfg.Columns.Add("Control Box", GetType(String))
        DTCfg.Columns.Add("IO Type", GetType(String))
        DTCfg.Columns.Add("Channels", GetType(Integer))
        DTCfg.Columns.Add("Edited", GetType(Boolean))

        DTCompare.Columns.Add("FileName", GetType(String))
        DTCompare.Columns.Add("Name", GetType(String))
        DTCompare.Columns.Add("Control Box", GetType(String))
        DTCompare.Columns.Add("Node", GetType(String))
        DTCompare.Columns.Add("Invert", GetType(String))
        DTCompare.Columns.Add("Bit", GetType(String))
        DTCompare.Columns.Add("Scaling", GetType(String))

        DTDups.Columns.Add("Field", GetType(String))
        DTDups.Columns.Add("Line", GetType(Integer))
        DTDups.Columns.Add("Name", GetType(String))
        DTDups.Columns.Add("Address", GetType(String))

        DTErrors.Columns.Add("Line", GetType(Integer))
        DTErrors.Columns.Add("Error Type", GetType(String))
        DTErrors.Columns.Add("Data", GetType(String))

        DTRaw.Columns.Add("Line", GetType(Integer))
        DTRaw.Columns.Add("WhiteSpace", GetType(String))
        DTRaw.Columns.Add("Data", GetType(String))

        DTDosing.Columns.Add("Node", GetType(String))
        DTDosing.Columns.Add("Output Name", GetType(String))
        DTDosing.Columns.Add("Input Name", GetType(String))
        DTDosing.Columns.Add("Acceleration", GetType(String))
        DTDosing.Columns.Add("Emergency Stop", GetType(Boolean))
        DTDosing.Columns.Add("Enable", GetType(Boolean))
        DTDosing.Columns.Add("Execute", GetType(Boolean))
        DTDosing.Columns.Add("Reset", GetType(Boolean))
        DTDosing.Columns.Add("Start Type", GetType(String))
        DTDosing.Columns.Add("Target Position", GetType(String))
        DTDosing.Columns.Add("Velocity", GetType(String))
        DTDosing.Columns.Add("Actual Position", GetType(String))
        DTDosing.Columns("Emergency Stop").DefaultValue = False
        DTDosing.Columns("Enable").DefaultValue = False
        DTDosing.Columns("Execute").DefaultValue = False
        DTDosing.Columns("Reset").DefaultValue = False
        DTDosing.PrimaryKey = {DTDosing.Columns("Node")}

        dtEdits.Columns.Add("Line", GetType(Integer))
        dtEdits.Columns.Add("Before/After", GetType(String))
        dtEdits.Columns.Add("Scaling", GetType(String))

        DTCFGFileList.Columns.Add("FileName", GetType(String))
        DTCFGFileList.DefaultView.Sort = "FileName ASC"

        DTCfg.CaseSensitive = True

        GridAll.DataSource = DTCfg.DefaultView
        GridDosing.DataSource = DTDosing.DefaultView
        GridErrors.DataSource = DTErrors.DefaultView
        GridRaw.DataSource = DTRaw.DefaultView
        GridDups.DataSource = DTDups.DefaultView
        DTCompare.DefaultView.Sort = "Name ASC, FileName ASC"
        GridCompare.DataSource = DTCompare.DefaultView

        DVChanged.RowStateFilter = DataViewRowState.ModifiedCurrent
        GridEdits.DataSource = DVChanged

        'Make sure directories exist for saving/restoring grid customizations
        If Not Directory.Exists("C:\BOM_Reader") Then
            Directory.CreateDirectory("C:\BOM_Reader")
        End If
        If Not Directory.Exists("C:\BOM_Reader\Settings") Then
            Directory.CreateDirectory("C:\BOM_Reader\Settings")
        End If

        GridList = {GridAI, GridAll, GridDosing, GridDups, GridErrors, GridRaw}
        TabList = {Tab_AI, Tab_All, Tab_Dosing, Tab_Dups, Tab_Errors, Tab_Raw}

        'For Each grid As XamDataGrid In GridList
        '    If File.Exists("C:\BOM_Reader\Settings\Grid" & grid.Name & "_Layouts.xml") Then
        '        Try
        '            fs = New FileStream("C:\BOM_Reader\Settings\Grid" & grid.Name & "_Layouts.xml", FileMode.Open, FileAccess.Read) : grid.LoadCustomizations(fs) : fs.Close()
        '        Catch ex As Exception
        '            File.Delete("C:\BOM_Reader\Settings\Grid" & grid.Name & "_Layouts.xml")
        '        End Try
        '    End If
        'Next

        Call Add_Login()
    End Sub

    Private Sub BtnOpenFile_Click(sender As Object, e As RoutedEventArgs) Handles BtnOpenFile.Click
        FileAction = "ReadFile"
        OpenCFGFile()
        DTCFGFileList.Rows.Clear()
        DTCFGFileList.Rows.Add({CFGFileName.Substring(CFGFileName.LastIndexOf("\") + 1)})
        Me.Title = "P500 Configuration Wizard" & "   " & CFGFileName.Substring(CFGFileName.LastIndexOf("\") + 1)
        BtnCompareFile.IsEnabled = True
    End Sub

    Private Sub BtnCompareFile_Click(sender As Object, e As RoutedEventArgs) Handles BtnCompareFile.Click
        FileAction = "CompareFiles"
        Me.Title = "P500 Configuration Wizard" & "    Comparing " & CFGFileName.Substring(CFGFileName.LastIndexOf("\") + 1)
        DTCompare.Rows.Clear()
        Dim fn As String = CFGFileName.Substring(CFGFileName.LastIndexOf("\") + 1)
        Dim dr As DataRow
        For Each dr In DTCfg.Rows
            DTCompare.Rows.Add({fn, dr("Name"), dr("Control Box"), dr("Node"), dr("Invert"), dr("Bit"), dr("Scaling")})
        Next
        Dim nr As Integer = DTCompare.Rows.Count
        OpenCFGFile()
        DTCFGFileList.Rows.Add({CFGFileName.Substring(CFGFileName.LastIndexOf("\") + 1)})
    End Sub

    Private Sub FinishCompare()
        Me.Title &= " and " & CFGFileName
        Dim fn As String = CFGFileName.Substring(CFGFileName.LastIndexOf("\") + 1)
        For Each dr In DTCfg.Rows
            DTCompare.Rows.Add({fn, dr("Name"), dr("Control Box"), dr("Node"), dr("Invert"), dr("Bit"), dr("Scaling")})
        Next

        'Make sure there are no missing lines
        'There should be a ConfigFile/Node combination
        MissingRecordsList.Clear()
        For Each drv As DataRowView In DTCompare.DefaultView
            For Each drf As DataRow In DTCFGFileList.Rows

            Next
        Next


        For Each Tab As TabItem In TabList
            Tab.IsEnabled = False
        Next

        Tab_Compare.IsEnabled = True
        TC.SelectedItem = Tab_Compare
    End Sub

    Private Sub OpenCFGFile()

        Dim ofd As New OpenFileDialog With {
                       .Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                       .FilterIndex = 1
                   }

        If ofd.ShowDialog = True Then
            My.Settings.CfgDir = ofd.FileName.Substring(0, ofd.FileName.LastIndexOf("\") + 1)
            My.Settings.Save()
            CFGFileName = ofd.FileName

            DTCfg.Rows.Clear()
            DTErrors.Rows.Clear()
            DTDups.Rows.Clear()
            DTRaw.Rows.Clear()
            DTDosing.Rows.Clear()
            LoadBusy.BusyContent = "Loading Data File"
            LoadBusy.IsBusy = True

            Scan_Duplicates = Chk_Duplicates.IsChecked
            Scan_Steppers = Chk_Stepper.IsChecked
            LoadFile_bgw.WorkerReportsProgress = True
            LoadFile_bgw.RunWorkerAsync()

            'Dim drx As DataRecord = GridOptions.ActiveRecord
            'drx.RefreshCellValues()
            For Each tab As TabItem In TabList
                tab.IsEnabled = False
            Next
        End If
    End Sub

    Private Sub LoadFile_bgw_DoWork(sender As Object, e As DoWorkEventArgs) Handles LoadFile_bgw.DoWork
        Load_Cfg_File()
    End Sub

    Private Sub LoadFile_bgw_RunWorkerCompleted(sender As Object, e As RunWorkerCompletedEventArgs) Handles LoadFile_bgw.RunWorkerCompleted
        LoadBusy.IsBusy = False

        If FileAction = "ReadFile" Then
            For Each Tab As TabItem In TabList
                Tab.IsEnabled = True
            Next
            If Scan_Duplicates = False Then Tab_Dups.IsEnabled = False
            If Scan_Steppers = False Then Tab_Dosing.IsEnabled = False
            TC.SelectedItem = Tab_All

        Else
            FinishCompare()
        End If
        BtnExport.IsEnabled = True

    End Sub

    Private Sub Load_Cfg_File()
        Linenum = 0
        Dim TR As New StreamReader(CFGFileName)
        Dim Line As String = TR.ReadLine : Linenum += 1 : Parse_Raw_Line(Linenum, Line)
        Line = Line.TrimStart
        'Skip down to start of Controller data
        Do Until Line.StartsWith("<Controller")
            Line = TR.ReadLine : Linenum += 1 : Parse_Raw_Line(Linenum, Line)
            Line = Line.TrimStart
        Loop
        Do Until TR.EndOfStream
            Line = TR.ReadLine : Linenum += 1 : Parse_Raw_Line(Linenum, Line)
            Line = Line.TrimStart.TrimEnd

            If Line.StartsWith("<Axis") Then
                Exit Do
            End If
            If CommentStarted Then
                CommentStarted = Not Line.EndsWith("-->")
            Else
                If Line.StartsWith("<!--") Then
                    CommentStarted = Not Line.EndsWith("-->")
                Else
                    If Line.Length > 0 Then
                        Process_Line(Line)
                    End If
                End If
            End If
        Loop

        Do Until TR.EndOfStream
            Line = TR.ReadLine : Linenum += 1 : Parse_Raw_Line(Linenum, Line)
        Loop

        TR.Close()
        If Scan_Duplicates = True Then
            Find_Duplicates()
        End If
        If Scan_Steppers = True Then
            Analyze_Dosing_Pumps()
        End If

        DTCfg.AcceptChanges()
    End Sub

    Private Sub Parse_Raw_Line(Linenum As Integer, line As String)
        Dim ws As String
        Dim l As String = line.TrimStart
        Dim p0 As Integer = line.Length - l.Length
        ws = line.Substring(0, p0)
        DTRaw.Rows.Add({Linenum, ws, l})
    End Sub

    Private Sub Analyze_Dosing_Pumps()
        Dim dvPA As New DataView(DTCfg) With {
            .RowFilter = "[IO Type] = 'Stepper'"
        }
        Dim drn As DataRow

        Dim Node, Fnc As String
        Dim p0 As Integer
        drn = DTDosing.NewRow
        drn("Node") = "Expected"
        drn("Output Name") = "Expected"
        drn("Acceleration") = "0=0,32767=32767"
        drn("Emergency Stop") = True
        drn("Enable") = True
        drn("Execute") = True
        drn("Reset") = True
        drn("Start Type") = "0=0,1=1"
        drn("Target Position") = "0=0,1=23809.96272"
        drn("Velocity") = "0=0,32767=32767"
        drn("Actual Position") = "0=0,1=23809.96272"
        DTDosing.Rows.Add(drn)
        On Error Resume Next
        For Each drv As DataRowView In dvPA
            drn = DTDosing.NewRow
            p0 = drv("Node").ToString.IndexOf("_")
            Node = drv("Node").ToString.Substring(0, p0)
            Fnc = drv("Node").ToString.Substring(p0 + 1)
            drn("Node") = Node
            DTDosing.Rows.Add(drn)
        Next
        On Error GoTo 0

        Dim InputName, OutputName As String
        For Each dr As DataRow In DTDosing.Rows
            If dr("Output Name").ToString <> "Expected" Then
                dvPA.RowFilter = "Node like '" & dr("Node").ToString & "*'"
                InputName = ""
                OutputName = ""
                For Each drv As DataRowView In dvPA
                    p0 = drv("Node").ToString.IndexOf("_")
                    Fnc = drv("Node").ToString.Substring(p0 + 1)
                    If drv("IO") = "Input" And InputName = "" Then
                        p0 = drv("Name").ToString.IndexOf("_", 3)
                        InputName = drv("Name").ToString.Substring(2, p0 - 2)
                    End If
                    If drv("IO") = "Output" And OutputName = "" Then
                        p0 = drv("Name").ToString.IndexOf("_", 3)
                        OutputName = drv("Name").ToString.Substring(2, p0 - 2)
                    End If
                    Select Case Fnc
                        Case = "ACCELERATION"
                            dr("Acceleration") = drv("Scaling")
                        Case = "EMERGENCY_STOP"
                            dr("Emergency Stop") = True
                        Case = "ENABLE"
                            dr("Enable") = True
                        Case = "EXECUTE"
                            dr("Execute") = True
                        Case = "RESET"
                            dr("Reset") = True
                        Case = "START_TYPE"
                            dr("Start Type") = drv("Scaling")
                        Case = "TARGET_POSITION"
                            dr("Target Position") = drv("Scaling")
                        Case = "VELOCITY"
                            dr("Velocity") = drv("Scaling")
                        Case = "ACTUAL_POSITION"
                            dr("Actual Position") = drv("Scaling")
                    End Select
                    dr("Input Name") = InputName
                    dr("Output Name") = OutputName
                Next
            End If
        Next
    End Sub

    Private Sub Process_Line(Line As String)
        Dim l_IO, l_Name, l_Address, l_Type, l_Scaling, l_ControlBox, l_Invert, l_Bit As String
        Dim Node As String = ""

        Dim InUse As Boolean

        If Line.StartsWith("<Input") Then
            l_IO = "Input"
        ElseIf Line.StartsWith("<Output") Then
            l_IO = "Output"
        Else
            DTErrors.Rows.Add({Linenum, "Input/Output", Line})
            Exit Sub
        End If

        Dim p0, p1 As Integer
        Dim iot() As Object
        Dim parts() As String
        Dim ns As Integer

        Dim ErrorType As String = ""

        Try
            ErrorType = "Name" : l_Name = GetField(Line, "Name=")
            ErrorType = "Address" : l_Address = GetField(Line, "Address=")
            ErrorType = "Type" : l_Type = GetField(Line, " Type=")
            ErrorType = "Scaling" : l_Scaling = GetField(Line, "Scaling=")
            ErrorType = "Invert" : l_Invert = GetField(Line, "Invert=")
            ErrorType = "Bit" : l_Bit = GetField(Line, "Bit=")
            If l_Address.Contains("-EX260") Then
                p0 = l_Address.IndexOf("-EX260")
                Node = l_Address.Substring(p0 + 7)
            Else
                parts = l_Address.Split("_")
                ns = 4
                ErrorType = "Node"
                If parts.Length > 3 Then
                    If parts(4).StartsWith("E") Then
                        ns = 5
                    End If
                    Node = parts(ns)
                    For i As Integer = ns + 1 To parts.Length - 1
                        Node &= "_" & parts(i)
                    Next
                End If
            End If
            InUse = Not l_Name.EndsWith(Node)
            ErrorType = "Control Box"
            p0 = l_Name.IndexOf("_")
            p1 = l_Name.IndexOf("_", p0 + 1)
            'Control box is between the second . and first _, except where PNEU nodes are concerned
            p0 = l_Address.IndexOf(".")
            p0 = l_Address.IndexOf(".", p0 + 1)
            If l_Address.ToUpper.Contains("_PNEU") Then
                p1 = l_Address.IndexOf("_PNEU")
            Else
                p1 = l_Address.IndexOf("_")
            End If
            l_ControlBox = l_Address.Substring(p0 + 1, p1 - p0 - 1)
            ErrorType = "IO Type" : iot = Get_IO_Type(l_Address)
            If iot(0) = "Pneu" Then
                l_Bit = Pneu_Bit(Node, l_Bit)
            End If
        Catch ex As Exception
            DTErrors.Rows.Add({Linenum, ErrorType, Line})
            Exit Sub
        End Try
        If Not l_Invert Is Nothing Then
            l_Invert = l_Invert.Trim
        End If
        If Not iot(0) Is Nothing Then
            iot(0) = iot(0).Trim
        End If

        DTCfg.Rows.Add({Linenum, l_IO, l_Name, l_Address, l_Type, l_Scaling, Node, InUse, l_Invert, l_Bit, l_ControlBox, iot(0), iot(1), False})
    End Sub

    Function Pneu_Bit(valve_string As String, bit As Integer) As String
        Do Until valve_string.StartsWith("V") = True
            valve_string = valve_string.Substring(1)
        Loop
        valve_string = valve_string.Substring(1)
        If valve_string.Substring(0, 1) = "0" Then
            valve_string = valve_string.Substring(1)
        End If

        valve_string = valve_string.Substring(0, valve_string.IndexOf("-"))
        Dim v As Integer = bit Mod 2
        If v = 0 Then
            Return bit & " (" & valve_string + (bit / 2) & "A)"
        Else
            Return bit & " (" & valve_string + ((bit - 1) / 2) & "B)"
        End If
        Return valve_string
    End Function

    Private Sub Find_Duplicates()
        DVdups.Sort = "Name ASC"
        For i = 1 To DVdups.Count - 1
            If DVdups(i - 1).Item("Name") = DVdups(i).Item("Name") Then
                DTDups.Rows.Add({"Name", DVdups(i - 1).Item("Line"), DVdups(i - 1).Item("Name"), DVdups(i - 1).Item("Address")})
                DTDups.Rows.Add({"Name", DVdups(i).Item("Line"), DVdups(i).Item("Name"), DVdups(i).Item("Address")})
            End If
        Next

        DVdups.Sort = "Address ASC"
        For i = 1 To DVdups.Count - 1
            If DVdups(i - 1).Item("Address") = DVdups(i).Item("Address") Then
                DTDups.Rows.Add({"Address", DVdups(i - 1).Item("Line"), DVdups(i - 1).Item("Name"), DVdups(i - 1).Item("Address")})
                DTDups.Rows.Add({"Address", DVdups(i).Item("Line"), DVdups(i).Item("Name"), DVdups(i).Item("Address")})
            End If
        Next
    End Sub

    Private Function GetField(Line As String, Field As String) As String
        Dim f As String
        Dim p1, p2 As Integer

        p1 = Line.ToLower.IndexOf(Field.ToLower)
        If p1 > 0 Then
            p1 += Field.Length + 1
            p2 = Line.IndexOf("""", p1)
            f = Line.Substring(p1, p2 - p1)
        Else
            f = Nothing
        End If
        Return f
    End Function

    Private Function Get_IO_Type(Address As String) As Object()
        Dim card As String
        Dim cIOType As String = ""
        Dim cChannels As Integer = 0
        'If Address.Contains("_EL") = True And Address.Contains("STATUS") = False Then
        If Address.Contains("_EL") = True Then
            card = Address.Substring(Address.IndexOf("_EL") + 1)
            card = card.Substring(0, card.IndexOf("_")).Trim

            Select Case card
                Case "EL1018"
                    cIOType = "DI"
                    cChannels = 8
                Case "EL1819"
                    cIOType = "DI"
                    cChannels = 16
                Case "EL1872"
                    cIOType = "DI"
                    cChannels = 16
                Case "EL1904"
                    cIOType = "SDI"
                    cChannels = 4
                Case "EL2004"
                    cIOType = "DO"
                    cChannels = 4
                Case "EL2008"
                    cIOType = "DO"
                    cChannels = 8
                Case "EL2624"
                    cIOType = "Relay"
                    cChannels = 4
                Case "EL2809"
                    cIOType = "DO"
                    cChannels = 16
                Case "EL2872"
                    cIOType = "DO"
                    cChannels = 16
                Case "EL3058"
                    cIOType = "AI 4-20mA"
                    cChannels = 8
                Case "EL3068"
                    cIOType = "AI 0-10V"
                    cChannels = 8
                Case "EL3202"
                    cIOType = "RTD"
                    cChannels = 2
                Case "EL3214"
                    cIOType = "RTD"
                    cChannels = 4
                Case "EL3692"
                    cIOType = "Resistance"
                    cChannels = 2
                Case "EL4008"
                    cIOType = "AO 0-10V"
                    cChannels = 8
                Case "EL6224"
                    cIOType = "IOLink"
                    cChannels = 4
                Case = "EL7031"
                    cIOType = "Stepper"
                    cChannels = 8
                Case "EL1512"
                    cIOType = "Counter"
                    cChannels = 2
                    'Case "EL7031"
                    '    skip_Line = True
                    'Case Else
                    '    If list_Undefined.Contains(card) = False Then
                    '        list_Undefined.Add(card)
                    '        MessageBox.Show(card & " will be skipped", "Undefined Card", MessageBoxButtons.OK)
                    '    End If
            End Select
        ElseIf Address.Contains("-EX260_") Then
            cIOType = "Pneu"
            cChannels = 8
        ElseIf Address.Contains("_EP1018_") Then
            cIOType = "DI"
            cChannels = 8
        End If
        Return New Object() {cIOType, cChannels}
    End Function

    Private Sub MainWindow_Closing(sender As Object, e As CancelEventArgs) Handles Me.Closing
        If DVChanged.Count > 0 Then
            Dim rtn As MsgBoxResult = MsgBox("Save Changes?", MsgBoxStyle.YesNoCancel, "Data have been edited")
            If rtn = MsgBoxResult.Cancel Then Exit Sub
            If rtn = MsgBoxResult.Yes Then
                Dim sw As New StreamWriter("D:\P500_Cfg\CFGChanges.xml", False)
                For Each dr As DataRow In DTRaw.Rows
                    sw.WriteLine(dr("WhiteSpace") & dr("Line"))
                Next
            End If
        End If

        For Each grid As XamDataGrid In GridList
            fs = New FileStream("C:\BOM_Reader\Settings\Grid" & grid.Name & "_Layouts.xml", FileMode.OpenOrCreate, FileAccess.Write) : grid.SaveCustomizations(fs) : fs.Close()
        Next
    End Sub

    Private Sub GridAI_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles GridAI.MouseDoubleClick, GridAll.MouseDoubleClick,
        GridDosing.MouseDoubleClick, GridDups.MouseDoubleClick
        Dim g As XamDataGrid = sender
        Dim c As Cell = g.ActiveCell
        Dim rn As Integer
        Dim fn As String = c.Field.Name
        If c.Field.Name = "Line" Then
            rn = c.Value
            e.Handled = True
            TC.SelectedItem = Tab_Raw
            GridRaw.ActiveRecord = GridRaw.Records(Get_Index_From_LineNum(rn, GridRaw))
        End If
    End Sub

    Private Function Get_Index_From_LineNum(line As Integer, Grid As XamDataGrid) As Integer
        Dim indx As Integer = 0
        For Each rec As DataRecord In Grid.Records
            If rec.Cells("Line").Value = line Then
                Return indx
            End If
            indx += 1
        Next
        Return -1
    End Function

#Region "Login"
    Async Sub Add_Login()
        Try
            Await Task.Run(AddressOf Report_Login)
        Catch ex As Exception

        End Try
    End Sub
    Sub Report_Login()
        'Dim Con_SQL As New SqlConnection With {
        '    .ConnectionString = "Server=tcp:p500.database.windows.net,1433;Database=P500;Uid=panellogreader@p500;Pwd=PanelReader1$;Encrypt=yes;TrustServerCertificate=no;Connection Timeout=30;"
        '}
        'Dim cmd_Add_Login As New SqlCommand With {
        '     .Connection = Con_SQL,
        '     .CommandText = "INSERT INTO [P500_Users] ([user_Name], [user_Login], [user_CV]) VALUES ('New User', @login, 1)"
        ' }
        'cmd_Add_Login.Parameters.AddWithValue("@login", Get_User_Identity)
        'Con_SQL.Open()
        'cmd_Add_Login.ExecuteNonQuery()
        'Con_SQL.Close()
    End Sub

    Function Get_User_Identity() As String
        Dim wi As WindowsIdentity = WindowsIdentity.GetCurrent
        Dim wp As New WindowsPrincipal(wi)
        Return wp.Identity.Name.ToString

    End Function

#End Region

#Region "Export"
    Private Sub BtnExport_Click(sender As Object, e As RoutedEventArgs) Handles BtnExport.Click
        Keyboard.ClearFocus()
        DataWorkbook = New Workbook()
        DataWorkbook.SetCurrentFormat(WorkbookFormat.Excel2007)
        Call Export_File(DTCfg, "Config Data")
        Me.SaveExport(DataWorkbook)
    End Sub

    Public Sub Export_File(dt As DataTable, Sheet_Name As String)

        Try
            Dim sheetOne As Worksheet = DataWorkbook.Worksheets.Add(Sheet_Name)
            Dim currentColumn As Integer = 0
            Dim ColCount As Integer = 0
            Dim DV As New DataView With {
                    .Table = dt
                }
            For Each column As DataColumn In dt.Columns
                Select Case column.ColumnName
                    Case "Set_Point"
                        Me.SetCellValue(sheetOne.Rows(0).Cells(currentColumn), "Set Point", "Text")
                        System.Math.Max(System.Threading.Interlocked.Increment(currentColumn), currentColumn - 1)
                        ColCount += 1
                    Case "Count"
                        Me.SetCellValue(sheetOne.Rows(0).Cells(currentColumn), "Count", "Text")
                        System.Math.Max(System.Threading.Interlocked.Increment(currentColumn), currentColumn - 1)
                        ColCount += 1
                    Case "Time"
                        Me.SetCellValue(sheetOne.Rows(0).Cells(currentColumn), "Time", "Text")
                        System.Math.Max(System.Threading.Interlocked.Increment(currentColumn), currentColumn - 1)
                        ColCount += 1
                    Case "FileName", "Output", "Holder_ID"

                    Case Else
                        Me.SetCellValue(sheetOne.Rows(0).Cells(currentColumn), column.ColumnName, "Text")
                        System.Math.Max(System.Threading.Interlocked.Increment(currentColumn), currentColumn - 1)
                        ColCount += 1
                End Select
            Next
            'Export Data From Grid
            Dim currentRow As Integer = 1
            Dim worksheetRow As WorksheetRow
            For Each DRV As DataRowView In DV
                worksheetRow = sheetOne.Rows(currentRow)
                Dim currentCell As Integer = -1
                For Each column As DataColumn In dt.Columns
                    Dim Type_String As String = Nothing
                    Select Case column.DataType
                        Case GetType(Integer)
                            Type_String = "Integer"
                        Case GetType(String)
                            Type_String = "Text"
                        Case GetType(Date)
                            Type_String = "Time"
                        Case GetType(Double)
                            Type_String = "Double"
                        Case Else
                            Type_String = "Blank"
                    End Select
                    If column.ColumnName.Contains("Zone") = True Then
                        SetCellValue(worksheetRow.Cells(Threading.Interlocked.Increment(currentCell)), DRV.Item(column.ColumnName), "Double")
                    Else
                        Select Case column.ColumnName
                            'Case "ID", "Attempt", "Cycle", "Measurement", "StepNum", "CarrierSlot"
                            '    Me.SetCellValue(worksheetRow.Cells(Threading.Interlocked.Increment(currentCell)), DRV.Item(column.ColumnName), "Integer")
                            'Case "Value", "From", "To", "Distance", "Move Time", "Neg Torque", "Pos Torque", "Error", "PanelBow", "PanelResistanceA", "BladderPressure", "Level", "Flow", "FacilityWaterPressure", "RinseWaterFlow", "Duration", "RunTime", "ReservoirTemperature"
                            '    Me.SetCellValue(worksheetRow.Cells(Threading.Interlocked.Increment(currentCell)), DRV.Item(column.ColumnName), "Double")
                            Case "Time"
                                Me.SetCellValue(worksheetRow.Cells(Threading.Interlocked.Increment(currentCell)), DRV.Item(column.ColumnName), "Time")
                            Case "FileName", "Output", "Holder_ID"
                            Case Else
                                Select Case column.DataType
                                    Case GetType(Double)
                                        Me.SetCellValue(worksheetRow.Cells(Threading.Interlocked.Increment(currentCell)), DRV.Item(column.ColumnName), "Double")
                                    Case GetType(Integer)
                                        Me.SetCellValue(worksheetRow.Cells(Threading.Interlocked.Increment(currentCell)), DRV.Item(column.ColumnName), "Integer")
                                    Case GetType(Date)
                                        Me.SetCellValue(worksheetRow.Cells(Threading.Interlocked.Increment(currentCell)), DRV.Item(column.ColumnName), "Time")
                                    Case Else
                                        Me.SetCellValue(worksheetRow.Cells(Threading.Interlocked.Increment(currentCell)), DRV.Item(column.ColumnName), "Text")
                                End Select

                        End Select
                    End If

                Next
                currentRow += 1
            Next
            DV.Dispose()

        Catch ex As Exception

        End Try

    End Sub
    Private Sub SetCellValue(ByVal cell As WorksheetCell, ByVal value As Object, ValueType As String)
        Try
            If cell.Value <> "" Then
                Select Case ValueType
                    Case "Text"
                        cell.Value = value.ToString
                    Case "Time"
                        cell.Value = value.ToString
                        Dim a As Date = cell.Value
                        Dim b As String = a.Year
                        If b = 1 Then
                            cell.Value = cell.Value.ToString.Substring(9)
                        Else
                            cell.CellFormat.FormatString = "MM/dd/yy HH:mm:ss"
                        End If

                    Case "Double"
                        If value.ToString.Length > 0 Then
                            cell.Value = CDbl(value)
                        End If

                    Case "Integer"
                        cell.Value = CInt(value)
                End Select

            End If

        Catch ex As Exception
            cell.Value = Nothing
        End Try

        cell.Value = value
        cell.CellFormat.ShrinkToFit = ExcelDefaultableBoolean.[True]
        cell.CellFormat.VerticalAlignment = VerticalCellAlignment.Center
        cell.CellFormat.Alignment = HorizontalCellAlignment.Center
    End Sub
    Private Sub SaveExport(ByVal dataWorkbook As Workbook)
        Dim dialog As SaveFileDialog
        Dim exportStream As Stream

        ' Export to xlsx excel file format

        '    Key .Filter = "Excel files|*.xlsx", _
        '    Key .DefaultExt = "xlsx" _
        '}
        dialog = New SaveFileDialog With {
            .Filter = "Excel files|*.xlsx",
            .DefaultExt = "xlsx"
        } 'With { _
        dialog.ShowDialog()

        Try
            exportStream = dialog.OpenFile()
            dataWorkbook.Save(exportStream)
            exportStream.Close()
            Process.Start(dialog.FileName)
        Catch ex As Exception
            System.Windows.MessageBox.Show(ex.Message)
        End Try


    End Sub
    Public Function DateNow() As String
        Dim TempDate As String = Now.Year.ToString.Substring(2)
        If Now.Month.ToString.Length = 1 Then
            TempDate &= "0" & Now.Month
        Else
            TempDate &= Now.Month
        End If
        If Now.Day.ToString.Length = 1 Then
            TempDate &= "0" & Now.Day
        Else
            TempDate &= Now.Day
        End If
        If Now.Hour.ToString.Length = 1 Then
            TempDate &= "0" & Now.Hour
        Else
            TempDate &= Now.Hour
        End If
        If Now.Minute.ToString.Length = 1 Then
            TempDate &= "0" & Now.Minute
        Else
            TempDate &= Now.Minute
        End If
        If Now.Second.ToString.Length = 1 Then
            TempDate &= "0" & Now.Second
        Else
            TempDate &= Now.Second
        End If
        Return TempDate
    End Function

    Private Sub GridAll_CellUpdated(sender As Object, e As CellUpdatedEventArgs) Handles GridAll.CellUpdated
        e.Cell.Record.Cells("Edited").Value = True
        Tab_Edits.IsEnabled = True
    End Sub

    'Private Sub Tab_Edits_GotFocus(sender As Object, e As RoutedEventArgs) Handles Tab_Edits.GotFocus
    '    Beep()
    'End Sub

    Private Sub TC_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles TC.SelectionChanged
        BtnSaveFile.IsEnabled = False
        'The edits tab has been selected so load the before and after data for each row which has been changed
        If TC.SelectedIndex = 7 Then
            dtEdits.Rows.Clear()
            DVChanged.RowStateFilter = DataViewRowState.ModifiedOriginal
            For Each drv As DataRowView In DVChanged
                dtEdits.Rows.Add(drv("Line"), "Before", drv("Scaling"))
            Next
            DVChanged.RowStateFilter = DataViewRowState.ModifiedCurrent
            For Each drv As DataRowView In DVChanged
                dtEdits.Rows.Add(drv("Line"), "After", drv("Scaling"))
            Next
            dtEdits.DefaultView.Sort = "Line ASC, [Before/After] Desc"
            GridEdits.DataSource = dtEdits.DefaultView
        End If
        If dtEdits.Rows.Count > 0 Then BtnSaveFile.IsEnabled = True
    End Sub

    Private Sub BtnSaveFile_Click(sender As Object, e As RoutedEventArgs) Handles BtnSaveFile.Click
        Dim ChangeLog As String = vbCrLf & vbCrLf & vbTab & "<!--Config File Changelog " & Get_User_Identity() & "   " & Now.ToString(" dd MMM yyyy  HH:mm:ss") & vbCrLf
        DTRaw.Rows.Add(-1, vbCrLf & vbCrLf & vbTab, "<!--Config File Changelog " & Get_User_Identity() & "   " & Now.ToString(" dd MMM yyyy  HH:mm:ss"))
        Dim L As Integer
        Dim B, A As String
        Dim rb, ra As String
        Dim nr As Integer = dtEdits.Rows.Count
        'Since changes are displayed in 2 rows, the first contains the pre edit data and the second the post edit data
        For i = 0 To nr - 1 Step 2
            L = dtEdits.DefaultView(i).Item("Line")
            B = dtEdits.DefaultView(i).Item("Scaling")
            A = dtEdits.DefaultView(i + 1).Item("Scaling")

            'Now make the changes to the raw data
            rb = DTRaw.Rows(L - 1).Item("Data")
            ra = rb.Replace(B, A)
            DTRaw.Rows(L - 1).Item("Data") = ra
            ChangeLog &= vbTab & vbTab & "Line: " & L & "  from: " & rb & vbCrLf
            ChangeLog &= vbTab & vbTab & "Line: " & L & "  to:   " & ra & vbCrLf & vbCrLf
            DTRaw.Rows.Add(-1, vbTab & vbTab, "Line: " & L & "  from: " & rb)
            DTRaw.Rows.Add(-1, vbTab & vbTab, "Line: " & L & "  to:   " & ra)
        Next
        ChangeLog &= vbTab & "</End of Change Log-->" & vbCrLf
        DTRaw.Rows.Add(-1, vbTab, "</End of Change Log-->")
        DTCfg.DefaultView.RowFilter = "Edited = True"
        Dim ne As Integer = DTCfg.DefaultView.Count

        'Rename the original data file and recreate it with the edited data

        Dim NewFile As String = CFGFileName.Substring(CFGFileName.LastIndexOf("\") + 1)
        Dim p0 As Integer = NewFile.LastIndexOf(".")
        NewFile = NewFile.Substring(0, p0) & Now.ToString("_yyyyMMddTHHmmss") & NewFile.Substring(p0)
        My.Computer.FileSystem.RenameFile(CFGFileName, NewFile)

        Dim sw As System.IO.StreamWriter = My.Computer.FileSystem.OpenTextFileWriter(CFGFileName, False)
        For Each dr As DataRow In DTRaw.Rows
            sw.WriteLine(dr("WhiteSpace") & dr("Data"))
        Next
        sw.WriteLine(ChangeLog)
        sw.Close()

        DTCfg.DefaultView.RowFilter = "Edited = True"
        For Each drv As DataRowView In DTCfg.DefaultView
            drv("Edited") = False
        Next
        DTCfg.DefaultView.RowFilter = Nothing
        DTCfg.AcceptChanges()
        TC.SelectedItem = Tab_All
        MsgBox("Original data saved to " & NewFile, MsgBoxStyle.OkOnly, "Data File Renamed")
    End Sub

    Private Sub BtnRejectChanges_Click(sender As Object, e As RoutedEventArgs) Handles BtnRejectChanges.Click
        DTCfg.DefaultView.RowFilter = "Edited = True"
        For Each drv As DataRowView In DTCfg.DefaultView
            drv("Edited") = False
        Next
        DTCfg.DefaultView.RowFilter = Nothing
        DTCfg.RejectChanges()
        TC.SelectedItem = Tab_All
    End Sub



#End Region
End Class
Public Class Edit_Made_Converter
    Implements System.Windows.Data.IValueConverter

    Public Function ConvertBack1(ByVal value As Object, ByVal targetType As System.Type, ByVal parameter As Object, ByVal culture As System.Globalization.CultureInfo) As Object Implements System.Windows.Data.IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

    Public Function Convert(ByVal value As Object, ByVal targetType As System.Type, ByVal parameter As Object, ByVal culture As System.Globalization.CultureInfo) As Object Implements System.Windows.Data.IValueConverter.Convert
        Dim edited As Boolean = value
        If edited = True Then
            Return Brushes.Red
        Else
            Return Brushes.Black
        End If


    End Function
End Class