<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="TMG_ITC.Login" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Login - VC Automated System</title>
    <link href="Content/bootstrap.min.css" rel="stylesheet" />
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css" />
    <link href="Content/Site.css?v=20260427-clean3" rel="stylesheet" />
</head>
<body class="bg-ice">
    <form id="form1" runat="server">
        <div class="rail-viewport d-flex justify-content-center align-items-center">
            <div class="login-container">
                <!-- Left Side: Info -->
                <div class="login-side-info">
                    <div class="login-logo-box">
                        <i class="bi bi-camera-video" style="font-size: 2.5rem;"></i>
                    </div>
                    <h1 class="fw-bold mb-3">VC Automated System</h1>
                    <p class="opacity-75 px-4">Seamless video conference booking for your organization. Efficient, reliable, and automated.</p>
                    
                    <div class="login-footer-text">
                        TMG-ITC Department &copy; 2026
                    </div>
                </div>

                <!-- Right Side: Form -->
                <div class="login-side-form">
                    <div class="mb-5">
                        <h2 class="fw-bold mb-2">Employee Login</h2>
                        <p class="text-muted">Enter your credentials to access the modules.</p>
                    </div>

                    <div class="mb-4">
                        <label class="text-label">Employee Code</label>
                        <asp:TextBox ID="txtEmployeecode" 
                            runat="server" 
                            CssClass="form-control form-control-premium" 
                            placeholder="e.g. EMP001"></asp:TextBox>
                    </div>

                    <div class="d-grid gap-2">
                        <asp:Button ID="btnEmployeeLogin" 
                            runat="server" 
                            Text="Login" 
                            CssClass="btn-dark-navy" 
                            OnClick="btnLogin_Click" />
                    </div>

                    <asp:Label ID="lblMessage" runat="server" CssClass="text-danger mt-3 d-block text-center small"></asp:Label>
                </div>
            </div>
        </div>
    </form>
</body>
</html>
