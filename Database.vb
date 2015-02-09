Imports System.Threading
Imports System.Data.SqlClient
Imports System.Text.RegularExpressions

Public Class Database

    Public ConnectionString As String
    Public QueryString As String
    Public TableName As String
    Public ColumnNames As String()
    Public Records As Dictionary(Of Integer, String())
    Public PrimaryKeys As String()
    Public PrimaryKeysIndexs As Integer()
    Public ForeignKeys As String()
    Public ForeignKeysIndexs As Integer()
    Public RowsAffected As String
    Public ErrorFlag As Boolean
    Public ErrorMsg As String
    Public Order As String


    'Will get all available column names from the request
    Public Sub GetColumnNames()
        Try
            Dim Db As New SqlConnection(ConnectionString)
            Dim Cmd As New SqlCommand
            Dim Reader As SqlDataReader
            Db.Open()

            Cmd.Connection = Db
            Cmd.CommandText = QueryString
            Reader = Cmd.ExecuteReader()

            For ColumnIndex As Integer = 0 To Reader.FieldCount - 1
                ReDim Preserve ColumnNames(ColumnIndex)
                ColumnNames(ColumnIndex) = Reader.GetName(ColumnIndex)
            Next

            Reader.Close()
            Db.Close()
        Catch ex As Exception
            ErrorFlag = True
            ErrorMsg = "Column Fetching Error: " + ex.Message
        End Try
    End Sub

    'Will get all records from the request
    Public Function GetRecords()
        Dim records = New Dictionary(Of Integer, String())
        Try
            Dim Db As New SqlConnection(ConnectionString)
            Dim Cmd As New SqlCommand
            Dim Reader As SqlDataReader
            Db.Open()

            Cmd.Connection = Db
            Cmd.CommandText = QueryString
            Reader = Cmd.ExecuteReader()

            Dim RowIndex As Integer = 0
            Dim RowData(0) As String
            While Reader.Read() = True

                For index As Integer = 0 To Reader.FieldCount - 1
                    ReDim Preserve RowData(index)
                    '
                    RowData(index) = Reader(index).ToString
                Next

                records.Add(RowIndex, RowData)
                ReDim RowData(0)
                RowIndex = RowIndex + 1
            End While
            Reader.Close()
            Db.Close()
            Return records
        Catch ex As Exception
            ErrorFlag = True
            ErrorMsg = "Record Fetching Error: " + ex.Message
            Return records
        End Try
    End Function

    'executes an sql statement on the database
    Public Sub ExecuteStatement()
        Try
            Dim Db As New SqlConnection(ConnectionString)
            Dim Cmd As New SqlCommand
            Db.Open()
            Cmd.Connection = Db
            Cmd.CommandText = QueryString
            RowsAffected = Cmd.ExecuteNonQuery()
            Db.Close()
        Catch ex As Exception
            ErrorFlag = True
            ErrorMsg = "Execution Error: " + ex.Message
        End Try
    End Sub

    'get back table data
    Public Sub GetTable()
        QueryString = "SELECT * FROM " + TableName + " ORDER BY " + Order
        GetColumnNames()
        If Records IsNot Nothing Then
            Records.Clear()
        End If
        Records = GetRecords()
    End Sub
    
    'get the primary keys
    Public Sub GetPrimaryKeys()
        QueryString = "SELECT column_name FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE OBJECTPROPERTY(OBJECT_ID(constraint_name), 'IsPrimaryKey') = 1 AND table_name = '" + TableName + "'"
        Dim Records As Dictionary(Of Integer, String()) = GetRecords()

        Dim KeyIndex As Integer = 0
        For Each Record As String() In Records.Values
            ReDim Preserve PrimaryKeys(KeyIndex)
            PrimaryKeys(KeyIndex) = Record.GetValue(0)
            KeyIndex = KeyIndex + 1
        Next
        PrimaryKeysIndexs = GetKeyIndexs(PrimaryKeys)
    End Sub

    'get the foreign keys
    Public Sub GetForeignKeys()
        QueryString = "SELECT column_name FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE OBJECTPROPERTY(OBJECT_ID(constraint_name), 'IsForeignKey') = 1 AND table_name = '" + TableName + "'"
        Dim Records As Dictionary(Of Integer, String()) = GetRecords()

        Dim KeyIndex As Integer = 0
        For Each Record As String() In Records.Values
            ReDim Preserve ForeignKeys(KeyIndex)
            ForeignKeys(KeyIndex) = Record.GetValue(0)
            KeyIndex = KeyIndex + 1
        Next
        ForeignKeysIndexs = GetKeyIndexs(ForeignKeys)
    End Sub

    'gets the key indexs
    Public Function GetKeyIndexs(Keys As String())
        Dim KeyIndexs As Integer()
        Try
            For Index As Integer = 0 To Keys.Count - 1
                ReDim Preserve KeyIndexs(Index)
                KeyIndexs(Index) = Array.IndexOf(ColumnNames, Keys(Index))
            Next
            Return KeyIndexs
        Catch ex As Exception
            Console.WriteLine("No key indexs found!")
        End Try
        Return KeyIndexs
    End Function

    'inserts a record into the database
    Public Sub InsertRecord(ColumnIndexs As Integer(), Values As String())

        Dim ColumnString As String = ""
        For Index As Integer = 0 To ColumnIndexs.Count - 1
            If Index > 0 Then
                ColumnString = ColumnString + ", "
            End If
            ColumnString = ColumnString + ColumnNames(ColumnIndexs(Index))
        Next

        Dim ValueString As String = String.Join("', '", Values)
        QueryString = "INSERT INTO %Table% (%1%)  VALUES ('%2%');".Replace("%Table%", TableName).Replace("%1%", ColumnString).Replace("%2%", ValueString)
        ExecuteStatement()
        'GetTable()
    End Sub
  
    'deletes a record from the database
    Public Sub DeleteRecord(RowIndex As Integer)
        Dim Record As String() = Records(RowIndex)
        Dim WhereString As String = ""
        For Index As Integer = 0 To PrimaryKeys.Count - 1
            If Index > 0 Then
                WhereString = WhereString + " AND "
            End If
            WhereString = WhereString + PrimaryKeys(Index) + " = '" + Record(PrimaryKeysIndexs(Index)) + "'"
        Next
        QueryString = "DELETE %Table% WHERE %1%".Replace("%Table%", TableName).Replace("%1%", WhereString)
        ExecuteStatement()

    End Sub
    
    'updates a record
    Public Sub UpdateRecord(RowIndex As Integer, ColumnIndexs As Integer(), Values As String())

        Dim SetString As String = ""
        For Index As Integer = 0 To ColumnIndexs.Count - 1
            If Index > 0 Then
                SetString = SetString + ", "
            End If
            SetString = SetString + ColumnNames(ColumnIndexs(Index)) + "='" + Values(Index) + "'"
        Next

        Dim Record As String() = Records(RowIndex)
        Dim WhereString As String = ""
        For Index As Integer = 0 To PrimaryKeys.Count - 1
            If Index > 0 Then
                WhereString = WhereString + " AND "
            End If
            WhereString = WhereString + PrimaryKeys(Index) + " = '" + Record(PrimaryKeysIndexs(Index)) + "'"
        Next
        QueryString = "UPDATE %Table% SET %0% WHERE %1%;".Replace("%Table%", TableName).Replace("%0%", SetString).Replace("%1%", WhereString)
        ExecuteStatement()
    End Sub
    
    'Executes an sql file similar to how SQL SERVER works.
    Public Sub ExecuteSqlFile(SqlFileText As String)
        Dim Queries As String() = Split(SqlFileText, "GO")
        Dim pattern As String = "--(.*)"
        Dim Rgx1 As New Regex(pattern, RegexOptions.Multiline)

        pattern = "/\*(.|[\r\n])*?\*/"
        Dim Rgx2 As New Regex(pattern, RegexOptions.Multiline)

        For Each Query As String In Queries
                Query = Rgx1.Replace(Query, "")
                Query = Rgx2.Replace(Query, "")
                Query = Query.Replace(Environment.NewLine, "")

                pattern = "USE[ ]{1,}(.*)[;]{0,1}"
                Dim options As RegexOptions = RegexOptions.Multiline Or RegexOptions.IgnoreCase
                Dim UseString As Match = Regex.Match(Query, pattern, options)
                If UseString.Success Then
                    Me.ConnectionString = "Server=TYLER;Database=" + RTrim(UseString.Groups(1).Value) + ";Trusted_Connection=True;User Id=TYLER\tholu_000;"
                    Query = Query.Replace(UseString.Value, "")
                End If
                Me.QueryString = Query
            Me.ExecuteStatement()
            If Me.ErrorFlag Then
                Console.WriteLine("Error Launching the .sql query")
                Exit For
            End If
        Next
    End Sub

End Class
