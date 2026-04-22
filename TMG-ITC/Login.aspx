<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="TMG_ITC.Login" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server" >
    <title>Login Page</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" rel="stylesheet"/>
</head>
<body>
    <form id="form1" runat="server">
        <div class="container d-flex justify-content-center align-items-center vh-100">
    <div class="card shadow-lg p-4" style="width:400px;">
        <h3 class="text-center mb-4">Employee Login</h3>

        <div class="mb-3">
            <asp:Label ID="lblEmployeeCode" runat="server" Text="Employee Code" CssClass="form-label"></asp:Label>
            <asp:TextBox ID="txtEmployeecode" runat="server" CssClass="form-control"></asp:TextBox>
        </div>
        <div class="d-grid">
            <asp:Button ID="btnEmployeeLogin"
                runat="server"
                Text="Login"
                CssClass="btn btn-primary"
                OnClick="btnLogin_Click" />
        </div>

        <div class="mt-3 text-center">
            <asp:Label ID="lblMessage" runat="server" CssClass="text-danger"></asp:Label>
        </div>
    </div>
</div>

    </form>
</body>
</html>
