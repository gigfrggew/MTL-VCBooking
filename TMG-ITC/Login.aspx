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
    <style>
        .login-side-form {
            position: relative;
            overflow: hidden;
            min-height: 500px;
        }

        .login-form-content {
            transition: all 0.4s ease;
        }

        .login-form-content.searching {
            opacity: 0;
            transform: translateY(-20px);
            pointer-events: none;
        }

        .login-status-container {
            position: absolute;
            inset: 0;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            padding: 40px;
            text-align: center;
            opacity: 0;
            transform: translateY(20px);
            transition: all 0.4s cubic-bezier(0.34, 1.56, 0.64, 1);
            pointer-events: none;
            background: #fff;
            z-index: 10;
        }

        .login-status-container.show {
            opacity: 1;
            transform: translateY(0);
            pointer-events: auto;
        }

        /* Animated Icons */
        .icon-box {
            width: 90px;
            height: 90px;
            margin: 0 auto 24px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 44px;
            transition: all 0.3s ease;
        }

        .icon-success { background: #dcfce7; color: #22c55e; }
        .icon-error { background: #fee2e2; color: #ef4444; }

        .pulse-success { animation: pulse-green 2s infinite; }
        @keyframes pulse-green {
            0% { box-shadow: 0 0 0 0 rgba(34, 197, 94, 0.4); }
            70% { box-shadow: 0 0 0 20px rgba(34, 197, 94, 0); }
            100% { box-shadow: 0 0 0 0 rgba(34, 197, 94, 0); }
        }

        .shake { animation: shake 0.5s; }
        @keyframes shake {
            0%, 100% { transform: translateX(0); }
            25% { transform: translateX(-10px); }
            75% { transform: translateX(10px); }
        }
    </style>
</head>
<body class="bg-ice">
    <form id="form1" runat="server">
        <asp:ScriptManager ID="ScriptManager1" runat="server" />
        
        <div class="rail-viewport d-flex justify-content-center align-items-center">
            <asp:UpdatePanel ID="UpdatePanel1" runat="server">
                <ContentTemplate>
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
                            <!-- In-Card Status -->
                            <div id="loginStatus" class="login-status-container">
                                <div id="loginIconBox" class="icon-box">
                                    <div id="loginSpinner" class="spinner-border text-primary" style="width: 3rem; height: 3rem;" role="status"></div>
                                    <i id="loginSuccessIcon" class="bi bi-check-lg d-none"></i>
                                    <i id="loginErrorIcon" class="bi bi-x-lg d-none"></i>
                                </div>
                                <h3 id="loginStatusTitle" class="fw-bold mb-2">Verifying</h3>
                                <p id="loginStatusMsg" class="text-secondary mb-0">Checking your credentials...</p>
                            </div>

                            <div id="loginFormContent" class="login-form-content">
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
                                        OnClientClick="showLoginProcessing();"
                                        OnClick="btnLogin_Click" />
                                </div>

                                <asp:Label ID="lblMessage" runat="server" CssClass="text-danger mt-3 d-block text-center small"></asp:Label>
                            </div>
                        </div>
                    </div>
                </ContentTemplate>
            </asp:UpdatePanel>
        </div>

        <script>
            function showLoginProcessing() {
                var formContent = document.getElementById('loginFormContent');
                var status = document.getElementById('loginStatus');
                var spinner = document.getElementById('loginSpinner');
                var successIcon = document.getElementById('loginSuccessIcon');
                var errorIcon = document.getElementById('loginErrorIcon');
                var iconBox = document.getElementById('loginIconBox');
                var title = document.getElementById('loginStatusTitle');
                var msg = document.getElementById('loginStatusMsg');

                if (formContent) formContent.classList.add('searching');
                if (status) status.classList.add('show');
                
                if (spinner) spinner.classList.remove('d-none');
                if (successIcon) successIcon.classList.add('d-none');
                if (errorIcon) errorIcon.classList.add('d-none');
                if (iconBox) iconBox.className = 'icon-box';
                if (title) title.innerText = 'Verifying';
                if (msg) msg.innerText = 'Checking your credentials...';
            }

            function showLoginResult(isSuccess, message) {
                var formContent = document.getElementById('loginFormContent');
                var status = document.getElementById('loginStatus');
                var spinner = document.getElementById('loginSpinner');
                var successIcon = document.getElementById('loginSuccessIcon');
                var errorIcon = document.getElementById('loginErrorIcon');
                var iconBox = document.getElementById('loginIconBox');
                var title = document.getElementById('loginStatusTitle');
                var msg = document.getElementById('loginStatusMsg');

                // Re-apply visibility states since UpdatePanel just replaced the DOM
                if (formContent) formContent.classList.add('searching');
                if (status) status.classList.add('show');
                if (spinner) spinner.classList.add('d-none');
                
                if (isSuccess) {
                    if (successIcon) successIcon.classList.remove('d-none');
                    if (iconBox) iconBox.className = 'icon-box icon-success pulse-success';
                    if (title) title.innerText = 'Success!';
                    if (msg) msg.innerText = 'Welcome back. Redirecting...';
                    
                    setTimeout(function() {
                        window.location.href = 'Dashboard.aspx';
                    }, 1200);
                } else {
                    if (errorIcon) errorIcon.classList.remove('d-none');
                    if (iconBox) iconBox.className = 'icon-box icon-error shake';
                    if (title) title.innerText = 'Access Denied';
                    if (msg) msg.innerText = message || 'Invalid credentials.';
                    
                    setTimeout(function() {
                        if (status) status.classList.remove('show');
                        if (formContent) formContent.classList.remove('searching');
                    }, 2200);
                }
            }
        </script>
    </form>
</body>
</html>
