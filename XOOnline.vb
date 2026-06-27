Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports System.Net
Imports System.Net.Sockets
Imports System.IO
Imports System.Text
Imports System.Threading

Public Class frmXO
    Inherits Form

    ' === Controls ===
    Private txtIP As TextBox
    Private txtPort As TextBox
    Private WithEvents btnHost As Button
    Private WithEvents btnConnect As Button
    Private WithEvents btnReset As Button
    Private lblStatus As Label
    Private lblTurn As Label
    Private btnCell(8) As Button

    ' === Network ===
    Private listener As TcpListener
    Private client As TcpClient
    Private netStream As NetworkStream
    Private reader As StreamReader
    Private writer As StreamWriter
    Private netThread As Thread
    Private isConnected As Boolean = False
    Private isHost As Boolean = False

    ' === Game state ===
    Private board(8) As Char
    Private mySymbol As Char = "X"c
    Private isMyTurn As Boolean = False
    Private gameOver As Boolean = False

    Public Sub New()
        InitUI()
        ResetBoardLocal()
    End Sub

    Private Sub InitUI()
        Me.Text = "XO Online PvP - 2CongLC"
        Me.ClientSize = New Size(420, 500)
        Me.FormBorderStyle = FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.StartPosition = FormStartPosition.CenterScreen

        Dim lblIP As New Label()
        lblIP.Text = "IP:"
        lblIP.SetBounds(10, 15, 35, 20)
        Me.Controls.Add(lblIP)

        txtIP = New TextBox()
        txtIP.Text = "127.0.0.1"
        txtIP.SetBounds(45, 12, 110, 20)
        Me.Controls.Add(txtIP)

        Dim lblPort As New Label()
        lblPort.Text = "Port:"
        lblPort.SetBounds(165, 15, 40, 20)
        Me.Controls.Add(lblPort)

        txtPort = New TextBox()
        txtPort.Text = "5050"
        txtPort.SetBounds(210, 12, 55, 20)
        Me.Controls.Add(txtPort)

        btnHost = New Button()
        btnHost.Text = "Host"
        btnHost.SetBounds(275, 10, 60, 25)
        Me.Controls.Add(btnHost)

        btnConnect = New Button()
        btnConnect.Text = "Connect"
        btnConnect.SetBounds(340, 10, 65, 25)
        Me.Controls.Add(btnConnect)

        lblStatus = New Label()
        lblStatus.Text = "Chua ket noi"
        lblStatus.SetBounds(10, 45, 400, 20)
        lblStatus.ForeColor = Color.DarkBlue
        Me.Controls.Add(lblStatus)

        Dim cellSize As Integer = 100
        Dim gap As Integer = 5
        Dim startX As Integer = 55
        Dim startY As Integer = 75

        Dim i As Integer = 0
        Dim r As Integer
        Dim c As Integer
        For r = 0 To 2
            For c = 0 To 2
                Dim btn As New Button()
                btn.SetBounds(startX + c * (cellSize + gap), startY + r * (cellSize + gap), cellSize, cellSize)
                btn.Font = New Font("Segoe UI", 36, FontStyle.Bold)
                btn.Tag = i
                btn.Enabled = False
                AddHandler btn.Click, AddressOf CellClick
                Me.Controls.Add(btn)
                btnCell(i) = btn
                i = i + 1
            Next c
        Next r

        lblTurn = New Label()
        lblTurn.Text = "Luot cua: -"
        lblTurn.SetBounds(10, startY + 3 * (cellSize + gap) + 5, 400, 20)
        lblTurn.Font = New Font("Segoe UI", 10, FontStyle.Bold)
        Me.Controls.Add(lblTurn)

        btnReset = New Button()
        btnReset.Text = "Choi lai"
        btnReset.SetBounds(160, startY + 3 * (cellSize + gap) + 30, 100, 30)
        btnReset.Enabled = False
        Me.Controls.Add(btnReset)
    End Sub

    ' ================= HOST =================
    Private Sub btnHost_Click(ByVal sender As Object, ByVal e As EventArgs) Handles btnHost.Click
        If isConnected Then Return
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then
            MessageBox.Show("Port khong hop le.")
            Return
        End If
        Try
            listener = New TcpListener(IPAddress.Any, port)
            listener.Start()
            isHost = True
            lblStatus.Text = String.Format("Dang cho doi thu ket noi tren port {0}...", port)
            btnHost.Enabled = False
            btnConnect.Enabled = False
            netThread = New Thread(New ThreadStart(AddressOf AcceptClientWorker))
            netThread.IsBackground = True
            netThread.Start()
        Catch ex As Exception
            MessageBox.Show("Loi khi mo Host: " & ex.Message)
        End Try
    End Sub

    Private Sub AcceptClientWorker()
        Try
            client = listener.AcceptTcpClient()
            netStream = client.GetStream()
            reader = New StreamReader(netStream, Encoding.UTF8)
            writer = New StreamWriter(netStream, Encoding.UTF8)
            writer.AutoFlush = True
            mySymbol = "X"c
            isMyTurn = True
            isConnected = True
            Me.Invoke(New MethodInvoker(AddressOf OnConnected))
            ReceiveLoop()
        Catch ex As Exception
            ' listener bi dong hoac loi ket noi - bo qua
        End Try
    End Sub

    ' ================= CLIENT =================
    Private Sub btnConnect_Click(ByVal sender As Object, ByVal e As EventArgs) Handles btnConnect.Click
        If isConnected Then Return
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then
            MessageBox.Show("Port khong hop le.")
            Return
        End If
        Dim ip As String = txtIP.Text.Trim()
        btnHost.Enabled = False
        btnConnect.Enabled = False
        lblStatus.Text = "Dang ket noi..."
        netThread = New Thread(New ParameterizedThreadStart(AddressOf ConnectWorker))
        netThread.IsBackground = True
        netThread.Start(New String() {ip, port.ToString()})
    End Sub

    Private Sub ConnectWorker(ByVal data As Object)
        Dim arr As String() = CType(data, String())
        Dim ip As String = arr(0)
        Dim port As Integer = CInt(arr(1))
        Try
            client = New TcpClient()
            client.Connect(ip, port)
            netStream = client.GetStream()
            reader = New StreamReader(netStream, Encoding.UTF8)
            writer = New StreamWriter(netStream, Encoding.UTF8)
            writer.AutoFlush = True
            isHost = False
            mySymbol = "O"c
            isMyTurn = False
            isConnected = True
            Me.Invoke(New MethodInvoker(AddressOf OnConnected))
            ReceiveLoop()
        Catch ex As Exception
            Dim errMsg As String = ex.Message
            Try
                Me.Invoke(New MethodInvoker(Sub()
                                                 lblStatus.Text = "Ket noi that bai: " & errMsg
                                                 btnHost.Enabled = True
                                                 btnConnect.Enabled = True
                                             End Sub))
            Catch
            End Try
        End Try
    End Sub

    Private Sub OnConnected()
        lblStatus.Text = String.Format("Da ket noi. Ban la quan '{0}'", mySymbol)
        btnReset.Enabled = True
        ResetBoardLocal()
        EnableBoard(isMyTurn)
        UpdateTurnLabel()
    End Sub

    ' ================= RECEIVE LOOP =================
    Private Sub ReceiveLoop()
        Try
            Do While isConnected
                Dim line As String = reader.ReadLine()
                If line Is Nothing Then
                    Exit Do
                End If
                Dim msg As String = line
                Me.Invoke(New MethodInvoker(Sub() ProcessMessage(msg)))
            Loop
        Catch ex As Exception
            ' ket noi bi dong - bo qua
        End Try
        isConnected = False
        Try
            Me.Invoke(New MethodInvoker(AddressOf OnDisconnected))
        Catch
        End Try
    End Sub

    Private Sub OnDisconnected()
        lblStatus.Text = "Doi thu da ngat ket noi."
        EnableBoard(False)
        btnReset.Enabled = False
        btnHost.Enabled = True
        btnConnect.Enabled = True
    End Sub

    Private Sub ProcessMessage(ByVal msg As String)
        Dim parts As String() = msg.Split("|"c)
        Select Case parts(0)
            Case "MOVE"
                Dim idx As Integer = CInt(parts(1))
                Dim sym As Char = CChar(parts(2))
                ApplyMove(idx, sym, False)
            Case "RESET"
                ResetBoardLocal()
                isMyTurn = (mySymbol = "X"c)
                EnableBoard(isMyTurn)
                UpdateTurnLabel()
                lblStatus.Text = "Doi thu da bat dau lai tran moi."
        End Select
    End Sub

    ' ================= GAME LOGIC =================
    Private Sub CellClick(ByVal sender As Object, ByVal e As EventArgs)
        If Not isConnected OrElse gameOver OrElse Not isMyTurn Then Return
        Dim btn As Button = CType(sender, Button)
        Dim idx As Integer = CInt(btn.Tag)
        If board(idx) <> " "c Then Return

        ApplyMove(idx, mySymbol, True)
    End Sub

    Private Sub ApplyMove(ByVal idx As Integer, ByVal sym As Char, ByVal sendNetwork As Boolean)
        board(idx) = sym
        btnCell(idx).Text = sym.ToString()
        If sym = "X"c Then
            btnCell(idx).ForeColor = Color.Blue
        Else
            btnCell(idx).ForeColor = Color.Red
        End If
        btnCell(idx).Enabled = False

        If sendNetwork Then
            writer.WriteLine("MOVE|" & idx.ToString() & "|" & sym.ToString())
        End If

        If CheckWin(sym) Then
            gameOver = True
            EnableBoard(False)
            If sym = mySymbol Then
                lblStatus.Text = "Ban da THANG!"
            Else
                lblStatus.Text = "Ban da THUA!"
            End If
            lblTurn.Text = "Tran dau ket thuc."
            Return
        End If

        If IsBoardFull() Then
            gameOver = True
            EnableBoard(False)
            lblStatus.Text = "Hoa!"
            lblTurn.Text = "Tran dau ket thuc."
            Return
        End If

        isMyTurn = Not isMyTurn
        EnableBoard(isMyTurn)
        UpdateTurnLabel()
    End Sub

    Private Function CheckWin(ByVal sym As Char) As Boolean
        Dim lines(7, 2) As Integer
        lines(0, 0) = 0 : lines(0, 1) = 1 : lines(0, 2) = 2
        lines(1, 0) = 3 : lines(1, 1) = 4 : lines(1, 2) = 5
        lines(2, 0) = 6 : lines(2, 1) = 7 : lines(2, 2) = 8
        lines(3, 0) = 0 : lines(3, 1) = 3 : lines(3, 2) = 6
        lines(4, 0) = 1 : lines(4, 1) = 4 : lines(4, 2) = 7
        lines(5, 0) = 2 : lines(5, 1) = 5 : lines(5, 2) = 8
        lines(6, 0) = 0 : lines(6, 1) = 4 : lines(6, 2) = 8
        lines(7, 0) = 2 : lines(7, 1) = 4 : lines(7, 2) = 6

        Dim k As Integer
        For k = 0 To 7
            If board(lines(k, 0)) = sym AndAlso board(lines(k, 1)) = sym AndAlso board(lines(k, 2)) = sym Then
                Return True
            End If
        Next k
        Return False
    End Function

    Private Function IsBoardFull() As Boolean
        Dim i As Integer
        For i = 0 To 8
            If board(i) = " "c Then Return False
        Next i
        Return True
    End Function

    Private Sub UpdateTurnLabel()
        If gameOver Then Return
        If isMyTurn Then
            lblTurn.Text = String.Format("Luot cua: BAN ({0})", mySymbol)
        Else
            lblTurn.Text = "Luot cua: DOI THU"
        End If
    End Sub

    Private Sub EnableBoard(ByVal en As Boolean)
        Dim i As Integer
        For i = 0 To 8
            btnCell(i).Enabled = en AndAlso (board(i) = " "c)
        Next i
    End Sub

    Private Sub ResetBoardLocal()
        Dim i As Integer
        For i = 0 To 8
            board(i) = " "c
            btnCell(i).Text = ""
            btnCell(i).Enabled = isConnected
            btnCell(i).ForeColor = Color.Black
        Next i
        gameOver = False
    End Sub

    ' ================= RESET BUTTON =================
    Private Sub btnReset_Click(ByVal sender As Object, ByVal e As EventArgs) Handles btnReset.Click
        If Not isConnected Then Return
        ResetBoardLocal()
        isMyTurn = (mySymbol = "X"c)
        EnableBoard(isMyTurn)
        UpdateTurnLabel()
        lblStatus.Text = "Ban da bat dau lai tran moi."
        Try
            writer.WriteLine("RESET")
        Catch
        End Try
    End Sub

    Protected Overrides Sub OnFormClosing(ByVal e As FormClosingEventArgs)
        Try
            isConnected = False
            If writer IsNot Nothing Then writer.WriteLine("BYE")
            If client IsNot Nothing Then client.Close()
            If listener IsNot Nothing Then listener.Stop()
        Catch
        End Try
        MyBase.OnFormClosing(e)
    End Sub

    <STAThread()>
    Public Shared Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New frmXO())
    End Sub

End Class
