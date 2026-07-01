Option Strict On
Option Explicit On

Imports System
Imports System.Drawing
Imports System.Windows.Forms
Imports System.Collections.Generic

Public Class frmXO
    Inherits Form

    ' === Controls ===
    Private txtIP As TextBox
    Private txtPort As TextBox
    Private WithEvents btnHost As Button
    Private WithEvents btnConnect As Button
    Private WithEvents btnAI As Button
    Private WithEvents btnReset As Button
    Private lblStatus As Label
    Private lblTurn As Label

    ' === Card nguoi choi (giong kieu MineForm.vb) ===
    Private pnlCardMe As Panel
    Private pnlCardOpp As Panel
    Private lblCardMeInfo As Label
    Private lblCardOppInfo As Label

    ' === Khung chat (giong kieu MineForm.vb) ===
    Private pnlChat As Panel
    Private lstChat As ListBox
    Private txtChatInput As TextBox
    Private WithEvents btnSend As Button

    ' === Timer cho AI "suy nghi" truoc khi danh ===
    Private WithEvents aiTimer As System.Windows.Forms.Timer
    Private rnd As New Random()

    ' === Kich thuoc ban co ===
    Private Const ROWS As Integer = 10
    Private Const COLS As Integer = 10
    Private Const WIN_COUNT As Integer = 5
    Private Const CELL_COUNT As Integer = ROWS * COLS

    ' === Kich thuoc khu vuc ben phai (card + chat) ===
    Private Const SIDE_X As Integer = 460
    Private Const SIDE_W As Integer = 200
    Private Const SIDE_TOP As Integer = 75
    Private Const SIDE_BOTTOM As Integer = 495
    Private Const CARD_H As Integer = 58
    Private Const CARD_GAP As Integer = 8

    Private btnCell(CELL_COUNT - 1) As Button

    ' === Network (tach rieng qua NetworkPeer) ===
    Private peer As NetworkPeer
    Private isHost As Boolean = False

    ' === Game state ===
    Private board(CELL_COUNT - 1) As Char
    Private mySymbol As Char = "X"c
    Private isMyTurn As Boolean = False
    Private isConnected As Boolean = False
    Private gameOver As Boolean = False
    Private vsAI As Boolean = False

    Public Sub New()
        InitUI()
        ResetBoardLocal()
    End Sub

    Private Sub InitUI()
        Me.Text = "XO Online PvP - 2CongLC"
        Me.ClientSize = New Size(680, 570)
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
        btnHost.SetBounds(275, 10, 55, 25)
        Me.Controls.Add(btnHost)

        btnConnect = New Button()
        btnConnect.Text = "Connect"
        btnConnect.SetBounds(335, 10, 60, 25)
        Me.Controls.Add(btnConnect)

        btnAI = New Button()
        btnAI.Text = "Danh AI"
        btnAI.SetBounds(400, 10, 65, 25)
        btnAI.BackColor = Color.MediumPurple
        btnAI.ForeColor = Color.White
        Me.Controls.Add(btnAI)

        lblStatus = New Label()
        lblStatus.Text = "Chua ket noi"
        lblStatus.SetBounds(10, 45, 470, 20)
        lblStatus.ForeColor = Color.DarkBlue
        Me.Controls.Add(lblStatus)

        Dim cellSize As Integer = 40
        Dim gap As Integer = 2
        Dim startX As Integer = 20
        Dim startY As Integer = 75

        Dim i As Integer = 0
        Dim r As Integer
        Dim c As Integer
        For r = 0 To ROWS - 1
            For c = 0 To COLS - 1
                Dim btn As New Button()
                btn.SetBounds(startX + c * (cellSize + gap), startY + r * (cellSize + gap), cellSize, cellSize)
                btn.Font = New Font("Segoe UI", 14, FontStyle.Bold)
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
        lblTurn.SetBounds(10, startY + ROWS * (cellSize + gap) + 5, 440, 20)
        lblTurn.Font = New Font("Segoe UI", 10, FontStyle.Bold)
        Me.Controls.Add(lblTurn)

        btnReset = New Button()
        btnReset.Text = "Choi lai"
        btnReset.SetBounds(190, startY + ROWS * (cellSize + gap) + 30, 100, 30)
        btnReset.Enabled = False
        Me.Controls.Add(btnReset)

        aiTimer = New System.Windows.Forms.Timer()
        aiTimer.Interval = 450

        BuildSidePanel()
    End Sub

    ' ================= CARD + CHAT (giong kieu MineForm.vb) =================
    Private Sub BuildSidePanel()
        pnlCardMe = BuildPlayerCard("BAN", Color.DodgerBlue, New Point(SIDE_X, SIDE_TOP), SIDE_W, lblCardMeInfo)
        Me.Controls.Add(pnlCardMe)

        pnlCardOpp = BuildPlayerCard("DOI THU", Color.OrangeRed, New Point(SIDE_X, SIDE_TOP + CARD_H + CARD_GAP), SIDE_W, lblCardOppInfo)
        Me.Controls.Add(pnlCardOpp)

        Dim chatY As Integer = SIDE_TOP + 2 * CARD_H + CARD_GAP + CARD_GAP
        BuildChatPanel(SIDE_X, chatY, SIDE_W, SIDE_BOTTOM - chatY)
    End Sub

    ' Tao 1 the (card) thong tin nguoi choi: thanh mau | ten | trang thai
    Private Function BuildPlayerCard(ByVal title As String, ByVal accent As Color, ByVal loc As Point, ByVal w As Integer, ByRef infoLabel As Label) As Panel
        Dim p As New Panel()
        p.Location = loc
        p.Size = New Size(w, CARD_H)
        p.BackColor = Color.FromArgb(35, 35, 35)

        ' Thanh mau doc ben trai
        Dim bar As New Panel()
        bar.Location = New Point(0, 0)
        bar.Size = New Size(4, CARD_H)
        bar.BackColor = accent
        p.Controls.Add(bar)

        ' Ten (BAN / DOI THU)
        Dim lblTitle As New Label()
        lblTitle.Text = title
        lblTitle.Font = New Font("Segoe UI", 9.0!, FontStyle.Bold)
        lblTitle.ForeColor = accent
        lblTitle.Location = New Point(12, 6)
        lblTitle.AutoSize = True
        p.Controls.Add(lblTitle)

        ' Dong thong tin (quan co / trang thai)
        infoLabel = New Label()
        infoLabel.Text = "-"
        infoLabel.Font = New Font("Segoe UI", 9.0!)
        infoLabel.ForeColor = Color.LightGray
        infoLabel.Location = New Point(12, 26)
        infoLabel.AutoSize = True
        p.Controls.Add(infoLabel)

        Return p
    End Function

    ' Khung chat: ListBox hien tin nhan + TextBox go + nut Gui (giong MineForm.vb)
    Private Sub BuildChatPanel(ByVal x As Integer, ByVal y As Integer, ByVal w As Integer, ByVal h As Integer)
        pnlChat = New Panel()
        pnlChat.Location = New Point(x, y)
        pnlChat.Size = New Size(w, h)
        pnlChat.BackColor = Color.FromArgb(20, 20, 20)

        lstChat = New ListBox()
        lstChat.Location = New Point(0, 0)
        lstChat.Size = New Size(w, h - 32)
        lstChat.BackColor = Color.FromArgb(35, 35, 35)
        lstChat.ForeColor = Color.LightGray
        lstChat.BorderStyle = BorderStyle.FixedSingle
        pnlChat.Controls.Add(lstChat)

        txtChatInput = New TextBox()
        txtChatInput.Location = New Point(0, h - 27)
        txtChatInput.Size = New Size(w - 55, 25)
        txtChatInput.Enabled = False
        AddHandler txtChatInput.KeyDown, Sub(s As Object, ev As KeyEventArgs)
            If ev.KeyCode = Keys.Enter Then
                BtnSend_Click(s, EventArgs.Empty)
                ev.Handled = True
                ev.SuppressKeyPress = True
            End If
        End Sub
        pnlChat.Controls.Add(txtChatInput)

        btnSend = New Button()
        btnSend.Text = "Gui"
        btnSend.Location = New Point(w - 50, h - 28)
        btnSend.Size = New Size(50, 27)
        btnSend.BackColor = Color.SteelBlue
        btnSend.ForeColor = Color.White
        btnSend.FlatStyle = FlatStyle.Flat
        btnSend.Enabled = False
        pnlChat.Controls.Add(btnSend)

        Me.Controls.Add(pnlChat)
    End Sub

    Private Sub BtnSend_Click(ByVal sender As Object, ByVal e As EventArgs) Handles btnSend.Click
        If Not isConnected OrElse vsAI Then Return
        If txtChatInput.Text.Trim() = "" Then Return
        Dim msg As String = txtChatInput.Text.Trim()
        AppendChat("Ban: " & msg)
        If peer IsNot Nothing Then peer.SendLine("CHAT|" & msg)
        txtChatInput.Text = ""
        txtChatInput.Focus()
    End Sub

    Private Sub AppendChat(ByVal msg As String)
        If lstChat Is Nothing Then Return
        lstChat.Items.Add(msg)
        lstChat.TopIndex = Math.Max(0, lstChat.Items.Count - 1)
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
            peer = New NetworkPeer(Me)
            HookPeerEvents()
            isHost = True
            lblStatus.Text = String.Format("Dang cho doi thu ket noi tren port {0}...", port)
            btnHost.Enabled = False
            btnConnect.Enabled = False
            btnAI.Enabled = False
            peer.StartHost(port)
        Catch ex As Exception
            MessageBox.Show("Loi khi mo Host: " & ex.Message)
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
        peer = New NetworkPeer(Me)
        HookPeerEvents()
        isHost = False
        btnHost.Enabled = False
        btnConnect.Enabled = False
        btnAI.Enabled = False
        lblStatus.Text = "Dang ket noi..."
        peer.ConnectToHost(ip, port)
    End Sub

    ' ================= DANH VOI AI =================
    Private Sub btnAI_Click(ByVal sender As Object, ByVal e As EventArgs) Handles btnAI.Click
        If isConnected Then Return
        vsAI = True
        mySymbol = "X"c
        isMyTurn = True
        isConnected = True
        btnHost.Enabled = False
        btnConnect.Enabled = False
        btnAI.Enabled = False
        btnReset.Enabled = True
        lblStatus.Text = "Dang choi voi AI. Ban la quan 'X'."
        ResetBoardLocal()
        EnableBoard(isMyTurn)
        UpdateTurnLabel()
        UpdateCardInfo()
        lstChat.Items.Clear()
        txtChatInput.Enabled = False
        btnSend.Enabled = False
    End Sub

    ' ================= PEER EVENTS =================
    Private Sub HookPeerEvents()
        AddHandler peer.Connected, AddressOf Peer_Connected
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
    End Sub

    Private Sub Peer_Connected()
        mySymbol = If(isHost, "X"c, "O"c)
        isMyTurn = isHost
        isConnected = True
        OnConnected()
    End Sub

    Private Sub Peer_Disconnected()
        isConnected = False
        OnDisconnected()
    End Sub

    Private Sub Peer_LineReceived(ByVal line As String)
        ProcessMessage(line)
    End Sub

    Private Sub OnConnected()
        lblStatus.Text = String.Format("Da ket noi. Ban la quan '{0}'", mySymbol)
        btnReset.Enabled = True
        ResetBoardLocal()
        EnableBoard(isMyTurn)
        UpdateTurnLabel()
        UpdateCardInfo()
        lstChat.Items.Clear()
        txtChatInput.Enabled = True
        btnSend.Enabled = True
    End Sub

    Private Sub UpdateCardInfo()
        Dim oppSymbol As Char = If(mySymbol = "X"c, "O"c, "X"c)
        lblCardMeInfo.Text = "Quan: " & mySymbol
        If vsAI Then
            lblCardOppInfo.Text = "Quan: " & oppSymbol & " (AI)"
        Else
            lblCardOppInfo.Text = "Quan: " & oppSymbol
        End If
    End Sub

    Private Sub OnDisconnected()
        lblStatus.Text = "Doi thu da ngat ket noi."
        EnableBoard(False)
        btnReset.Enabled = False
        btnHost.Enabled = True
        btnConnect.Enabled = True
        btnAI.Enabled = True
        txtChatInput.Enabled = False
        btnSend.Enabled = False
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
            Case "CHAT"
                Dim chatMsg As String = String.Join("|", parts, 1, parts.Length - 1)
                AppendChat("Doi thu: " & chatMsg)
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

        If sendNetwork AndAlso Not vsAI AndAlso peer IsNot Nothing Then
            peer.SendLine("MOVE|" & idx.ToString() & "|" & sym.ToString())
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

        If vsAI AndAlso Not gameOver AndAlso Not isMyTurn Then
            aiTimer.Start()
        End If
    End Sub

    Private Function CheckWin(ByVal sym As Char) As Boolean
        ' 4 huong: ngang, doc, cheo xuoi (\), cheo nguoc (/)
        Dim dr(3) As Integer
        Dim dc(3) As Integer
        dr(0) = 0 : dc(0) = 1   ' ngang
        dr(1) = 1 : dc(1) = 0   ' doc
        dr(2) = 1 : dc(2) = 1   ' cheo \
        dr(3) = 1 : dc(3) = -1  ' cheo /

        Dim r As Integer
        Dim c As Integer
        Dim d As Integer
        For r = 0 To ROWS - 1
            For c = 0 To COLS - 1
                If board(r * COLS + c) <> sym Then Continue For

                For d = 0 To 3
                    Dim count As Integer = 1
                    Dim nr As Integer = r + dr(d)
                    Dim nc As Integer = c + dc(d)
                    Do While nr >= 0 AndAlso nr < ROWS AndAlso nc >= 0 AndAlso nc < COLS AndAlso board(nr * COLS + nc) = sym
                        count += 1
                        If count >= WIN_COUNT Then Return True
                        nr += dr(d)
                        nc += dc(d)
                    Loop
                Next d
            Next c
        Next r
        Return False
    End Function

    Private Function IsBoardFull() As Boolean
        Dim i As Integer
        For i = 0 To CELL_COUNT - 1
            If board(i) = " "c Then Return False
        Next i
        Return True
    End Function

    Private Sub UpdateTurnLabel()
        If gameOver Then Return
        If isMyTurn Then
            lblTurn.Text = String.Format("Luot cua: BAN ({0})", mySymbol)
        ElseIf vsAI Then
            lblTurn.Text = "Luot cua: AI"
        Else
            lblTurn.Text = "Luot cua: DOI THU"
        End If
    End Sub

    Private Sub EnableBoard(ByVal en As Boolean)
        Dim i As Integer
        For i = 0 To CELL_COUNT - 1
            btnCell(i).Enabled = en AndAlso (board(i) = " "c)
        Next i
    End Sub

    Private Sub ResetBoardLocal()
        aiTimer.Stop()
        Dim i As Integer
        For i = 0 To CELL_COUNT - 1
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
        If Not vsAI AndAlso peer IsNot Nothing Then
            peer.SendLine("RESET")
        End If
    End Sub

    ' ================= AI: chon nuoc di =================
    Private Sub AiTimer_Tick(ByVal sender As Object, ByVal e As EventArgs) Handles aiTimer.Tick
        aiTimer.Stop()
        AIMakeMove()
    End Sub

    Private Sub AIMakeMove()
        If gameOver OrElse Not isConnected OrElse Not vsAI OrElse isMyTurn Then Return
        Dim aiSymbol As Char = If(mySymbol = "X"c, "O"c, "X"c)

        Dim bestScore As Long = Long.MinValue
        Dim bestCells As New List(Of Integer)

        Dim r As Integer
        Dim c As Integer
        For r = 0 To ROWS - 1
            For c = 0 To COLS - 1
                Dim idx As Integer = r * COLS + c
                If board(idx) <> " "c Then Continue For

                Dim offense As Long = EvaluateCell(r, c, aiSymbol)
                Dim defense As Long = EvaluateCell(r, c, mySymbol)
                Dim score As Long = offense + CLng(defense * 0.9)

                If score > bestScore Then
                    bestScore = score
                    bestCells.Clear()
                    bestCells.Add(idx)
                ElseIf score = bestScore Then
                    bestCells.Add(idx)
                End If
            Next c
        Next r

        If bestCells.Count = 0 Then Return
        Dim chosen As Integer = bestCells(rnd.Next(bestCells.Count))
        ApplyMove(chosen, aiSymbol, False)
    End Sub

    ' Danh gia mot o trong (r, c) neu dat quan 'sym' vao thi loi bao nhieu diem
    ' (tinh theo 4 huong: ngang, doc, 2 duong cheo)
    Private Function EvaluateCell(ByVal r As Integer, ByVal c As Integer, ByVal sym As Char) As Long
        Dim dr() As Integer = {0, 1, 1, 1}
        Dim dc() As Integer = {1, 0, 1, -1}
        Dim total As Long = 0
        Dim d As Integer

        For d = 0 To 3
            Dim count As Integer = 1
            Dim openEnds As Integer = 0

            ' huong thuan
            Dim nr As Integer = r + dr(d)
            Dim nc As Integer = c + dc(d)
            Do While nr >= 0 AndAlso nr < ROWS AndAlso nc >= 0 AndAlso nc < COLS AndAlso board(nr * COLS + nc) = sym
                count += 1
                nr += dr(d)
                nc += dc(d)
            Loop
            If nr >= 0 AndAlso nr < ROWS AndAlso nc >= 0 AndAlso nc < COLS AndAlso board(nr * COLS + nc) = " "c Then
                openEnds += 1
            End If

            ' huong nguoc
            Dim pr As Integer = r - dr(d)
            Dim pc As Integer = c - dc(d)
            Do While pr >= 0 AndAlso pr < ROWS AndAlso pc >= 0 AndAlso pc < COLS AndAlso board(pr * COLS + pc) = sym
                count += 1
                pr -= dr(d)
                pc -= dc(d)
            Loop
            If pr >= 0 AndAlso pr < ROWS AndAlso pc >= 0 AndAlso pc < COLS AndAlso board(pr * COLS + pc) = " "c Then
                openEnds += 1
            End If

            total += ScoreLine(count, openEnds)
        Next d

        Return total
    End Function

    ' Cham diem 1 duong thang dua tren so quan lien tiep va so dau ho (open ends)
    Private Function ScoreLine(ByVal count As Integer, ByVal openEnds As Integer) As Long
        If count >= WIN_COUNT Then Return 1000000

        Select Case count
            Case 4
                If openEnds = 2 Then Return 50000
                If openEnds = 1 Then Return 5000
                Return 50
            Case 3
                If openEnds = 2 Then Return 2000
                If openEnds = 1 Then Return 300
                Return 20
            Case 2
                If openEnds = 2 Then Return 100
                If openEnds = 1 Then Return 20
                Return 5
            Case Else
                Return 1
        End Select
    End Function

    Protected Overrides Sub OnFormClosing(ByVal e As FormClosingEventArgs)
        Try
            isConnected = False
            aiTimer.Stop()
            If peer IsNot Nothing Then
                peer.SendLine("BYE")
                peer.CloseConnection()
            End If
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
